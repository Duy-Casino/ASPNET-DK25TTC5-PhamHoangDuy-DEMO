using System.Buffers.Text;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Common.Options;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Entities;

namespace OnlineLearning.Application.Features.Auth;

public interface IAuthService
{
    Task<Result<AuthResult>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<Result<AuthResult>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<Result<AuthResult>> RefreshAsync(string? refreshToken, CancellationToken ct = default);
    Task LogoutAsync(string? refreshToken, CancellationToken ct = default);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);
    Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
    Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct = default);
    Task<Result<UserDto>> GetProfileAsync(Guid userId, CancellationToken ct = default);
    Task<Result<UserDto>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default);
    Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string? currentRefreshToken, CancellationToken ct = default);
}

public sealed class AuthService(
    UserManager<AppUser> userManager,
    IAppDbContext db,
    IJwtTokenService jwt,
    IEmailSender emailSender,
    IDateTimeProvider clock,
    ICurrentUser currentUser,
    IOptions<JwtOptions> jwtOptions,
    IOptions<AppOptions> appOptions,
    ILogger<AuthService> logger) : IAuthService
{
    private static readonly Error InvalidCredentials =
        new("invalid_credentials", "Email hoặc mật khẩu không đúng.", ErrorType.Unauthorized);
    private static readonly Error InvalidRefresh =
        new("invalid_refresh_token", "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.", ErrorType.Unauthorized);

    public async Task<Result<AuthResult>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
            return Error.Conflict("Email đã được sử dụng.");

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            CreatedAt = clock.UtcNow,
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded) return IdentityError(created);

        await userManager.AddToRoleAsync(user, Roles.Student);
        await SendConfirmationEmailAsync(user, ct);

        return await IssueTokensAsync(user, ct);
    }

    public async Task<Result<AuthResult>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null) return InvalidCredentials;

        if (await userManager.IsLockedOutAsync(user))
            return new Error("locked_out", "Tài khoản tạm khóa do đăng nhập sai nhiều lần. Thử lại sau 15 phút.", ErrorType.Unauthorized);

        if (!user.IsActive)
            return new Error("account_disabled", "Tài khoản đã bị vô hiệu hóa. Liên hệ quản trị viên.", ErrorType.Forbidden);

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            return InvalidCredentials;
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return await IssueTokensAsync(user, ct);
    }

    public async Task<Result<AuthResult>> RefreshAsync(string? refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return InvalidRefresh;

        var now = clock.UtcNow;
        var hash = jwt.HashToken(refreshToken);
        var stored = await db.RefreshTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null) return InvalidRefresh;

        if (stored.RevokedAt is not null)
        {
            // Token đã bị xoay vòng mà vẫn được dùng lại → có thể bị đánh cắp: thu hồi toàn bộ phiên.
            logger.LogWarning("Phát hiện tái sử dụng refresh token của user {UserId}", stored.UserId);
            await RevokeAllAsync(stored.UserId, null, ct);
            return InvalidRefresh;
        }

        if (!stored.IsActive(now) || !stored.User.IsActive || await userManager.IsLockedOutAsync(stored.User))
            return InvalidRefresh;

        var (newRaw, newEntity) = CreateRefreshToken(stored.UserId, now);
        stored.RevokedAt = now;
        stored.RevokedByIp = currentUser.IpAddress;
        stored.ReplacedByTokenHash = newEntity.TokenHash;
        db.RefreshTokens.Add(newEntity);
        await db.SaveChangesAsync(ct);

        var roles = await userManager.GetRolesAsync(stored.User);
        var (access, accessExp) = jwt.CreateAccessToken(stored.User, roles);
        return new AuthResult(new AuthResponse(access, accessExp, ToDto(stored.User, roles)), newRaw, newEntity.ExpiresAt);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var hash = jwt.HashToken(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = clock.UtcNow;
            stored.RevokedByIp = currentUser.IpAddress;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        // Luôn trả về thành công để không lộ email nào tồn tại.
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive) return;

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var link = $"{appOptions.Value.ClientUrl.TrimEnd('/')}/dat-lai-mat-khau" +
                   $"?email={Uri.EscapeDataString(user.Email!)}&token={EncodeToken(token)}";

        await emailSender.SendAsync(user.Email!, "Đặt lại mật khẩu EduViet",
            $"<p>Chào {WebUtility.HtmlEncode(user.FullName)},</p>" +
            $"<p>Nhấn vào liên kết sau để đặt lại mật khẩu (hiệu lực 1 giờ):</p>" +
            $"<p><a href=\"{link}\">Đặt lại mật khẩu</a></p>" +
            "<p>Nếu bạn không yêu cầu, hãy bỏ qua email này.</p>", ct);
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var invalid = Error.Validation("Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null) return Result.Failure(invalid);

        var token = DecodeToken(request.Token);
        if (token is null) return Result.Failure(invalid);

        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            return result.Errors.Any(e => e.Code == "InvalidToken")
                ? Result.Failure(invalid)
                : Result.Failure(IdentityError(result));
        }

        await userManager.ResetAccessFailedCountAsync(user);
        await userManager.SetLockoutEndDateAsync(user, null);
        await RevokeAllAsync(user.Id, null, ct);
        return Result.Success();
    }

    public async Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        var token = DecodeToken(request.Token);
        if (user is null || token is null)
            return Result.Failure(Error.Validation("Liên kết xác nhận không hợp lệ."));

        var result = await userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded
            ? Result.Success()
            : Result.Failure(Error.Validation("Liên kết xác nhận không hợp lệ hoặc đã hết hạn."));
    }

    public async Task<Result<UserDto>> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Error.NotFound("Không tìm thấy người dùng.");
        return ToDto(user, await userManager.GetRolesAsync(user));
    }

    public async Task<Result<UserDto>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Error.NotFound("Không tìm thấy người dùng.");

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.Headline = request.Headline?.Trim();
        user.Bio = request.Bio?.Trim();
        user.AvatarUrl = request.AvatarUrl;
        user.UpdatedAt = clock.UtcNow;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) return IdentityError(result);
        return ToDto(user, await userManager.GetRolesAsync(user));
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string? currentRefreshToken, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Result.Failure(Error.NotFound("Không tìm thấy người dùng."));

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return result.Errors.Any(e => e.Code == "PasswordMismatch")
                ? Result.Failure(Error.Validation("Mật khẩu hiện tại không đúng."))
                : Result.Failure(IdentityError(result));
        }

        // Đăng xuất mọi thiết bị khác, giữ phiên hiện tại.
        var keep = string.IsNullOrWhiteSpace(currentRefreshToken) ? null : jwt.HashToken(currentRefreshToken);
        await RevokeAllAsync(user.Id, keep, ct);
        return Result.Success();
    }

    // ---------------- helpers ----------------

    private async Task<Result<AuthResult>> IssueTokensAsync(AppUser user, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var roles = await userManager.GetRolesAsync(user);
        var (access, accessExp) = jwt.CreateAccessToken(user, roles);
        var (raw, entity) = CreateRefreshToken(user.Id, now);
        db.RefreshTokens.Add(entity);

        // Dọn token hết hạn của user để bảng không phình to
        var stale = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.ExpiresAt < now.AddDays(-1))
            .ToListAsync(ct);
        foreach (var t in stale) db.RefreshTokens.Remove(t);

        await db.SaveChangesAsync(ct);
        return new AuthResult(new AuthResponse(access, accessExp, ToDto(user, roles)), raw, entity.ExpiresAt);
    }

    private (string Raw, RefreshToken Entity) CreateRefreshToken(Guid userId, DateTime now)
    {
        var raw = jwt.GenerateRefreshToken();
        return (raw, new RefreshToken
        {
            UserId = userId,
            TokenHash = jwt.HashToken(raw),
            CreatedAt = now,
            ExpiresAt = now.AddDays(jwtOptions.Value.RefreshTokenDays),
            CreatedByIp = currentUser.IpAddress,
        });
    }

    private async Task RevokeAllAsync(Guid userId, string? exceptHash, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.TokenHash != exceptHash)
            .ToListAsync(ct);
        foreach (var t in active)
        {
            t.RevokedAt = now;
            t.RevokedByIp = currentUser.IpAddress;
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SendConfirmationEmailAsync(AppUser user, CancellationToken ct)
    {
        try
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var link = $"{appOptions.Value.ClientUrl.TrimEnd('/')}/xac-nhan-email?userId={user.Id}&token={EncodeToken(token)}";
            await emailSender.SendAsync(user.Email!, "Xác nhận email EduViet",
                $"<p>Chào {WebUtility.HtmlEncode(user.FullName)},</p><p>Cảm ơn bạn đã đăng ký EduViet.</p>" +
                $"<p><a href=\"{link}\">Xác nhận email</a></p>", ct);
        }
        catch (Exception ex)
        {
            // Không chặn đăng ký nếu gửi email lỗi
            logger.LogError(ex, "Gửi email xác nhận thất bại cho {Email}", user.Email);
        }
    }

    private static string EncodeToken(string token) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(token));

    private static string? DecodeToken(string encoded)
    {
        try { return Encoding.UTF8.GetString(Base64Url.DecodeFromChars(encoded)); }
        catch (FormatException) { return null; }
    }

    private static Error IdentityError(IdentityResult result) =>
        Error.Validation(string.Join(" ", result.Errors.Select(e => e.Description)));

    internal static UserDto ToDto(AppUser u, IEnumerable<string> roles) =>
        new(u.Id, u.Email!, u.FullName, u.AvatarUrl, u.PhoneNumber, u.Headline, u.Bio, roles.ToList(), u.CreatedAt);
}
