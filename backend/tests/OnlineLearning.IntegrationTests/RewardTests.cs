using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OnlineLearning.Application.Features.Rewards;
using OnlineLearning.Domain.Entities;

namespace OnlineLearning.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class RewardTests(TestApp app)
{
    private const string PaidCourse = "sql-server-cho-lap-trinh-vien";          // 390.000đ
    private const string FreeCourse = "ky-nang-thuyet-trinh-chuyen-nghiep";

    private async Task<(HttpClient Client, Guid UserId)> NewUserAsync(string? referralCode = null, string? ip = null)
    {
        var email = $"pts{Guid.NewGuid():N}@test.local";
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        if (ip is not null) client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        var reg = await (await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "Học Viên Điểm", email, password = "Matkhau123", referralCode })).OkJsonAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reg.GetProperty("accessToken").GetString());
        return (client, reg.GetProperty("user").GetProperty("id").GetGuid());
    }

    private static async Task<JsonElement> SummaryAsync(HttpClient c) => await (await c.GetAsync("/api/rewards/summary")).OkJsonAsync();
    private static async Task<int> BalanceAsync(HttpClient c) => (await SummaryAsync(c)).GetProperty("balance").GetInt32();

    private async Task<Guid> BuyWithTestGatewayAsync(HttpClient client, string slug)
    {
        var courseId = app.Db(db => db.Courses.First(c => c.Slug == slug).Id);
        var order = await (await client.PostAsJsonAsync("/api/orders", new { courseIds = new[] { courseId } })).OkJsonAsync();
        var orderId = order.GetProperty("id").GetGuid();
        var checkout = await (await client.PostAsJsonAsync($"/api/payments/orders/{orderId}/checkout", new { provider = "Test" })).OkJsonAsync();
        var gateway = checkout.GetProperty("redirectUrl").GetString()!;
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(gateway)).StatusCode);
        // Cổng gửi lại IPN (replay) → không cộng điểm lần hai
        await client.GetAsync(gateway);
        return orderId;
    }

    [Fact]
    public void Purchase_points_round_down_to_whole_points()
    {
        var rule = new PointRule { Points = 10, AmountUnit = 100_000 };
        Assert.Equal(24, RewardService.PurchasePoints(249_000, rule));
        Assert.Equal(39, RewardService.PurchasePoints(390_000, rule));
        Assert.Equal(0, RewardService.PurchasePoints(9_999, rule));
        Assert.Equal(0, RewardService.PurchasePoints(0, rule));
    }

    [Fact]
    public async Task Purchase_and_referral_award_once_after_ipn_and_refund_revokes_both()
    {
        var (referrer, referrerId) = await NewUserAsync();
        var code = (await SummaryAsync(referrer)).GetProperty("referral").GetProperty("code").GetString()!;
        Assert.Matches("^[2-9A-Z]{8}$", code);

        var (buyer, buyerId) = await NewUserAsync(code);
        Assert.True(app.Db(db => db.Referrals.Any(r => r.ReferrerId == referrerId && r.ReferredUserId == buyerId)));

        var orderId = await BuyWithTestGatewayAsync(buyer, PaidCourse);
        Assert.Equal(39, await BalanceAsync(buyer));          // 390.000đ → 39 điểm, IPN lặp không cộng thêm
        Assert.Equal(50, await BalanceAsync(referrer));       // thưởng giới thiệu
        Assert.Equal(1, app.Db(db => db.PointTransactions.Count(t => t.UserId == buyerId && t.ReferenceKey == $"purchase:{orderId}")));

        var tx = await (await buyer.GetAsync("/api/rewards/transactions")).OkJsonAsync();
        Assert.Equal("purchase", tx.GetProperty("items")[0].GetProperty("ruleKey").GetString());

        // Hoàn tiền: bắt buộc lý do, chỉ Admin
        var admin = await app.LoginAsync("admin@eduviet.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.PostAsJsonAsync($"/api/admin/orders/{orderId}/refund", new { reason = "Muốn hoàn tiền" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/admin/orders/{orderId}/refund", new { reason = "" })).StatusCode);
        (await admin.PostAsJsonAsync($"/api/admin/orders/{orderId}/refund", new { reason = "Học viên yêu cầu trong 7 ngày" })).EnsureSuccessStatusCode();

        Assert.Equal(0, await BalanceAsync(buyer));
        Assert.Equal(0, await BalanceAsync(referrer));
        Assert.Equal("Refunded", app.Db(db => db.Orders.First(o => o.Id == orderId).Status.ToString()));
        Assert.Equal("Revoked", app.Db(db => db.Enrollments.First(e => e.OrderId == orderId).Status.ToString()));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/admin/orders/{orderId}/refund", new { reason = "Hoàn lần hai" })).StatusCode);

        // Tự giới thiệu / mã sai bị bỏ qua, không làm hỏng đăng ký
        var (_, strangerId) = await NewUserAsync("SAIMAXXX");
        Assert.False(app.Db(db => db.Referrals.Any(r => r.ReferredUserId == strangerId)));
    }

    [Fact]
    public async Task Daily_check_in_once_per_day_with_streak_bonus_and_reset_after_missing_a_day()
    {
        var (client, _) = await NewUserAsync();
        try
        {
            var first = await (await client.PostAsync("/api/rewards/check-in", null)).OkJsonAsync();
            Assert.Equal(5, first.GetProperty("pointsAwarded").GetInt32());
            Assert.Equal(1, first.GetProperty("streak").GetInt32());
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/api/rewards/check-in", null)).StatusCode);

            // Đồng thời hai yêu cầu trong cùng ngày mới: chỉ một được cộng
            app.Clock.Offset = TimeSpan.FromDays(1);
            var parallel = await Task.WhenAll(client.PostAsync("/api/rewards/check-in", null), client.PostAsync("/api/rewards/check-in", null));
            Assert.Single(parallel, r => r.IsSuccessStatusCode);

            JsonElement last = default;
            for (var d = 2; d <= 6; d++)
            {
                app.Clock.Offset = TimeSpan.FromDays(d);
                last = await (await client.PostAsync("/api/rewards/check-in", null)).OkJsonAsync();
            }
            Assert.Equal(7, last.GetProperty("streak").GetInt32());
            Assert.Equal(30, last.GetProperty("streakBonus").GetInt32());
            Assert.Equal(7 * 5 + 30, await BalanceAsync(client));

            // Bỏ lỡ ngày thứ 8 → chuỗi bắt đầu lại
            app.Clock.Offset = TimeSpan.FromDays(8);
            var reset = await (await client.PostAsync("/api/rewards/check-in", null)).OkJsonAsync();
            Assert.Equal(1, reset.GetProperty("streak").GetInt32());
            Assert.Equal(0, reset.GetProperty("streakBonus").GetInt32());
        }
        finally
        {
            app.Clock.Offset = TimeSpan.Zero;
        }
    }

    [Fact]
    public async Task Share_rewards_only_when_someone_else_opens_and_at_most_once_per_day()
    {
        var (sharer, sharerId) = await NewUserAsync(ip: "10.1.0.1");
        var courseId = app.Db(db => db.Courses.First(c => c.Slug == PaidCourse).Id);
        var share = await (await sharer.PostAsJsonAsync("/api/rewards/shares", new { courseId, channel = "Facebook" })).OkJsonAsync();
        var token = share.GetProperty("token").GetString()!;
        Assert.Equal(PaidCourse, share.GetProperty("courseSlug").GetString());
        // Tạo lại trong ngày dùng lại cùng liên kết
        var again = await (await sharer.PostAsJsonAsync("/api/rewards/shares", new { courseId, channel = "Facebook" })).OkJsonAsync();
        Assert.Equal(token, again.GetProperty("token").GetString());

        HttpClient Visitor(string ip, string ua)
        {
            var c = app.Anonymous();
            c.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
            c.DefaultRequestHeaders.UserAgent.ParseAdd(ua);
            return c;
        }

        // Chính người chia sẻ mở (cùng tài khoản, hoặc ẩn danh cùng IP) → không thưởng
        (await sharer.PostAsync($"/api/rewards/shares/{token}/open", null)).EnsureSuccessStatusCode();
        (await Visitor("10.1.0.1", "Incognito/1.0").PostAsync($"/api/rewards/shares/{token}/open", null)).EnsureSuccessStatusCode();
        Assert.Equal(0, await BalanceAsync(sharer));

        // Người khác mở → +5; mở lặp và người thứ ba trong cùng ngày không cộng thêm
        var friend = Visitor("10.1.0.2", "Friend/1.0");
        (await friend.PostAsync($"/api/rewards/shares/{token}/open", null)).EnsureSuccessStatusCode();
        (await friend.PostAsync($"/api/rewards/shares/{token}/open", null)).EnsureSuccessStatusCode();
        (await Visitor("10.1.0.3", "Other/1.0").PostAsync($"/api/rewards/shares/{token}/open", null)).EnsureSuccessStatusCode();
        Assert.Equal(5, await BalanceAsync(sharer));
        Assert.Equal(2, app.Db(db => db.CourseShares.First(s => s.Token == token).OpenCount));

        // Token giả không gây lỗi, không cộng điểm
        Assert.Equal(HttpStatusCode.NoContent, (await app.Anonymous().PostAsync("/api/rewards/shares/KHONGTONTAI/open", null)).StatusCode);
        Assert.True((await SummaryAsync(sharer)).GetProperty("tasks").EnumerateArray()
            .Single(t => t.GetProperty("key").GetString() == "course_share").GetProperty("doneToday").GetBoolean());
        Assert.Equal(1, app.Db(db => db.PointTransactions.Count(t => t.UserId == sharerId && t.RuleKey == "course_share")));
    }

    [Fact]
    public async Task Completing_course_awards_once_and_only_from_real_progress()
    {
        var (client, userId) = await NewUserAsync();
        var courseId = app.Db(db => db.Courses.First(c => c.Slug == FreeCourse).Id);
        (await client.PostAsJsonAsync("/api/orders", new { courseIds = new[] { courseId } })).EnsureSuccessStatusCode();
        Assert.Equal(0, await BalanceAsync(client));   // khóa 0đ không có điểm mua hàng

        var course = await (await client.GetAsync($"/api/learning/courses/{FreeCourse}")).OkJsonAsync();
        var quizId = course.GetProperty("courseQuizzes")[0].GetProperty("id").GetGuid();
        var take = await (await client.PostAsync($"/api/learning/quizzes/{quizId}/attempts", null)).OkJsonAsync();
        var correct = app.Db(db => db.AnswerOptions.Where(o => o.Question.QuizId == quizId && o.IsCorrect).Select(o => new { o.QuestionId, o.Id }).ToList());
        await client.PostAsJsonAsync($"/api/learning/attempts/{take.GetProperty("attemptId").GetGuid()}/submit",
            new { answers = correct.Select(c => new { questionId = c.QuestionId, optionIds = new[] { c.Id } }) });

        var lessons = course.GetProperty("sections").EnumerateArray().SelectMany(s => s.GetProperty("lessons").EnumerateArray())
            .Select(l => l.GetProperty("id").GetGuid()).ToList();
        foreach (var id in lessons.Take(lessons.Count - 1)) await client.PostAsync($"/api/learning/lessons/{id}/complete", null);
        Assert.Equal(0, await BalanceAsync(client));   // chưa xong 100% → chưa có điểm
        await client.PostAsync($"/api/learning/lessons/{lessons[^1]}/complete", null);
        Assert.Equal(100, await BalanceAsync(client));

        // Bỏ đánh dấu rồi hoàn thành lại không cộng lần hai
        await client.DeleteAsync($"/api/learning/lessons/{lessons[^1]}/complete");
        await client.PostAsync($"/api/learning/lessons/{lessons[^1]}/complete", null);
        Assert.Equal(100, await BalanceAsync(client));
        Assert.Equal(1, app.Db(db => db.PointTransactions.Count(t => t.UserId == userId && t.RuleKey == "course_completion")));
    }

    [Fact]
    public async Task Redeem_creates_personal_voucher_and_balance_never_goes_negative()
    {
        var (client, userId) = await NewUserAsync();
        var admin = await app.LoginAsync("admin@eduviet.local");

        // Điều chỉnh thủ công: bắt buộc lý do, không trừ quá số dư, chỉ Admin
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/rewards/adjustments", new { userId, points = 300, reason = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/rewards/adjustments", new { userId, points = -10, reason = "Trừ thử khi chưa có điểm" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/rewards/adjustments", new { userId, points = 999, reason = "Tự cộng điểm cho mình" })).StatusCode);
        var adj = await (await admin.PostAsJsonAsync("/api/admin/rewards/adjustments", new { userId, points = 300, reason = "Thưởng sự kiện tháng 11" })).OkJsonAsync();
        Assert.Equal(300, adj.GetProperty("balanceAfter").GetInt32());
        Assert.Equal("Quản trị viên", adj.GetProperty("createdByName").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/rewards/redeem", new { points = 250 })).StatusCode); // không phải bội số 100
        var voucher = await (await client.PostAsJsonAsync("/api/rewards/redeem", new { points = 200 })).OkJsonAsync();
        Assert.Equal(20_000m, voucher.GetProperty("voucherValue").GetDecimal());
        var code = voucher.GetProperty("couponCode").GetString()!;
        Assert.Equal(100, await BalanceAsync(client));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/rewards/redeem", new { points = 500 })).StatusCode);
        Assert.Equal(100, await BalanceAsync(client));

        var summary = await SummaryAsync(client);
        Assert.Equal(300, summary.GetProperty("totalEarned").GetInt32());
        Assert.Equal(200, summary.GetProperty("totalSpent").GetInt32());

        // Mã chỉ chủ sở hữu dùng được
        var courseId = app.Db(db => db.Courses.First(c => c.Slug == PaidCourse).Id);
        var quote = await (await client.PostAsJsonAsync("/api/orders/quote", new { courseIds = new[] { courseId }, couponCode = code })).OkJsonAsync();
        Assert.Equal(20_000m, quote.GetProperty("discountAmount").GetDecimal());
        var (other, _) = await NewUserAsync();
        var otherQuote = await (await other.PostAsJsonAsync("/api/orders/quote", new { courseIds = new[] { courseId }, couponCode = code })).JsonAsync();
        Assert.NotEqual(20_000m, otherQuote.TryGetProperty("discountAmount", out var d) ? d.GetDecimal() : -1m);
    }

    [Fact]
    public async Task Admin_manages_rules_sees_overview_balances_and_exports_csv()
    {
        var admin = await app.LoginAsync("admin@eduviet.local");
        var rules = await (await admin.GetAsync("/api/admin/rewards/rules")).OkJsonAsync();
        Assert.True(rules.GetArrayLength() >= 7);
        var share = rules.EnumerateArray().Single(r => r.GetProperty("key").GetString() == "course_share");
        var shareId = share.GetProperty("id").GetGuid();

        // Không xóa được nhiệm vụ hệ thống; tắt được
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/admin/rewards/rules/{shareId}")).StatusCode);
        var off = await (await admin.PutAsJsonAsync($"/api/admin/rewards/rules/{shareId}",
            new { name = "Chia sẻ khóa học", description = (string?)null, points = 8, amountUnit = (decimal?)null, threshold = (int?)null, isActive = false, sortOrder = 5 })).OkJsonAsync();
        Assert.False(off.GetProperty("isActive").GetBoolean());
        Assert.Equal(8, off.GetProperty("points").GetInt32());
        (await admin.PutAsJsonAsync($"/api/admin/rewards/rules/{shareId}",
            new { name = "Chia sẻ khóa học", points = 5, isActive = true, sortOrder = 5 })).EnsureSuccessStatusCode();

        // Quy tắc mua hàng bắt buộc có đơn vị tiền
        var purchaseId = rules.EnumerateArray().Single(r => r.GetProperty("key").GetString() == "purchase").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/admin/rewards/rules/{purchaseId}",
            new { name = "Mua khóa học", points = 10, isActive = true, sortOrder = 1 })).StatusCode);

        // Nhiệm vụ tùy chỉnh: thêm rồi xóa được
        var custom = await (await admin.PostAsJsonAsync("/api/admin/rewards/rules",
            new { key = "su_kien_thang_11", name = "Sự kiện tháng 11", points = 20, isActive = true, sortOrder = 20 })).OkJsonAsync();
        Assert.False(custom.GetProperty("isSystem").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/rewards/rules",
            new { key = "purchase", name = "Trùng", points = 1, isActive = true, sortOrder = 1 })).StatusCode);
        (await admin.DeleteAsync($"/api/admin/rewards/rules/{custom.GetProperty("id").GetGuid()}")).EnsureSuccessStatusCode();

        var (student, studentId) = await NewUserAsync();
        await (await admin.PostAsJsonAsync("/api/admin/rewards/adjustments", new { userId = studentId, points = 42, reason = "Kiểm tra báo cáo" })).OkJsonAsync();

        var overview = await (await admin.GetAsync("/api/admin/rewards/overview")).OkJsonAsync();
        Assert.True(overview.GetProperty("totalIssued").GetInt32() >= 42);
        Assert.True(overview.GetProperty("outstandingBalance").GetInt32() >= 42);
        var balances = await (await admin.GetAsync($"/api/admin/rewards/balances?keyword=pts")).OkJsonAsync();
        Assert.Contains(balances.GetProperty("items").EnumerateArray(), b => b.GetProperty("userId").GetGuid() == studentId);
        var history = await (await admin.GetAsync($"/api/admin/rewards/transactions?userId={studentId}")).OkJsonAsync();
        Assert.Equal(42, history.GetProperty("items")[0].GetProperty("points").GetInt32());

        var csv = await admin.GetAsync($"/api/admin/rewards/transactions/export?userId={studentId}");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        var text = await csv.Content.ReadAsStringAsync();
        Assert.Contains("Kiểm tra báo cáo", text);

        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/admin/rewards/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Anonymous().GetAsync("/api/rewards/summary")).StatusCode);
    }
}
