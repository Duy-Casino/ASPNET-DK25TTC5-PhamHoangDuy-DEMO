using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Admin;
using OnlineLearning.Application.Features.Growth;
using OnlineLearning.Application.Features.Learning;
using OnlineLearning.Application.Features.Orders;
using OnlineLearning.Infrastructure.Email;
using OnlineLearning.Infrastructure.Persistence;

namespace OnlineLearning.Infrastructure.Operations;

public sealed class SystemStatusService(
    AppDbContext db, IConfiguration configuration, IHostEnvironment env, IEnumerable<IPaymentGateway> gateways, IFileStorage storage,
    IOptions<SmtpOptions> smtp, JobStatusRegistry jobs, EmailStatusTracker emails,
    IOrderService orders, ICartReminderService cartReminders, IStudyReminderService studyReminders) : ISystemStatusService
{
    private static readonly DateTime StartedAt = DateTime.UtcNow;

    private static readonly (string Key, string Name, string Interval)[] JobList =
    [
        ("order-expiry", "Hủy đơn quá hạn thanh toán", "5 phút"),
        ("cart-reminder", "Email nhắc giỏ hàng", "1 giờ"),
        ("study-reminder", "Email nhắc học", "6 giờ"),
    ];

    public async Task<SystemStatusDto> GetAsync(CancellationToken ct = default)
    {
        var components = new List<ComponentStatusDto>();
        var canConnect = await db.Database.CanConnectAsync(ct);
        var sqliteDemo = DependencyInjection.IsSqliteDemo(configuration);
        var provider = db.Database.ProviderName?.Split('.').Last() ?? "?";
        var pending = 0;
        if (canConnect && !sqliteDemo && db.Database.IsRelational() && !provider.Contains("Sqlite"))
            pending = (await db.Database.GetPendingMigrationsAsync(ct)).Count();
        components.Add(new("Cơ sở dữ liệu", !canConnect ? "error" : pending > 0 ? "warning" : "ok",
            !canConnect ? "Không kết nối được" : $"{provider}{(sqliteDemo ? " (chế độ demo)" : "")}{(pending > 0 ? $" · {pending} migration chưa áp dụng" : "")}"));

        var smtpOk = !string.IsNullOrWhiteSpace(smtp.Value.Host);
        components.Add(new("Email (SMTP)", smtpOk ? "ok" : "warning", smtpOk ? $"{smtp.Value.Host}:{smtp.Value.Port}" : "Chưa cấu hình: email chỉ được ghi vào log, không gửi thật"));

        foreach (var g in gateways)
            components.Add(new($"Thanh toán {g.DisplayName}", !g.IsEnabled ? "off" : g.IsTestMode ? "warning" : "ok",
                !g.IsEnabled ? "Chưa bật (thiếu khóa cấu hình)" : g.IsTestMode ? "Đang bật, chế độ thử nghiệm (không thu tiền thật)" : "Đang hoạt động"));
        components.Add(new("Lưu trữ tệp", "ok", storage.ProviderName));

        var jobsEnabled = configuration.GetValue("BackgroundJobs:Enabled", true);
        var jobDtos = JobList.Select(j =>
        {
            var last = jobs.Get(j.Key);
            return new JobStatusDto(j.Key, j.Name, j.Interval, last?.At, last?.Succeeded, last?.Message, jobsEnabled);
        }).ToList();

        var recent = emails.Recent;
        var since = DateTime.UtcNow.AddHours(-24);
        var counts = new Dictionary<string, int>
        {
            ["users"] = await db.Users.CountAsync(ct),
            ["courses"] = await db.Courses.CountAsync(c => !c.IsDeleted, ct),
            ["enrollments"] = await db.Enrollments.CountAsync(ct),
            ["orders"] = await db.Orders.CountAsync(ct),
            ["notifications"] = await db.Notifications.CountAsync(ct),
            ["auditLogs"] = await db.AdminAuditLogs.CountAsync(ct),
        };
        return new SystemStatusDto(DateTime.UtcNow, env.EnvironmentName, DateTime.UtcNow - StartedAt, components, jobDtos,
            recent.Take(20).ToList(), recent.Count(e => !e.Succeeded && e.At >= since), counts);
    }

    public async Task<Result<string>> RunJobAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var message = key switch
            {
                "order-expiry" => $"Đã chuyển {await orders.ExpireStaleOrdersAsync(ct)} đơn sang hết hạn",
                "cart-reminder" => $"Đã gửi {await cartReminders.SendDueRemindersAsync(ct)} email nhắc giỏ hàng",
                "study-reminder" => $"Đã gửi {await studyReminders.SendDueRemindersAsync(ct)} email nhắc học",
                _ => null,
            };
            if (message is null) return Error.NotFound("Không có công việc này.");
            jobs.Record(key, true, message + " (chạy thủ công)");
            return message;
        }
        catch (Exception ex)
        {
            jobs.Record(key, false, ex.Message);
            return Error.Conflict($"Chạy thất bại: {ex.Message}");
        }
    }
}
