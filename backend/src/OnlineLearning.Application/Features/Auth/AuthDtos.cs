using FluentValidation;

namespace OnlineLearning.Application.Features.Auth;

public sealed record RegisterRequest(string FullName, string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
public sealed record ConfirmEmailRequest(Guid UserId, string Token);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record UpdateProfileRequest(string FullName, string? PhoneNumber, string? Headline, string? Bio, string? AvatarUrl);

public sealed record UserDto(
    Guid Id, string Email, string FullName, string? AvatarUrl, string? PhoneNumber,
    string? Headline, string? Bio, IReadOnlyList<string> Roles, DateTime CreatedAt);

public sealed record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAt, UserDto User);

/// <summary>Kết quả nội bộ: refresh token gốc chỉ được đặt vào cookie HttpOnly, không trả trong body.</summary>
public sealed record AuthResult(AuthResponse Response, string RefreshToken, DateTime RefreshTokenExpiresAt);

internal static class PasswordRules
{
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Vui lòng nhập mật khẩu.")
            .MinimumLength(8).WithMessage("Mật khẩu tối thiểu 8 ký tự.")
            .MaximumLength(100)
            .Matches("[A-Z]").WithMessage("Mật khẩu cần ít nhất một chữ hoa.")
            .Matches("[a-z]").WithMessage("Mật khẩu cần ít nhất một chữ thường.")
            .Matches("[0-9]").WithMessage("Mật khẩu cần ít nhất một chữ số.");
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Vui lòng nhập họ tên.").MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Email không hợp lệ.").MaximumLength(256);
        RuleFor(x => x.Password).StrongPassword();
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Email không hợp lệ.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Vui lòng nhập mật khẩu.");
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator() =>
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Email không hợp lệ.");
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).StrongPassword();
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Vui lòng nhập mật khẩu hiện tại.");
        RuleFor(x => x.NewPassword).StrongPassword()
            .NotEqual(x => x.CurrentPassword).WithMessage("Mật khẩu mới phải khác mật khẩu hiện tại.");
    }
}

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Vui lòng nhập họ tên.").MaximumLength(150);
        RuleFor(x => x.PhoneNumber).MaximumLength(20).Matches(@"^[0-9+\-\s]*$").WithMessage("Số điện thoại không hợp lệ.");
        RuleFor(x => x.Headline).MaximumLength(200);
        RuleFor(x => x.Bio).MaximumLength(4000);
        RuleFor(x => x.AvatarUrl).MaximumLength(500)
            .Must(Courses.SaveCourseRequestValidator.BeHttpsOrNull)
            .WithMessage("Ảnh đại diện phải là URL https hoặc ảnh đã tải lên.");
    }
}
