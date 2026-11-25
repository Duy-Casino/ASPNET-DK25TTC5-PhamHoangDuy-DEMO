using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Features.Revenue;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.IntegrationTests;

/// <summary>
/// Vận hành bán hàng: thu nhập và rút tiền của giảng viên, học viên yêu cầu hoàn tiền,
/// email biên nhận, biên nhận PDF, xuất CSV cho Admin.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class RevenueTests(TestApp app)
{
    private const string PaidCourse = "sql-server-cho-lap-trinh-vien";   // 390.000đ

    private async Task<(HttpClient Client, Guid UserId, string Email)> NewStudentAsync(int points = 0)
    {
        var email = $"rev{Guid.NewGuid():N}@test.local";
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var reg = await (await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "Học Viên Doanh Thu", email, password = "Matkhau123" })).OkJsonAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reg.GetProperty("accessToken").GetString());
        var userId = reg.GetProperty("user").GetProperty("id").GetGuid();
        if (points > 0)
        {
            var admin = await AdminAsync();
            (await admin.PostAsJsonAsync("/api/admin/rewards/adjustments", new { userId, points, reason = "Cấp điểm để kiểm thử" })).EnsureSuccessStatusCode();
        }
        return (client, userId, email);
    }

    private Task<HttpClient> AdminAsync() => app.LoginAsync("admin@eduviet.local");

    /// <summary>Mua khóa học qua kênh thử nghiệm; cổng gửi lại IPN hai lần (kiểm tra chống ghi sổ lặp).</summary>
    private async Task<Guid> BuyAsync(HttpClient client, int? points = null)
    {
        var courseId = app.Db(db => db.Courses.First(c => c.Slug == PaidCourse).Id);
        var order = await (await client.PostAsJsonAsync("/api/orders",
            new { courseIds = new[] { courseId }, usePoints = points is not null, points })).OkJsonAsync();
        var orderId = order.GetProperty("id").GetGuid();
        var checkout = await (await client.PostAsJsonAsync($"/api/payments/orders/{orderId}/checkout", new { provider = "Test" })).OkJsonAsync();
        var gateway = checkout.GetProperty("redirectUrl").GetString()!;
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(gateway)).StatusCode);
        await client.GetAsync(gateway);
        return orderId;
    }

    private static async Task<JsonElement> RefundStatusAsync(HttpClient c, Guid orderId) =>
        await (await c.GetAsync($"/api/orders/{orderId}/refund-request")).OkJsonAsync();

    // ---------------------------------------------------------------- thu nhập giảng viên

    [Fact]
    public void Ledger_records_paid_amount_per_course_and_skips_free_items()
    {
        // FinalPrice đã trừ mã giảm giá và điểm phân bổ (tổng = TotalAmount)
        var order = new Order
        {
            Id = Guid.NewGuid(), OrderCode = "EDVTEST", PointsDiscountAmount = 100_001, TotalAmount = 589_999,
            Items =
            {
                new OrderItem { Id = Guid.NewGuid(), InstructorId = Guid.NewGuid(), CourseTitle = "A", Price = 390_000, FinalPrice = 333_473 },
                new OrderItem { Id = Guid.NewGuid(), InstructorId = Guid.NewGuid(), CourseTitle = "B", Price = 300_000, FinalPrice = 256_526 },
                new OrderItem { Id = Guid.NewGuid(), InstructorId = Guid.NewGuid(), CourseTitle = "Miễn phí", FinalPrice = 0 },
            },
        };
        var paidAt = new DateTime(2026, 11, 20, 3, 0, 0, DateTimeKind.Utc);

        var rows = EarningLedger.BuildSales(order, paidAt, 70, 7);

        Assert.Equal(2, rows.Count);                                     // khóa 0đ không ghi sổ
        Assert.Equal(order.TotalAmount, rows.Sum(r => r.GrossAmount));   // tổng khớp số tiền thực thu
        Assert.Equal(233_431m, rows.Single(r => r.CourseTitle == "A").Amount);   // 70% × 333.473 = 233.431,1
        Assert.All(rows, r => Assert.Equal(paidAt.AddDays(7), r.AvailableAt));
    }

    [Fact]
    public async Task Paid_order_records_instructor_earning_once_and_emails_receipt()
    {
        var (client, _, email) = await NewStudentAsync(points: 500);
        var orderId = await BuyAsync(client, points: 500);   // giảm 50.000đ → thực trả 340.000đ

        var rows = app.Db(db => db.InstructorEarnings.Where(e => e.OrderId == orderId).ToList());
        var row = Assert.Single(rows);                                     // IPN gửi lặp không ghi sổ hai lần
        Assert.Equal(EarningType.Sale, row.Type);
        Assert.Equal(340_000m, row.GrossAmount);
        Assert.Equal(70m, row.SharePercent);
        Assert.Equal(238_000m, row.Amount);
        var paidAt = app.Db(db => db.Orders.First(o => o.Id == orderId).PaidAt!.Value);
        Assert.Equal(paidAt.AddDays(7), row.AvailableAt, TimeSpan.FromSeconds(1));

        // Email biên nhận gửi đúng một lần tới người mua
        var orderCode = app.Db(db => db.Orders.First(o => o.Id == orderId).OrderCode);
        var receipt = Assert.Single(app.Emails.All, m => m.To == email && m.Subject.Contains(orderCode));
        Assert.Contains("340.000đ", receipt.Body);

        // Giảng viên thấy dòng thu nhập đang chờ đối soát
        var instructorEmail = app.Db(db => db.Courses.Where(c => c.Slug == PaidCourse).Select(c => c.Instructor.Email).First())!;
        var instructor = await app.LoginAsync(instructorEmail);
        var list = await (await instructor.GetAsync("/api/instructor/earnings?pageSize=50")).OkJsonAsync();
        var mine = list.GetProperty("items").EnumerateArray().Single(e => e.GetProperty("orderCode").GetString() == orderCode);
        Assert.False(mine.GetProperty("isAvailable").GetBoolean());

        // Học viên không xem được trang thu nhập giảng viên
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/instructor/earnings/summary")).StatusCode);
    }

    // ---------------------------------------------------------------- yêu cầu hoàn tiền

    [Fact]
    public async Task Student_refund_request_approved_by_admin_refunds_order_and_reverses_earning()
    {
        var (client, userId, email) = await NewStudentAsync();
        var orderId = await BuyAsync(client);

        var status = await RefundStatusAsync(client, orderId);
        Assert.True(status.GetProperty("canRequest").GetBoolean());
        Assert.Equal(20m, status.GetProperty("maxProgressPercent").GetDecimal());

        // Lý do quá ngắn bị từ chối
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/orders/{orderId}/refund-request", new { reason = "ngắn" })).StatusCode);
        var created = await (await client.PostAsJsonAsync($"/api/orders/{orderId}/refund-request",
            new { reason = "Nội dung khóa học không phù hợp với nhu cầu của tôi." })).OkJsonAsync();
        Assert.Equal("Pending", created.GetProperty("status").GetString());

        // Không gửi trùng khi đang chờ xử lý
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/orders/{orderId}/refund-request",
            new { reason = "Gửi lại lần nữa cho chắc chắn." })).StatusCode);
        Assert.False((await RefundStatusAsync(client, orderId)).GetProperty("canRequest").GetBoolean());

        // Học viên khác không xem được yêu cầu của đơn này
        var (other, _, _) = await NewStudentAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/orders/{orderId}/refund-request")).StatusCode);

        var admin = await AdminAsync();
        var list = await (await admin.GetAsync("/api/admin/refund-requests?status=Pending&pageSize=100")).OkJsonAsync();
        var item = list.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("orderId").GetGuid() == orderId);
        Assert.False(item.GetProperty("hasCertificate").GetBoolean());
        var requestId = item.GetProperty("id").GetGuid();

        (await admin.PostAsJsonAsync($"/api/admin/refund-requests/{requestId}/approve", new { adminNote = "Đồng ý hoàn tiền theo chính sách." }))
            .EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.PostAsJsonAsync($"/api/admin/refund-requests/{requestId}/approve", new { adminNote = (string?)null })).StatusCode);

        // Đơn đã hoàn, quyền học bị thu hồi, thu nhập giảng viên có dòng hoàn âm cùng giá trị
        app.Db(db =>
        {
            Assert.Equal(OrderStatus.Refunded, db.Orders.First(o => o.Id == orderId).Status);
            Assert.Equal(EnrollmentStatus.Revoked, db.Enrollments.First(e => e.OrderId == orderId).Status);
            var rows = db.InstructorEarnings.Where(e => e.OrderId == orderId).ToList();
            Assert.Equal(2, rows.Count);
            Assert.Equal(0m, rows.Sum(r => r.Amount));
            var reversal = rows.Single(r => r.Type == EarningType.Refund);
            Assert.Equal(rows.Single(r => r.Type == EarningType.Sale).AvailableAt, reversal.AvailableAt);
            Assert.Equal(RefundRequestStatus.Approved, db.RefundRequests.First(r => r.Id == requestId).Status);
            return 0;
        });
        Assert.Contains(app.Emails.All, m => m.To == email && m.Subject.Contains("hoàn tiền"));

        var after = await RefundStatusAsync(client, orderId);
        Assert.False(after.GetProperty("canRequest").GetBoolean());
        Assert.Equal("Approved", after.GetProperty("requests")[0].GetProperty("status").GetString());
        _ = userId;
    }

    [Fact]
    public async Task Refund_request_follows_policy_deadline_progress_and_single_rejection()
    {
        // Học quá 20% nội dung → không được hoàn
        var (client, _, _) = await NewStudentAsync();
        var orderId = await BuyAsync(client);
        app.Db(db =>
        {
            db.Enrollments.Where(e => e.OrderId == orderId).ExecuteUpdate(s => s.SetProperty(e => e.ProgressPercent, 35m));
            return 0;
        });
        var blocked = await RefundStatusAsync(client, orderId);
        Assert.False(blocked.GetProperty("canRequest").GetBoolean());
        Assert.Contains("20%", blocked.GetProperty("blockedReason").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/orders/{orderId}/refund-request",
            new { reason = "Tôi muốn hoàn tiền khóa học này." })).StatusCode);

        // Quá 7 ngày kể từ khi thanh toán → không được hoàn
        var (late, _, _) = await NewStudentAsync();
        var lateOrder = await BuyAsync(late);
        app.Clock.Offset = TimeSpan.FromDays(8);
        try
        {
            var s = await RefundStatusAsync(late, lateOrder);
            Assert.False(s.GetProperty("canRequest").GetBoolean());
            Assert.Contains("7 ngày", s.GetProperty("blockedReason").GetString());
        }
        finally { app.Clock.Offset = TimeSpan.Zero; }

        // Admin từ chối (bắt buộc lý do) → học viên không gửi lại được
        var (student, _, _) = await NewStudentAsync();
        var order3 = await BuyAsync(student);
        var req = await (await student.PostAsJsonAsync($"/api/orders/{order3}/refund-request",
            new { reason = "Tôi đã mua nhầm khóa học này." })).OkJsonAsync();
        var admin = await AdminAsync();
        var id = req.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/admin/refund-requests/{id}/reject", new { adminNote = "" })).StatusCode);
        (await admin.PostAsJsonAsync($"/api/admin/refund-requests/{id}/reject", new { adminNote = "Bạn đã tải toàn bộ tài liệu của khóa học." }))
            .EnsureSuccessStatusCode();
        Assert.Equal(OrderStatus.Paid, app.Db(db => db.Orders.First(o => o.Id == order3).Status));
        var s3 = await RefundStatusAsync(student, order3);
        Assert.False(s3.GetProperty("canRequest").GetBoolean());
        Assert.Equal("Rejected", s3.GetProperty("requests")[0].GetProperty("status").GetString());
    }

    // ---------------------------------------------------------------- rút tiền

    [Fact]
    public async Task Instructor_payout_request_is_limited_by_balance_and_processed_by_admin()
    {
        var instructor = await app.LoginAsync("giangvien@eduviet.local");
        var summary = await (await instructor.GetAsync("/api/instructor/earnings/summary")).OkJsonAsync();
        var withdrawable = summary.GetProperty("withdrawableAmount").GetDecimal();
        Assert.True(withdrawable >= 100_000, $"Số dư mẫu cần ≥ 100.000đ, hiện {withdrawable}");
        Assert.Equal(70m, summary.GetProperty("sharePercent").GetDecimal());

        object Body(decimal amount) => new
        {
            amount, bankName = "Vietcombank", bankAccountNumber = "0123456789", bankAccountName = "Nguyen Minh Khoa", note = "Rút tiền tháng 11",
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await instructor.PostAsJsonAsync("/api/instructor/payouts", Body(50_000))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await instructor.PostAsJsonAsync("/api/instructor/payouts", Body(withdrawable + 1000))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await instructor.PostAsJsonAsync("/api/instructor/payouts",
            new { amount = 100_000, bankName = "VCB", bankAccountNumber = "12ab", bankAccountName = "A", note = (string?)null })).StatusCode);

        // Hủy được yêu cầu đang chờ
        var first = await (await instructor.PostAsJsonAsync("/api/instructor/payouts", Body(100_000))).OkJsonAsync();
        (await instructor.PostAsync($"/api/instructor/payouts/{first.GetProperty("id").GetGuid()}/cancel", null)).EnsureSuccessStatusCode();

        var amount = Math.Min(200_000, Math.Floor(withdrawable / 1000) * 1000);
        var payout = await (await instructor.PostAsJsonAsync("/api/instructor/payouts", Body(amount))).OkJsonAsync();
        Assert.Equal("NGUYEN MINH KHOA", payout.GetProperty("bankAccountName").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await instructor.PostAsJsonAsync("/api/instructor/payouts", Body(100_000))).StatusCode);

        var during = await (await instructor.GetAsync("/api/instructor/earnings/summary")).OkJsonAsync();
        Assert.Equal(withdrawable - amount, during.GetProperty("withdrawableAmount").GetDecimal());
        Assert.True(during.GetProperty("hasOpenPayout").GetBoolean());

        var admin = await AdminAsync();
        var list = await (await admin.GetAsync("/api/admin/payouts?status=Requested")).OkJsonAsync();
        var row = list.GetProperty("items").EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == payout.GetProperty("id").GetGuid());
        Assert.Equal(withdrawable - amount, row.GetProperty("instructorWithdrawable").GetDecimal() - amount);

        var id = payout.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/admin/payouts/{id}/paid", new { transferReference = "" })).StatusCode);
        (await admin.PostAsJsonAsync($"/api/admin/payouts/{id}/paid", new { transferReference = "FT26329123456", adminNote = "Đã chuyển" }))
            .EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.PostAsJsonAsync($"/api/admin/payouts/{id}/reject", new { adminNote = "Thử từ chối lại" })).StatusCode);

        var after = await (await instructor.GetAsync("/api/instructor/earnings/summary")).OkJsonAsync();
        Assert.Equal(withdrawable - amount, after.GetProperty("withdrawableAmount").GetDecimal());
        Assert.Equal(summary.GetProperty("paidOutAmount").GetDecimal() + amount, after.GetProperty("paidOutAmount").GetDecimal());
        Assert.False(after.GetProperty("hasOpenPayout").GetBoolean());
        Assert.Equal("Vietcombank", after.GetProperty("lastBank").GetProperty("bankName").GetString());

        // Tổng quan doanh thu: phần nền tảng = tổng thực thu − phần giảng viên
        var overview = await (await admin.GetAsync("/api/admin/revenue")).OkJsonAsync();
        Assert.Equal(overview.GetProperty("grossSales").GetDecimal() - overview.GetProperty("instructorShare").GetDecimal(),
            overview.GetProperty("platformShare").GetDecimal());
        Assert.Contains(overview.GetProperty("instructors").EnumerateArray(), i => i.GetProperty("instructorEmail").GetString() == "giangvien@eduviet.local");

        // Học viên/khách không gọi được API chi trả
        Assert.Equal(HttpStatusCode.Forbidden, (await instructor.GetAsync("/api/admin/payouts")).StatusCode);
    }

    // ---------------------------------------------------------------- biên nhận PDF, CSV

    [Fact]
    public async Task Invoice_pdf_only_for_owner_of_paid_order()
    {
        var (client, _, _) = await NewStudentAsync();
        var orderId = await BuyAsync(client);

        var res = await client.GetAsync($"/api/orders/{orderId}/invoice");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/pdf", res.Content.Headers.ContentType?.MediaType);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));

        var (other, _, _) = await NewStudentAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/orders/{orderId}/invoice")).StatusCode);
        var admin = await AdminAsync();
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/orders/{orderId}/invoice")).StatusCode);

        // Đơn chưa thanh toán không có biên nhận
        var courseId = app.Db(db => db.Courses.First(c => c.Slug == "excel-tu-co-ban-den-nang-cao").Id);
        var pending = await (await other.PostAsJsonAsync("/api/orders", new { courseIds = new[] { courseId } })).OkJsonAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await other.GetAsync($"/api/orders/{pending.GetProperty("id").GetGuid()}/invoice")).StatusCode);
    }

    [Fact]
    public async Task Admin_exports_orders_and_payouts_as_csv()
    {
        var (client, _, _) = await NewStudentAsync();
        var orderId = await BuyAsync(client);
        var code = app.Db(db => db.Orders.First(o => o.Id == orderId).OrderCode);

        var admin = await AdminAsync();
        var today = DateTime.UtcNow.AddHours(7).ToString("yyyy-MM-dd");
        var res = await admin.GetAsync($"/api/admin/orders/export?status=Paid&from={today}&to={today}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/csv", res.Content.Headers.ContentType?.MediaType);
        var csv = await res.Content.ReadAsStringAsync();
        Assert.StartsWith("Mã đơn,", csv.TrimStart('﻿'));
        Assert.Contains(code, csv);
        Assert.Contains("Đã thanh toán", csv);
        Assert.DoesNotContain("Chờ thanh toán", csv);

        // Lọc ngày cũ không có đơn hôm nay
        var old = await (await admin.GetAsync($"/api/admin/orders/export?from=2020-01-01&to=2020-01-02")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(code, old);

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/payouts/export")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/orders/export")).StatusCode);
    }
}
