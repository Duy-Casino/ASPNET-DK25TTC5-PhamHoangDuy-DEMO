using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using OnlineLearning.Application.Common.Interfaces;

namespace OnlineLearning.API.Common;

/// <summary>
/// Kiểm tra ID token do Google Identity Services cấp: tải khóa công khai từ tài liệu OpenID của Google (có cache),
/// xác minh chữ ký RS256, issuer, audience = Authentication:Google:ClientId và hạn dùng.
/// </summary>
public sealed class GoogleTokenValidator(IConfiguration configuration, ILogger<GoogleTokenValidator> logger) : IGoogleTokenValidator
{
    private static readonly ConfigurationManager<OpenIdConnectConfiguration> Discovery = new(
        "https://accounts.google.com/.well-known/openid-configuration",
        new OpenIdConnectConfigurationRetriever(),
        new HttpDocumentRetriever { RequireHttps = true });

    private string? ClientId => configuration["Authentication:Google:ClientId"] is { Length: > 0 } id ? id : null;

    public bool IsConfigured => ClientId is not null;

    public async Task<ExternalUserInfo?> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        if (ClientId is not { } clientId || string.IsNullOrWhiteSpace(idToken)) return null;
        try
        {
            var config = await Discovery.GetConfigurationAsync(ct);
            var result = await new JsonWebTokenHandler().ValidateTokenAsync(idToken, new TokenValidationParameters
            {
                ValidIssuers = ["https://accounts.google.com", "accounts.google.com"],
                ValidAudience = clientId,
                IssuerSigningKeys = config.SigningKeys,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
            });
            if (!result.IsValid)
            {
                logger.LogWarning(result.Exception, "ID token Google không hợp lệ");
                return null;
            }
            string? Claim(string type) => result.Claims.TryGetValue(type, out var v) ? v?.ToString() : null;
            var email = Claim("email");
            var sub = Claim("sub");
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(sub)) return null;
            var verified = string.Equals(Claim("email_verified"), "true", StringComparison.OrdinalIgnoreCase);
            return new ExternalUserInfo(sub, email, verified, Claim("name"), Claim("picture"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không xác thực được ID token Google");
            return null;
        }
    }
}
