using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OnlineLearning.API.Common;
using OnlineLearning.Application.Features.Auth;

namespace OnlineLearning.API.Controllers;

[Route("api/auth")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController(IAuthService auth, IWebHostEnvironment env) : ApiControllerBase
{
    public const string RefreshCookieName = "eduviet_rt";

    /// <summary>Đăng ký tài khoản học viên.</summary>
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct) =>
        WithRefreshCookie(await auth.RegisterAsync(request, ct));

    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct) =>
        WithRefreshCookie(await auth.LoginAsync(request, ct));

    /// <summary>Cấp access token mới bằng refresh token trong cookie HttpOnly (xoay vòng token).</summary>
    [HttpPost("refresh")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var result = await auth.RefreshAsync(Request.Cookies[RefreshCookieName], ct);
        if (result.IsFailure) Response.Cookies.Delete(RefreshCookieName, CookieOptions(DateTimeOffset.UnixEpoch));
        return WithRefreshCookie(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(Request.Cookies[RefreshCookieName], ct);
        Response.Cookies.Delete(RefreshCookieName, CookieOptions(DateTimeOffset.UnixEpoch));
        return NoContent();
    }

    /// <summary>Luôn trả 204 để không lộ email nào đã đăng ký.</summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await auth.ForgotPasswordAsync(request, ct);
        return NoContent();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct) =>
        FromResult(await auth.ResetPasswordAsync(request, ct));

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken ct) =>
        FromResult(await auth.ConfirmEmailAsync(request, ct));

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken ct) =>
        FromResult(await auth.GetProfileAsync(User.GetUserId(), ct));

    private IActionResult WithRefreshCookie(Application.Common.Models.Result<AuthResult> result)
    {
        if (result.IsFailure) return Problem(result.Error!);
        var value = result.Value!;
        Response.Cookies.Append(RefreshCookieName, value.RefreshToken, CookieOptions(value.RefreshTokenExpiresAt));
        return Ok(value.Response);
    }

    private CookieOptions CookieOptions(DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = !env.IsEnvironment("Testing"),
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        Expires = expires,
        IsEssential = true,
    };
}

[Route("api/account")]
[Authorize]
public sealed class AccountController(IAuthService auth) : ApiControllerBase
{
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken ct) =>
        FromResult(await auth.GetProfileAsync(User.GetUserId(), ct));

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken ct) =>
        FromResult(await auth.UpdateProfileAsync(User.GetUserId(), request, ct));

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct) =>
        FromResult(await auth.ChangePasswordAsync(User.GetUserId(), request, Request.Cookies[AuthController.RefreshCookieName], ct));
}
