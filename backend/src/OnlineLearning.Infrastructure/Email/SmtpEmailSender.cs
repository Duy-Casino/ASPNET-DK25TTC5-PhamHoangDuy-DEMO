using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnlineLearning.Application.Common.Interfaces;

namespace OnlineLearning.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>Để trống → không gửi thật, chỉ ghi log (tiện khi phát triển).</summary>
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; }
    public string From { get; set; } = "no-reply@eduviet.local";
    public string FromName { get; set; } = "EduViet";
    public string? UserName { get; set; }
    public string? Password { get; set; }
}

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.Host))
        {
            logger.LogInformation("[EMAIL - chưa cấu hình SMTP] To: {To} | Subject: {Subject}\n{Body}", to, subject, htmlBody);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(o.From, o.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(to);

        using var client = new SmtpClient(o.Host, o.Port) { EnableSsl = o.EnableSsl };
        if (!string.IsNullOrEmpty(o.UserName))
            client.Credentials = new NetworkCredential(o.UserName, o.Password);

        await client.SendMailAsync(message, ct);
    }
}
