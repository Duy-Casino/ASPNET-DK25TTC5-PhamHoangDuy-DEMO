using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OnlineLearning.Application.Features.Growth;
using OnlineLearning.Application.Features.Learning;

namespace OnlineLearning.Infrastructure.BackgroundJobs;

/// <summary>Mỗi giờ gửi email nhắc các giỏ hàng bị bỏ quên (xem <see cref="ICartReminderService"/>).</summary>
public sealed class CartReminderJob(IServiceScopeFactory scopeFactory, ILogger<CartReminderJob> logger, Operations.JobStatusRegistry jobs) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sent = await scope.ServiceProvider.GetRequiredService<ICartReminderService>().SendDueRemindersAsync(stoppingToken);
                jobs.Record("cart-reminder", true, $"Đã gửi {sent} email nhắc giỏ hàng");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi gửi email nhắc giỏ hàng");
                jobs.Record("cart-reminder", false, ex.Message);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

/// <summary>Mỗi 6 giờ gửi email nhắc học viên quay lại khóa học bỏ dở (xem <see cref="IStudyReminderService"/>).</summary>
public sealed class StudyReminderJob(IServiceScopeFactory scopeFactory, ILogger<StudyReminderJob> logger, Operations.JobStatusRegistry jobs) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sent = await scope.ServiceProvider.GetRequiredService<IStudyReminderService>().SendDueRemindersAsync(stoppingToken);
                jobs.Record("study-reminder", true, $"Đã gửi {sent} email nhắc học");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi gửi email nhắc học");
                jobs.Record("study-reminder", false, ex.Message);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
