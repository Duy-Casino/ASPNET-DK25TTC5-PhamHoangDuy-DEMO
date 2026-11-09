using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OnlineLearning.Application.Features.Orders;

namespace OnlineLearning.Infrastructure.BackgroundJobs;

/// <summary>FR-ORDER-09: định kỳ chuyển đơn Pending quá hạn sang Expired.</summary>
public sealed class OrderExpiryService(IServiceScopeFactory scopeFactory, ILogger<OrderExpiryService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
                var expired = await orders.ExpireStaleOrdersAsync(stoppingToken);
                if (expired > 0) logger.LogInformation("Đã hết hạn {Count} đơn hàng chờ thanh toán", expired);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi xử lý đơn hết hạn");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
