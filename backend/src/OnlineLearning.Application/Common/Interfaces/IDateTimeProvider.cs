using OnlineLearning.Domain.Entities;

namespace OnlineLearning.Application.Common.Interfaces;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}

public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
    string? IpAddress { get; }
}

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) CreateAccessToken(AppUser user, IEnumerable<string> roles);
    string GenerateRefreshToken();
    string HashToken(string token);
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
