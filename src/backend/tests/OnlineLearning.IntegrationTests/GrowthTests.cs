using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OnlineLearning.Application.Features.Growth;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.IntegrationTests;

/// <summary>Tăng doanh số: yêu thích, gợi ý khóa học, flash sale, combo, mua tặng, nhắc giỏ hàng.</summary>
[Collection(nameof(ApiCollection))]
public sealed class GrowthTests(TestApp app)
{
    private const string SqlCourse = "sql-server-cho-lap-trinh-vien";   // 390.000đ (giá gốc 690.000đ)

    private async Task<(HttpClient Client, Guid UserId, string Email)> NewUserAsync(string? email = null)
    {
        email ??= $"gr{Guid.NewGuid():N}@test.local";
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var reg = await (await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "Học Viên Tăng Trưởng", email, password = "Matkhau123" })).OkJsonAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reg.GetProperty("accessToken").GetString());
        return (client, reg.GetProperty("user").GetProperty("id").GetGuid(), email);
    }

    private Guid CourseId(string slug) => app.Db(db => db.Courses.First(c => c.Slug == slug).Id);
    private Guid CourseIdByTitle(string title) => app.Db(db => db.Courses.First(c => c.Title == title).Id);

    /// <summary>Thanh toán đơn qua kênh thử nghiệm (đơn có phí).</summary>
    private static async Task PayAsync(HttpClient client, Guid orderId)
    {
        var checkout = await (await client.PostAsJsonAsync($"/api/payments/orders/{orderId}/checkout", new { provider = "Test" })).OkJsonAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(checkout.GetProperty("redirectUrl").GetString()!)).StatusCode);
    }

    // ---------------------------------------------------------------- yêu thích

    [Fact]
    public async Task Wishlist_add_list_remove_is_idempotent_and_only_for_published_courses()
    {
        var (client, _, _) = await NewUserAsync();
        var id = CourseId(SqlCourse);

        (await client.PostAsync($"/api/wishlist/{id}", null)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/wishlist/{id}", null)).EnsureSuccessStatusCode();   // bấm lại không lỗi, không trùng
        var ids = await (await client.GetAsync("/api/wishlist/ids")).OkJsonAsync();
        Assert.Equal([id], ids.EnumerateArray().Select(x => x.GetGuid()));

        var list = await (await client.GetAsync("/api/wishlist")).OkJsonAsync();
        Assert.Equal(SqlCourse, list[0].GetProperty("course").GetProperty("slug").GetString());

        var pending = app.Db(db => db.Courses.First(c => c.Status == CourseStatus.Pending).Id);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/wishlist/{pending}", null)).StatusCode);

        (await client.DeleteAsync($"/api/wishlist/{id}")).EnsureSuccessStatusCode();
        Assert.Equal(0, (await (await client.GetAsync("/api/wishlist/ids")).OkJsonAsync()).GetArrayLength());
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Anonymous().GetAsync("/api/wishlist")).StatusCode);
    }

    // ---------------------------------------------------------------- gợi ý

    [Fact]
    public async Task Related_courses_exclude_the_course_and_owned_ones_and_cart_gets_suggestions()
    {
        var anon = await (await app.Anonymous().GetAsync($"/api/courses/{SqlCourse}/related?take=4")).OkJsonAsync();
        Assert.InRange(anon.GetArrayLength(), 1, 4);
        Assert.DoesNotContain(anon.EnumerateArray(), r => r.GetProperty("course").GetProperty("slug").GetString() == SqlCourse);
        Assert.All(anon.EnumerateArray(), r => Assert.Contains(r.GetProperty("reason").GetString(),
            new[] { RecommendationService.AlsoBought, RecommendationService.SameTopic }));

        // Khóa đã có trong giỏ không được gợi ý lại; giỏ hàng có gợi ý riêng
        var (client, _, _) = await NewUserAsync();
        var first = anon[0].GetProperty("course");
        (await client.PostAsJsonAsync("/api/cart/items", new { courseId = first.GetProperty("id").GetGuid() })).EnsureSuccessStatusCode();
        var mine = await (await client.GetAsync($"/api/courses/{SqlCourse}/related")).OkJsonAsync();
        Assert.DoesNotContain(mine.EnumerateArray(), r => r.GetProperty("course").GetProperty("id").GetGuid() == first.GetProperty("id").GetGuid());
        var suggestions = await (await client.GetAsync("/api/cart/suggestions")).OkJsonAsync();
        Assert.True(suggestions.GetArrayLength() > 0);
        Assert.DoesNotContain(suggestions.EnumerateArray(), r => r.GetProperty("course").GetProperty("id").GetGuid() == first.GetProperty("id").GetGuid());
    }

    // ---------------------------------------------------------------- flash sale

    [Fact]
    public async Task Flash_sale_price_applies_until_end_time_then_reverts()
    {
        var instructor = await app.LoginAsync("giangvien@eduviet.local");
        var courseId = CourseIdByTitle("ReactJS và TypeScript cho người mới");   // 990.000đ, không khuyến mãi
        var (student, _, _) = await NewUserAsync();
        async Task<decimal> QuoteAsync() =>
            (await (await student.PostAsJsonAsync("/api/orders/quote", new { courseIds = new[] { courseId } })).OkJsonAsync()).GetProperty("totalAmount").GetDecimal();

        // Kiểm tra dữ liệu: giá phải nhỏ hơn giá gốc, hạn tối đa 90 ngày
        Assert.Equal(HttpStatusCode.BadRequest, (await instructor.PutAsJsonAsync($"/api/instructor/courses/{courseId}/promotion",
            new { discountPrice = 990_000, discountEndsAt = DateTime.UtcNow.AddDays(1) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await instructor.PutAsJsonAsync($"/api/instructor/courses/{courseId}/promotion",
            new { discountPrice = 500_000, discountEndsAt = DateTime.UtcNow.AddDays(100) })).StatusCode);
        // Giảng viên khác không đặt được
        var other = await app.LoginAsync("lethu.design@eduviet.local");
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/instructor/courses/{courseId}/promotion",
            new { discountPrice = 500_000, discountEndsAt = DateTime.UtcNow.AddHours(2) })).StatusCode);

        var ends = DateTime.UtcNow.AddHours(2);
        var saved = await (await instructor.PutAsJsonAsync($"/api/instructor/courses/{courseId}/promotion",
            new { discountPrice = 500_000, discountEndsAt = ends })).OkJsonAsync();
        Assert.Equal(500_000m, saved.GetProperty("discountPrice").GetDecimal());
        try
        {
            var detail = await (await app.Anonymous().GetAsync("/api/courses/reactjs-va-typescript-cho-nguoi-moi")).OkJsonAsync();
            Assert.Equal(500_000m, detail.GetProperty("effectivePrice").GetDecimal());
            Assert.Equal(ends, detail.GetProperty("discountEndsAt").GetDateTime(), TimeSpan.FromSeconds(1));
            Assert.Equal(500_000m, await QuoteAsync());

            // Hết hạn: máy chủ tính lại giá gốc dù khuyến mãi chưa bị xóa
            app.Clock.Offset = TimeSpan.FromHours(3);
            Assert.Equal(990_000m, await QuoteAsync());
        }
        finally
        {
            app.Clock.Offset = TimeSpan.Zero;
            var cleared = await (await instructor.PutAsJsonAsync($"/api/instructor/courses/{courseId}/promotion",
                new { discountPrice = (decimal?)null, discountEndsAt = (DateTime?)null })).OkJsonAsync();
            Assert.Equal(JsonValueKind.Null, cleared.GetProperty("discountPrice").ValueKind);
        }
        Assert.Equal(990_000m, await QuoteAsync());
    }

    // ---------------------------------------------------------------- combo

    [Fact]
    public async Task Admin_creates_bundle_and_student_buys_it_with_price_split_across_courses()
    {
        var admin = await app.LoginAsync("admin@eduviet.local");
        var figma = CourseIdByTitle("Thiết kế UI/UX với Figma");          // giá gốc 1.190.000đ
        var photoshop = CourseIdByTitle("Photoshop cơ bản cho người đi làm"); // giá gốc 590.000đ

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/bundles",
            new { title = "Combo đắt", description = (string?)null, thumbnailUrl = (string?)null, price = 1_780_000, isActive = true, courseIds = new[] { figma, photoshop } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/bundles",
            new { title = "Một khóa", description = (string?)null, thumbnailUrl = (string?)null, price = 100_000, isActive = true, courseIds = new[] { figma } })).StatusCode);

        var bundle = await (await admin.PostAsJsonAsync("/api/admin/bundles",
            new { title = "Thiết kế cho người mới", description = "Figma và Photoshop", thumbnailUrl = (string?)null, price = 1_000_000, isActive = true, courseIds = new[] { figma, photoshop } })).OkJsonAsync();
        var bundleId = bundle.GetProperty("id").GetGuid();
        var slug = bundle.GetProperty("slug").GetString()!;
        Assert.StartsWith("combo-", slug);

        var publicList = await (await app.Anonymous().GetAsync($"/api/bundles?courseId={figma}")).OkJsonAsync();
        var listed = publicList.EnumerateArray().Single(b => b.GetProperty("id").GetGuid() == bundleId);
        Assert.Equal(1_780_000m, listed.GetProperty("originalTotal").GetDecimal());
        Assert.Equal(43, listed.GetProperty("savingPercent").GetInt32());

        var (student, _, _) = await NewUserAsync();
        var quote = await (await student.PostAsJsonAsync("/api/orders/quote", new { bundleId })).OkJsonAsync();
        Assert.Equal(1_000_000m, quote.GetProperty("totalAmount").GetDecimal());
        var order = await (await student.PostAsJsonAsync("/api/orders", new { fromCart = false, bundleId })).OkJsonAsync();
        var orderId = order.GetProperty("id").GetGuid();
        Assert.Equal("Thiết kế cho người mới", order.GetProperty("bundleTitle").GetString());
        var items = order.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(1_000_000m, items.Sum(i => i.GetProperty("finalPrice").GetDecimal()));
        Assert.Equal(668_539m, items.Single(i => i.GetProperty("courseId").GetGuid() == figma).GetProperty("price").GetDecimal()); // 1.000.000 × 1.190/1.780

        await PayAsync(student, orderId);
        app.Db(db =>
        {
            Assert.Equal(bundleId, db.Orders.First(o => o.Id == orderId).BundleId);
            Assert.Equal(2, db.Enrollments.Count(e => e.OrderId == orderId));
            Assert.Equal(1_000_000m, db.InstructorEarnings.Where(e => e.OrderId == orderId).Sum(e => e.GrossAmount));
            return 0;
        });

        // Đã sở hữu thì không mua lại combo; combo đã có đơn không xóa được, chỉ ẩn
        var again = await student.PostAsJsonAsync("/api/orders/quote", new { bundleId });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("Combo chỉ bán trọn bộ", await again.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/admin/bundles/{bundleId}")).StatusCode);
        (await admin.PutAsJsonAsync($"/api/admin/bundles/{bundleId}",
            new { title = "Thiết kế cho người mới", description = "Figma và Photoshop", thumbnailUrl = (string?)null, price = 1_000_000, isActive = false, courseIds = new[] { figma, photoshop } })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await app.Anonymous().GetAsync($"/api/bundles/{slug}")).StatusCode);
    }

    // ---------------------------------------------------------------- mua tặng

    [Fact]
    public async Task Gift_order_does_not_enroll_buyer_and_recipient_redeems_with_matching_email()
    {
        var (buyer, buyerId, buyerEmail) = await NewUserAsync();
        var courseId = CourseId(SqlCourse);
        var recipientEmail = $"qua{Guid.NewGuid():N}@test.local";

        Assert.Equal(HttpStatusCode.BadRequest, (await buyer.PostAsJsonAsync("/api/orders", new
        {
            courseIds = new[] { courseId }, fromCart = false, gift = new { recipientEmail = buyerEmail, recipientName = "Chính tôi", message = (string?)null },
        })).StatusCode);

        var order = await (await buyer.PostAsJsonAsync("/api/orders", new
        {
            courseIds = new[] { courseId }, fromCart = false,
            gift = new { recipientEmail, recipientName = "Bạn Thân", message = "Chúc bạn học tốt!" },
        })).OkJsonAsync();
        var orderId = order.GetProperty("id").GetGuid();
        Assert.Equal("Pending", order.GetProperty("gift").GetProperty("status").GetString());
        await PayAsync(buyer, orderId);

        // Người mua không được ghi danh; người nhận nhận email có mã
        Assert.Equal(0, app.Db(db => db.Enrollments.Count(e => e.UserId == buyerId && e.CourseId == courseId)));
        var paid = await (await buyer.GetAsync($"/api/orders/{orderId}")).OkJsonAsync();
        Assert.Equal("Sent", paid.GetProperty("gift").GetProperty("status").GetString());
        var mail = Assert.Single(app.Emails.All, m => m.To == recipientEmail);
        Assert.Contains("Chúc bạn học tốt!", mail.Body);
        var code = app.Db(db => db.CourseGifts.First(g => g.OrderId == orderId).Code);
        Assert.Contains(code, mail.Body);

        var anon = await (await app.Anonymous().GetAsync($"/api/gifts/{code}")).OkJsonAsync();
        Assert.False(anon.GetProperty("canRedeem").GetBoolean());
        Assert.Contains("SQL Server", anon.GetProperty("courses")[0].GetProperty("title").GetString());
        Assert.DoesNotContain(recipientEmail, anon.GetProperty("recipientEmailMasked").GetString());

        // Người khác đăng nhập không nhận được
        var (stranger, _, _) = await NewUserAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await stranger.PostAsync($"/api/gifts/{code}/redeem", null)).StatusCode);

        var (recipient, recipientId, _) = await NewUserAsync(recipientEmail);
        var view = await (await recipient.GetAsync($"/api/gifts/{code}")).OkJsonAsync();
        Assert.True(view.GetProperty("canRedeem").GetBoolean());
        var redeemed = await (await recipient.PostAsync($"/api/gifts/{code}/redeem", null)).OkJsonAsync();
        Assert.Equal(1, redeemed.GetProperty("enrolledCount").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await recipient.PostAsync($"/api/gifts/{code}/redeem", null)).StatusCode);
        Assert.Equal(1, app.Db(db => db.Enrollments.Count(e => e.UserId == recipientId && e.CourseId == courseId && e.OrderId == orderId)));

        // Hoàn tiền đơn quà đã nhận: thu hồi quyền học của người nhận
        var admin = await app.LoginAsync("admin@eduviet.local");
        (await admin.PostAsJsonAsync($"/api/admin/orders/{orderId}/refund", new { reason = "Người tặng yêu cầu hoàn tiền" })).EnsureSuccessStatusCode();
        Assert.Equal(EnrollmentStatus.Revoked, app.Db(db => db.Enrollments.First(e => e.UserId == recipientId && e.OrderId == orderId).Status));
    }

    // ---------------------------------------------------------------- nhắc giỏ hàng

    [Fact]
    public async Task Abandoned_cart_reminder_is_sent_once_per_cart_change_after_delay()
    {
        var (client, _, email) = await NewUserAsync();
        (await client.PostAsJsonAsync("/api/cart/items", new { courseId = CourseId(SqlCourse) })).EnsureSuccessStatusCode();
        int Sent() => app.Emails.All.Count(m => m.To == email && m.Subject.Contains("giỏ hàng"));

        async Task RunAsync()
        {
            using var scope = app.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ICartReminderService>().SendDueRemindersAsync();
        }

        try
        {
            await RunAsync();
            Assert.Equal(0, Sent());                    // chưa đủ 24 giờ

            app.Clock.Offset = TimeSpan.FromHours(25);
            await RunAsync();
            Assert.Equal(1, Sent());
            Assert.Contains("SQL Server cho lập trình viên", app.Emails.All.Last(m => m.To == email).Body);
            await RunAsync();
            Assert.Equal(1, Sent());                    // không nhắc lại khi giỏ không đổi

            // Thêm khóa mới → sau 24 giờ nữa được nhắc lần hai
            (await client.PostAsJsonAsync("/api/cart/items", new { courseId = CourseIdByTitle("Digital Marketing tổng quan") })).EnsureSuccessStatusCode();
            app.Clock.Offset = TimeSpan.FromHours(50);
            await RunAsync();
            Assert.Equal(2, Sent());
        }
        finally { app.Clock.Offset = TimeSpan.Zero; }
    }
}
