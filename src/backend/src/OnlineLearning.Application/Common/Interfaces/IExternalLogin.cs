namespace OnlineLearning.Application.Common.Interfaces;

/// <summary>Thông tin người dùng lấy từ ID token của nhà cung cấp đăng nhập ngoài (Google).</summary>
public sealed record ExternalUserInfo(string Subject, string Email, bool EmailVerified, string? FullName, string? PictureUrl);

/// <summary>Xác thực ID token Google (chữ ký, issuer, audience = Client ID, hạn dùng).</summary>
public interface IGoogleTokenValidator
{
    bool IsConfigured { get; }
    Task<ExternalUserInfo?> ValidateAsync(string idToken, CancellationToken ct = default);
}
