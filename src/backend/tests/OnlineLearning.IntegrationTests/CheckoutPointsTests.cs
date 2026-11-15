using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OnlineLearning.Application.Features.Orders;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.IntegrationTests;

/// <summary>Dùng điểm thưởng trừ trực tiếp vào giá khi thanh toán (quy tắc checkout_points: 1 điểm = 100đ, tối đa 50%).</summary>
[Collection(nameof(ApiCollection))]
public sealed class CheckoutPointsTests(TestApp app)
{
    private const string PaidCourse = "sql-server-cho-lap-trinh-vien";   // 390.000đ

    private async Task<(HttpClient Client, Guid UserId)> UserWithPointsAsync(int points)
    {
        var email = $"co{Guid.NewGuid():N}@test.local";
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var reg = await (await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "Học Viên Dùng Điểm", email, password = "Matkhau123" })).OkJsonAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reg.GetProperty("accessToken").GetString());
        var userId = reg.GetProperty("user").GetProperty("id").GetGuid();
        if (points > 0)
        {
            var admin = await app.LoginAsync("admin@eduviet.local");
            (await admin.PostAsJsonAsync("/api/admin/rewards/adjustments", new { userId, points, reason = "Cấp điểm để kiểm thử" })).EnsureSuccessStatusCode();
        }
        return (client, userId);
    }

    private Guid CourseId => app.Db(db => db.Courses.First(c => c.Slug == PaidCourse).Id);

    private static async Task<int> BalanceAsync(HttpClient c) =>
        (await (await c.GetAsync("/api/rewards/summary")).OkJsonAsync()).GetProperty("balance").GetInt32();

    private Task<HttpResponseMessage> QuoteAsync(HttpClient c, bool usePoints, int? points = null) =>
        c.PostAsJsonAsync("/api/orders/quote", new { courseIds = new[] { CourseId }, couponCode = (string?)null, usePoints, points });

    private async Task<JsonElement> OrderAsync(HttpClient c, int points) =>
        await (await c.PostAsJsonAsync("/api/orders", new { courseIds = new[] { CourseId }, usePoints = true, points })).OkJsonAsync();

    private async Task<HttpClient> AdminAsync() => await app.LoginAsync("admin@eduviet.local");

    private async Task<(Guid Id, object Body)> CheckoutRuleAsync(HttpClient admin, int threshold, bool active)
    {
        var rules = await (await admin.GetAsync("/api/admin/rewards/rules")).OkJsonAsync();
        var id = rules.EnumerateArray().Single(r => r.GetProperty("key").GetString() == "checkout_points").GetProperty("id").GetGuid();
        return (id, new { name = "Dùng điểm khi thanh toán", points = 1, amountUnit = 100m, threshold, isActive = active, sortOrder = 8 });
    }

    [Fact]
    public async Task Quote_is_optional_capped_by_balance_and_percent_and_never_trusts_client()
    {
        var (client, _) = await UserWithPointsAsync(3000);

        // Không chọn dùng điểm: giá gốc, nhưng vẫn báo số điểm có thể dùng
        var plain = await (await QuoteAsync(client, false)).OkJsonAsync();
        Assert.Equal(390_000m, plain.GetProperty("totalAmount").GetDecimal());
        Assert.Equal(0, plain.GetProperty("pointsUsed").GetInt32());
        var info = plain.GetProperty("points");
        Assert.True(info.GetProperty("enabled").GetBoolean());
        Assert.Equal(3000, info.GetProperty("balance").GetInt32());
        Assert.Equal(1950, info.GetProperty("maxUsable").GetInt32());   // 50% × 390.000đ = 195.000đ = 1.950 điểm

        // Dùng tối đa
        var max = await (await QuoteAsync(client, true)).OkJsonAsync();
        Assert.Equal(1950, max.GetProperty("pointsUsed").GetInt32());
        Assert.Equal(195_000m, max.GetProperty("pointsDiscountAmount").GetDecimal());
        Assert.Equal(195_000m, max.GetProperty("totalAmount").GetDecimal());

        // Chọn số điểm cụ thể
        var some = await (await QuoteAsync(client, true, 500)).OkJsonAsync();
        Assert.Equal(50_000m, some.GetProperty("pointsDiscountAmount").GetDecimal());
        Assert.Equal(340_000m, some.GetProperty("totalAmount").GetDecimal());
        Assert.Equal(340_000m, some.GetProperty("lines").EnumerateArray().Sum(l => l.GetProperty("finalPrice").GetDecimal()));

        // Vượt mức tối đa hoặc số điểm không hợp lệ
        Assert.Equal(HttpStatusCode.BadRequest, (await QuoteAsync(client, true, 2000)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await QuoteAsync(client, true, -5)).StatusCode);

        // Người chưa có điểm: dùng tối đa = 0 điểm, đặt cụ thể thì bị từ chối
        var (poor, _) = await UserWithPointsAsync(0);
        Assert.Equal(0, (await (await QuoteAsync(poor, true)).OkJsonAsync()).GetProperty("pointsUsed").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await QuoteAsync(poor, true, 10)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await poor.PostAsJsonAsync("/api/orders",
            new { courseIds = new[] { CourseId }, usePoints = true, points = 10 })).StatusCode);
    }

    [Fact]
    public async Task Order_deducts_points_and_cancel_restores_them()
    {
        var (client, userId) = await UserWithPointsAsync(3000);
        var order = await OrderAsync(client, 500);
        var orderId = order.GetProperty("id").GetGuid();
        Assert.Equal(500, order.GetProperty("pointsUsed").GetInt32());
        Assert.Equal(50_000m, order.GetProperty("pointsDiscountAmount").GetDecimal());
        Assert.Equal(340_000m, order.GetProperty("totalAmount").GetDecimal());
        Assert.Equal(340_000m, order.GetProperty("items").EnumerateArray().Sum(i => i.GetProperty("finalPrice").GetDecimal()));
        Assert.Equal(2500, await BalanceAsync(client));

        // Hủy đơn chờ thanh toán: hoàn đủ điểm, "tổng đã dùng" trở về 0
        (await client.PostAsync($"/api/orders/{orderId}/cancel", null)).EnsureSuccessStatusCode();
        Assert.Equal(3000, await BalanceAsync(client));
        Assert.Equal(0, app.Db(db => db.UserPoints.First(p => p.UserId == userId).TotalSpent));
        Assert.Equal(PointTransactionType.Restore,
            app.Db(db => db.PointTransactions.First(t => t.ReferenceKey == $"checkout-restore:{orderId}").Type));
        // Hủy lần hai không hoàn thêm
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/orders/{orderId}/cancel", null)).StatusCode);
        Assert.Equal(3000, await BalanceAsync(client));
    }

    [Fact]
    public async Task Paid_order_earns_points_on_cash_amount_only_and_refund_returns_used_points()
    {
        var (client, _) = await UserWithPointsAsync(3000);
        var order = await OrderAsync(client, 1000);                       // giảm 100.000đ → trả 290.000đ
        var orderId = order.GetProperty("id").GetGuid();
        var checkout = await (await client.PostAsJsonAsync($"/api/payments/orders/{orderId}/checkout", new { provider = "Test" })).OkJsonAsync();
        var gateway = checkout.GetProperty("redirectUrl").GetString()!;
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(gateway)).StatusCode);
        await client.GetAsync(gateway);                                   // IPN lặp

        Assert.Equal("Paid", app.Db(db => db.Orders.First(o => o.Id == orderId).Status.ToString()));
        Assert.Equal(3000 - 1000 + 29, await BalanceAsync(client));       // điểm mua hàng tính trên 290.000đ tiền thật

        var admin = await AdminAsync();
        (await admin.PostAsJsonAsync($"/api/admin/orders/{orderId}/refund", new { reason = "Học viên yêu cầu trong 7 ngày" })).EnsureSuccessStatusCode();
        Assert.Equal(3000, await BalanceAsync(client));                   // thu hồi 29 điểm mua hàng, hoàn 1.000 điểm đã dùng
    }

    [Fact]
    public async Task Expired_order_restores_points_and_late_ipn_takes_them_again()
    {
        var (client, userId) = await UserWithPointsAsync(3000);
        var order = await OrderAsync(client, 800);
        var orderId = order.GetProperty("id").GetGuid();
        var total = order.GetProperty("totalAmount").GetDecimal();
        Assert.Equal(2200, await BalanceAsync(client));

        try
        {
            app.Clock.Offset = TimeSpan.FromDays(2);
            using (var scope = app.Services.CreateScope())
                Assert.True(await scope.ServiceProvider.GetRequiredService<IOrderService>().ExpireStaleOrdersAsync() >= 1);
            Assert.Equal("Expired", app.Db(db => db.Orders.First(o => o.Id == orderId).Status.ToString()));
            Assert.Equal(3000, await BalanceAsync(client));

            // IPN hợp lệ đến muộn: đơn vẫn được ghi nhận, điểm của đơn bị trừ lại
            using (var scope = app.Services.CreateScope())
            {
                var paid = await scope.ServiceProvider.GetRequiredService<IOrderFulfillmentService>()
                    .MarkPaidAsync(orderId, new PaymentConfirmation(PaymentProvider.Test, $"LATE-{orderId:N}", null, total, null, "00", null));
                Assert.True(paid.IsSuccess);
            }
            Assert.Equal(3000 - 800 + 31, await BalanceAsync(client));    // 310.000đ → 31 điểm
            Assert.Equal(1, app.Db(db => db.PointTransactions.Count(t => t.UserId == userId && t.ReferenceKey == $"checkout-repay:{orderId}")));
        }
        finally
        {
            app.Clock.Offset = TimeSpan.Zero;
        }
    }

    [Fact]
    public async Task Admin_configures_rule_and_full_points_payment_completes_order_without_gateway()
    {
        var admin = await AdminAsync();
        var (id, _) = await CheckoutRuleAsync(admin, 50, true);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/admin/rewards/rules/{id}",
            new { name = "Dùng điểm khi thanh toán", points = 1, amountUnit = 100m, threshold = 150, isActive = true, sortOrder = 8 })).StatusCode);

        var (client, _) = await UserWithPointsAsync(5000);
        try
        {
            // Tắt tính năng: không dùng được điểm
            (await admin.PutAsJsonAsync($"/api/admin/rewards/rules/{id}", (await CheckoutRuleAsync(admin, 50, false)).Body)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest, (await QuoteAsync(client, true)).StatusCode);
            Assert.False((await (await QuoteAsync(client, false)).OkJsonAsync()).GetProperty("points").GetProperty("enabled").GetBoolean());

            // Cho trả 100% bằng điểm: đơn 0đ hoàn tất ngay, không qua cổng thanh toán
            (await admin.PutAsJsonAsync($"/api/admin/rewards/rules/{id}", (await CheckoutRuleAsync(admin, 100, true)).Body)).EnsureSuccessStatusCode();
            var order = await OrderAsync(client, 3900);
            Assert.Equal(0m, order.GetProperty("totalAmount").GetDecimal());
            Assert.Equal("Paid", order.GetProperty("status").GetString());
            Assert.Equal(1100, await BalanceAsync(client));                // không cộng điểm mua hàng cho đơn 0đ
        }
        finally
        {
            (await admin.PutAsJsonAsync($"/api/admin/rewards/rules/{id}", (await CheckoutRuleAsync(admin, 50, true)).Body)).EnsureSuccessStatusCode();
        }
    }
}
