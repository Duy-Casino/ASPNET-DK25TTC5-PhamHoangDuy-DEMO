using System.Collections.Concurrent;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Features.Admin;
using OnlineLearning.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace OnlineLearning.Infrastructure.Operations;

/// <summary>Lần chạy gần nhất của từng công việc nền (giữ trong bộ nhớ, hiển thị ở trang Trạng thái hệ thống).</summary>
public sealed class JobStatusRegistry
{
    public sealed record Entry(DateTime At, bool Succeeded, string Message);
    private readonly ConcurrentDictionary<string, Entry> _last = new();
    public void Record(string key, bool succeeded, string message) => _last[key] = new Entry(DateTime.UtcNow, succeeded, message);
    public Entry? Get(string key) => _last.TryGetValue(key, out var e) ? e : null;
}

/// <summary>Nhật ký 200 email gần nhất (thành công/lỗi) để Admin phát hiện SMTP gặp sự cố.</summary>
public sealed class EmailStatusTracker
{
    private readonly ConcurrentQueue<EmailEventDto> _events = new();
    public void Add(EmailEventDto e)
    {
        _events.Enqueue(e);
        while (_events.Count > 200 && _events.TryDequeue(out _)) { }
    }
    public IReadOnlyList<EmailEventDto> Recent => [.. _events.Reverse()];
}

/// <summary>Bọc bộ gửi email để ghi nhận kết quả từng lần gửi (che bớt địa chỉ người nhận).</summary>
public sealed class TrackingEmailSender(SmtpEmailSender inner, EmailStatusTracker tracker, IOptions<SmtpOptions> smtp) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var delivered = !string.IsNullOrWhiteSpace(smtp.Value.Host);
        try
        {
            await inner.SendAsync(to, subject, htmlBody, ct);
            tracker.Add(new EmailEventDto(DateTime.UtcNow, Mask(to), subject, true, null, delivered));
        }
        catch (Exception ex)
        {
            tracker.Add(new EmailEventDto(DateTime.UtcNow, Mask(to), subject, false, ex.Message, false));
            throw;
        }
    }

    private static string Mask(string email)
    {
        var at = email.IndexOf('@');
        return at <= 2 ? email : email[..2] + new string('*', at - 2) + email[at..];
    }
}
