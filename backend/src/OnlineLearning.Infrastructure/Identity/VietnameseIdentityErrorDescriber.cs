using Microsoft.AspNetCore.Identity;

namespace OnlineLearning.Infrastructure.Identity;

/// <summary>Thông báo lỗi Identity bằng tiếng Việt.</summary>
public sealed class VietnameseIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DuplicateEmail(string email) => new() { Code = nameof(DuplicateEmail), Description = "Email đã được sử dụng." };
    public override IdentityError DuplicateUserName(string userName) => new() { Code = nameof(DuplicateUserName), Description = "Email đã được sử dụng." };
    public override IdentityError InvalidEmail(string? email) => new() { Code = nameof(InvalidEmail), Description = "Email không hợp lệ." };
    public override IdentityError PasswordMismatch() => new() { Code = nameof(PasswordMismatch), Description = "Mật khẩu không đúng." };
    public override IdentityError InvalidToken() => new() { Code = nameof(InvalidToken), Description = "Mã xác thực không hợp lệ hoặc đã hết hạn." };
    public override IdentityError PasswordTooShort(int length) => new() { Code = nameof(PasswordTooShort), Description = $"Mật khẩu tối thiểu {length} ký tự." };
    public override IdentityError PasswordRequiresDigit() => new() { Code = nameof(PasswordRequiresDigit), Description = "Mật khẩu cần ít nhất một chữ số." };
    public override IdentityError PasswordRequiresLower() => new() { Code = nameof(PasswordRequiresLower), Description = "Mật khẩu cần ít nhất một chữ thường." };
    public override IdentityError PasswordRequiresUpper() => new() { Code = nameof(PasswordRequiresUpper), Description = "Mật khẩu cần ít nhất một chữ hoa." };
    public override IdentityError UserAlreadyInRole(string role) => new() { Code = nameof(UserAlreadyInRole), Description = $"Người dùng đã có vai trò {role}." };
    public override IdentityError UserLockoutNotEnabled() => new() { Code = nameof(UserLockoutNotEnabled), Description = "Tài khoản không hỗ trợ khóa." };
}
