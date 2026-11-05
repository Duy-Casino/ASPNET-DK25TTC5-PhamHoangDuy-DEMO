namespace OnlineLearning.Application.Common.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "EduViet.API";
    public string Audience { get; set; } = "EduViet.Client";
    public string SecretKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>URL Frontend, dùng để tạo link trong email (đặt lại mật khẩu, xác nhận email).</summary>
    public string ClientUrl { get; set; } = "http://localhost:5173";
    /// <summary>Số phút một đơn Pending còn hiệu lực.</summary>
    public int OrderExpiryMinutes { get; set; } = 30;
}
