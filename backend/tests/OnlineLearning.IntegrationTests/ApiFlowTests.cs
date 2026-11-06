using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class CatalogTests(TestApp app)
{
    [Fact]
    public async Task Home_returns_seeded_sections()
    {
        var home = await (await app.Anonymous().GetAsync("/api/home")).OkJsonAsync();

        Assert.True(home.GetProperty("featured").GetArrayLength() > 0);
        Assert.True(home.GetProperty("newest").GetArrayLength() > 0);
        Assert.True(home.GetProperty("bestSelling").GetArrayLength() > 0);
        Assert.True(home.GetProperty("categories").GetArrayLength() >= 8);
        Assert.True(home.GetProperty("featuredInstructors").GetArrayLength() > 0);
        Assert.True(home.GetProperty("testimonials").GetArrayLength() > 0);
        Assert.True(home.GetProperty("stats").GetProperty("totalCourses").GetInt32() >= 10);
    }

    [Fact]
    public async Task Sitemap_lists_static_pages_and_only_published_courses()
    {
        var res = await app.Anonymous().GetAsync("/sitemap.xml");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/xml", res.Content.Headers.ContentType?.MediaType);

        var doc = System.Xml.Linq.XDocument.Parse(await res.Content.ReadAsStringAsync());
        System.Xml.Linq.XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var locs = doc.Descendants(ns + "loc").Select(l => l.Value).ToList();

        Assert.Contains(locs, l => l.EndsWith("/khoa-hoc"));
        Assert.Contains(locs, l => l.EndsWith("/chinh-sach/hoan-tien"));
        Assert.Contains(locs, l => l.EndsWith("/khoa-hoc/excel-tu-co-ban-den-nang-cao"));
        Assert.Contains(locs, l => l.Contains("/giang-vien/"));
        // Khóa học chưa xuất bản (Pending) không lộ ra sitemap
        Assert.DoesNotContain(locs, l => l.Contains("docker", StringComparison.OrdinalIgnoreCase));
        Assert.All(locs, l => Assert.StartsWith("http", l));
    }

    [Fact]
    public async Task Search_filters_by_category_keyword_and_sorts_by_price()
    {
        var client = app.Anonymous();

        var byCategory = await (await client.GetAsync("/api/courses?category=lap-trinh&pageSize=50")).OkJsonAsync();
        Assert.All(byCategory.GetProperty("items").EnumerateArray(),
            c => Assert.Equal("lap-trinh", c.GetProperty("category").GetProperty("slug").GetString()));

        var byKeyword = await (await client.GetAsync("/api/courses?keyword=Excel")).OkJsonAsync();
        Assert.Contains(byKeyword.GetProperty("items").EnumerateArray(), c => c.GetProperty("title").GetString()!.Contains("Excel"));

        var sorted = await (await client.GetAsync("/api/courses?sort=price_asc&pageSize=50")).OkJsonAsync();
        var prices = sorted.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("effectivePrice").GetDecimal()).ToList();
        Assert.Equal(prices.OrderBy(p => p), prices);

        // Khóa học Pending không xuất hiện công khai
        var all = await (await client.GetAsync("/api/courses?keyword=Docker")).OkJsonAsync();
        Assert.Equal(0, all.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Course_detail_only_exposes_preview_videos()
    {
        var detail = await (await app.Anonymous().GetAsync("/api/courses/lap-trinh-c-tu-co-ban-den-nang-cao")).OkJsonAsync();

        var lessons = detail.GetProperty("sections").EnumerateArray().SelectMany(s => s.GetProperty("lessons").EnumerateArray()).ToList();
        Assert.NotEmpty(lessons);
        foreach (var l in lessons)
        {
            var isPreview = l.GetProperty("isPreview").GetBoolean();
            Assert.Equal(isPreview, l.GetProperty("previewVideoUrl").ValueKind == JsonValueKind.String);
        }
        Assert.False(detail.GetProperty("isEnrolled").GetBoolean());
        Assert.Equal(5, detail.GetProperty("ratingDistribution").EnumerateObject().Count());
    }

    [Fact]
    public async Task Pending_course_detail_is_hidden_from_public()
    {
        var res = await app.Anonymous().GetAsync("/api/courses/docker-va-kubernetes-cho-developer");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}

[Collection(nameof(ApiCollection))]
public sealed class AuthTests(TestApp app)
{
    [Fact]
    public async Task Register_login_refresh_rotation_and_reuse_detection()
    {
        var client = app.Anonymous();
        var email = $"user{Guid.NewGuid():N}@test.local";

        var reg = await (await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "Người Dùng Thử", email, password = "Matkhau123" })).OkJsonAsync();
        Assert.Contains("Student", reg.GetProperty("user").GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.False(reg.TryGetProperty("refreshToken", out _)); // refresh token chỉ nằm trong cookie HttpOnly

        // Refresh lần 1 thành công và xoay vòng token
        var cookieBefore = app.Db(db => db.RefreshTokens.Count(t => t.User.Email == email));
        var r1 = await client.PostAsync("/api/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(cookieBefore + 1, app.Db(db => db.RefreshTokens.Count(t => t.User.Email == email)));

        // Token cũ đã bị thu hồi, chỉ còn đúng một token hoạt động; token lưu dạng hash
        Assert.Equal(1, app.Db(db => db.RefreshTokens.Count(t => t.User.Email == email && t.RevokedAt != null && t.ReplacedByTokenHash != null)));
        Assert.Equal(1, app.Db(db => db.RefreshTokens.Count(t => t.User.Email == email && t.RevokedAt == null)));
        Assert.All(app.Db(db => db.RefreshTokens.Where(t => t.User.Email == email).Select(t => t.TokenHash).ToList()),
            h => Assert.Equal(64, h.Length));

        // Đăng xuất thu hồi token hiện tại
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);
    }

    [Fact]
    public async Task Reused_refresh_token_revokes_all_sessions()
    {
        // Mô phỏng kẻ tấn công giữ token cũ: tự gửi cookie cũ sau khi token đã xoay vòng
        var email = $"reuse{Guid.NewGuid():N}@test.local";
        var c1 = app.Anonymous();
        var reg = await c1.PostAsJsonAsync("/api/auth/register", new { fullName = "Reuse", email, password = "Matkhau123" });
        var oldCookie = reg.Headers.GetValues("Set-Cookie").First().Split(';')[0];

        Assert.Equal(HttpStatusCode.OK, (await c1.PostAsync("/api/auth/refresh", null)).StatusCode);

        var attacker = app.CreateClient(new() { HandleCookies = false });
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        req.Headers.Add("Cookie", oldCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await attacker.SendAsync(req)).StatusCode);

        Assert.Equal(0, app.Db(db => db.RefreshTokens.Count(t => t.User.Email == email && t.RevokedAt == null)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await c1.PostAsync("/api/auth/refresh", null)).StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401_and_validation_returns_400()
    {
        var client = app.Anonymous();
        var wrong = await client.PostAsJsonAsync("/api/auth/login", new { email = "hocvien@eduviet.local", password = "SaiMatKhau1" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        var invalid = await client.PostAsJsonAsync("/api/auth/register", new { fullName = "", email = "khong-hop-le", password = "123" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var problem = await invalid.JsonAsync();
        Assert.True(problem.GetProperty("errors").EnumerateObject().Count() >= 3);
    }

    [Fact]
    public async Task Profile_update_and_change_password()
    {
        var email = $"profile{Guid.NewGuid():N}@test.local";
        var client = app.Anonymous();
        var reg = await (await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Hồ Sơ", email, password = "Matkhau123" })).OkJsonAsync();
        client.DefaultRequestHeaders.Authorization = new("Bearer", reg.GetProperty("accessToken").GetString());

        var updated = await (await client.PutAsJsonAsync("/api/account/profile",
            new { fullName = "Hồ Sơ Mới", phoneNumber = "0901234567", headline = "Sinh viên", bio = "Xin chào" })).OkJsonAsync();
        Assert.Equal("Hồ Sơ Mới", updated.GetProperty("fullName").GetString());

        var wrongCurrent = await client.PostAsJsonAsync("/api/account/change-password", new { currentPassword = "Sai123456", newPassword = "Matkhau456" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongCurrent.StatusCode);

        var ok = await client.PostAsJsonAsync("/api/account/change-password", new { currentPassword = "Matkhau123", newPassword = "Matkhau456" });
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        await app.LoginAsync(email, "Matkhau456");
    }

    [Fact]
    public async Task Protected_endpoints_require_token_and_role()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Anonymous().GetAsync("/api/cart")).StatusCode);

        var student = await app.LoginAsync("hoangnam@eduviet.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/admin/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/instructor/courses")).StatusCode);
    }
}

[Collection(nameof(ApiCollection))]
public sealed class CommerceTests(TestApp app)
{
    private Guid CourseId(string slug) => app.Db(db => db.Courses.First(c => c.Slug == slug).Id);

    [Fact]
    public async Task Cart_order_with_coupon_uses_server_side_prices()
    {
        var email = $"buyer{Guid.NewGuid():N}@test.local";
        var client = app.Anonymous();
        var reg = await (await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Người Mua", email, password = "Matkhau123" })).OkJsonAsync();
        client.DefaultRequestHeaders.Authorization = new("Bearer", reg.GetProperty("accessToken").GetString());

        var csharp = CourseId("lap-trinh-c-tu-co-ban-den-nang-cao");          // 499.000đ
        var figma = CourseId("thiet-ke-ui-ux-voi-figma");                     // 690.000đ
        (await client.PostAsJsonAsync("/api/cart/items", new { courseId = csharp })).EnsureSuccessStatusCode();
        var cart = await (await client.PostAsJsonAsync("/api/cart/items", new { courseId = figma })).OkJsonAsync();
        Assert.Equal(2, cart.GetProperty("count").GetInt32());
        Assert.Equal(1_189_000m, cart.GetProperty("subTotal").GetDecimal());

        // Thêm trùng → 409
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/cart/items", new { courseId = figma })).StatusCode);

        // Báo giá với GIAM50K
        var quote = await (await client.PostAsJsonAsync("/api/orders/quote",
            new { courseIds = new[] { csharp, figma }, couponCode = "giam50k" })).OkJsonAsync();
        Assert.Equal(50_000m, quote.GetProperty("discountAmount").GetDecimal());
        Assert.Equal(1_139_000m, quote.GetProperty("totalAmount").GetDecimal());

        // Client cố gửi giá giả: bị bỏ qua vì DTO không có trường giá
        var order = await (await client.PostAsJsonAsync("/api/orders",
            new { fromCart = true, couponCode = "GIAM50K", totalAmount = 1000, price = 1 })).OkJsonAsync();
        Assert.Equal("Pending", order.GetProperty("status").GetString());
        Assert.Equal(1_139_000m, order.GetProperty("totalAmount").GetDecimal());
        var items = order.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(50_000m, items.Sum(i => i.GetProperty("discountAllocated").GetDecimal()));
        Assert.Equal(1_139_000m, items.Sum(i => i.GetProperty("finalPrice").GetDecimal()));

        // Chưa thanh toán → chưa có quyền học
        Assert.Equal(0, app.Db(db => db.Enrollments.Count(e => e.User.Email == email)));

        // Hủy đơn
        var orderId = order.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/orders/{orderId}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/orders/{orderId}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task Free_order_grants_enrollment_and_blocks_duplicate_purchase_then_review()
    {
        var email = $"free{Guid.NewGuid():N}@test.local";
        var client = app.Anonymous();
        var reg = await (await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Học Viên Miễn Phí", email, password = "Matkhau123" })).OkJsonAsync();
        client.DefaultRequestHeaders.Authorization = new("Bearer", reg.GetProperty("accessToken").GetString());

        var excel = CourseId("excel-tu-co-ban-den-nang-cao");
        (await client.PostAsJsonAsync("/api/cart/items", new { courseId = excel })).EnsureSuccessStatusCode();
        var enrollmentsBefore = app.Db(db => db.Courses.First(c => c.Id == excel).EnrollmentCount);

        var order = await (await client.PostAsJsonAsync("/api/orders",
            new { courseIds = new[] { excel }, couponCode = "MIENPHI100" })).OkJsonAsync();
        Assert.Equal("Paid", order.GetProperty("status").GetString());
        Assert.Equal(0m, order.GetProperty("totalAmount").GetDecimal());
        Assert.Equal("Free", order.GetProperty("payments")[0].GetProperty("provider").GetString());

        Assert.Equal(1, app.Db(db => db.Enrollments.Count(e => e.User.Email == email && e.CourseId == excel)));
        Assert.Equal(enrollmentsBefore + 1, app.Db(db => db.Courses.First(c => c.Id == excel).EnrollmentCount));
        Assert.Equal(1, app.Db(db => db.Coupons.First(c => c.Code == "MIENPHI100").UsedCount));

        // Giỏ hàng tự loại khóa đã mua
        var cart = await (await client.GetAsync("/api/cart")).OkJsonAsync();
        Assert.Equal(0, cart.GetProperty("count").GetInt32());

        // Không cho mua trùng
        var again = await client.PostAsJsonAsync("/api/orders", new { courseIds = new[] { excel } });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/cart/items", new { courseId = excel })).StatusCode);

        // Chi tiết khóa học biết đã sở hữu
        var detail = await (await client.GetAsync("/api/courses/excel-tu-co-ban-den-nang-cao")).OkJsonAsync();
        Assert.True(detail.GetProperty("isEnrolled").GetBoolean());

        // Đánh giá: được một lần
        var review = await client.PostAsJsonAsync($"/api/courses/{excel}/reviews", new { rating = 5, comment = "Rất hữu ích cho công việc hằng ngày." });
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/courses/{excel}/reviews", new { rating = 4, comment = "Lần hai" })).StatusCode);

        // Có thông báo thanh toán thành công
        var notes = await (await client.GetAsync("/api/notifications")).OkJsonAsync();
        Assert.Contains(notes.GetProperty("items").EnumerateArray(), n => n.GetProperty("type").GetString() == "Order");
    }

    [Fact]
    public async Task Cannot_review_without_enrollment_or_buy_own_course()
    {
        var student = await app.LoginAsync("ducthinh@eduviet.local");
        var powerBi = CourseId("power-bi-phan-tich-du-lieu-kinh-doanh");
        var review = await student.PostAsJsonAsync($"/api/courses/{powerBi}/reviews", new { rating = 1, comment = "Spam" });
        Assert.True(review.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Conflict);

        var instructor = await app.LoginAsync("phamquang.data@eduviet.local");
        var own = await instructor.PostAsJsonAsync("/api/cart/items", new { courseId = powerBi });
        Assert.Equal(HttpStatusCode.Conflict, own.StatusCode);
    }

    [Fact]
    public async Task Invalid_coupon_is_rejected()
    {
        var email = $"coupon{Guid.NewGuid():N}@test.local";
        await app.Anonymous().PostAsJsonAsync("/api/auth/register", new { fullName = "Thử Mã", email, password = "Matkhau123" });
        var client = await app.LoginAsync(email, "Matkhau123");
        var figma = CourseId("photoshop-co-ban-cho-nguoi-di-lam");
        var res = await client.PostAsJsonAsync("/api/orders/quote", new { courseIds = new[] { figma }, couponCode = "KHONGTONTAI" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        // GIAM50K yêu cầu đơn tối thiểu 300.000đ, Photoshop chỉ 299.000đ
        var min = await client.PostAsJsonAsync("/api/orders/quote", new { courseIds = new[] { figma }, couponCode = "GIAM50K" });
        Assert.Equal(HttpStatusCode.BadRequest, min.StatusCode);
    }
}

[Collection(nameof(ApiCollection))]
public sealed class InstructorAndAdminTests(TestApp app)
{
    [Fact]
    public async Task Instructor_creates_course_submits_and_admin_approves()
    {
        var instructor = await app.LoginAsync("giangvien@eduviet.local");
        var categoryId = app.Db(db => db.Categories.First(c => c.Slug == "lap-trinh").Id);

        var title = $"Node.js căn bản {Guid.NewGuid():N}"[..30];
        var created = await (await instructor.PostAsJsonAsync("/api/instructor/courses", new
        {
            title, categoryId, shortDescription = "Học Node.js", level = "Beginner", price = 500000,
            description = "<p>Mô tả</p><script>alert('xss')</script>",
            outcomes = new[] { "Viết API với Express" },
        })).OkJsonAsync();
        var courseId = created.GetProperty("id").GetGuid();
        Assert.Equal("Draft", created.GetProperty("status").GetString());
        Assert.DoesNotContain("<script", created.GetProperty("description").GetString());

        // Gửi duyệt khi thiếu nội dung → 400
        Assert.Equal(HttpStatusCode.BadRequest, (await instructor.PostAsync($"/api/instructor/courses/{courseId}/submit", null)).StatusCode);

        var section = await (await instructor.PostAsJsonAsync($"/api/instructor/courses/{courseId}/sections", new { title = "Bắt đầu" })).OkJsonAsync();
        var sectionId = section.GetProperty("id").GetGuid();
        var lesson = await (await instructor.PostAsJsonAsync($"/api/instructor/sections/{sectionId}/lessons", new
        {
            title = "Cài đặt Node.js", type = "Video", videoUrl = "https://example.com/video.mp4", durationSeconds = 300, isPreview = true,
        })).OkJsonAsync();
        (await instructor.PostAsJsonAsync($"/api/instructor/sections/{sectionId}/lessons", new
        {
            title = "Module", type = "Article", content = "<p>Nội dung</p>", durationSeconds = 120, isPreview = false,
        })).EnsureSuccessStatusCode();
        (await instructor.PostAsJsonAsync($"/api/instructor/lessons/{lesson.GetProperty("id").GetGuid()}/resources", new
        {
            fileName = "slide.pdf", fileUrl = "https://example.com/slide.pdf", contentType = "application/pdf", fileSize = 1024,
        })).EnsureSuccessStatusCode();

        Assert.Equal(2, app.Db(db => db.Courses.First(c => c.Id == courseId).TotalLessons));
        Assert.Equal(420, app.Db(db => db.Courses.First(c => c.Id == courseId).TotalDurationSeconds));

        // Bổ sung ảnh rồi gửi duyệt
        (await instructor.PutAsJsonAsync($"/api/instructor/courses/{courseId}", new
        {
            title, categoryId, shortDescription = "Học Node.js", description = "<p>Mô tả đầy đủ</p>", level = "Beginner",
            price = 500000, discountPrice = 350000, thumbnailUrl = "https://picsum.photos/800/450",
        })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await instructor.PostAsync($"/api/instructor/courses/{courseId}/submit", null)).StatusCode);

        // Đang chờ duyệt → khóa chỉnh sửa
        var locked = await instructor.PostAsJsonAsync($"/api/instructor/courses/{courseId}/sections", new { title = "Thêm" });
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);

        // Giảng viên khác không thấy khóa học này
        var other = await app.LoginAsync("lethu.design@eduviet.local");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/instructor/courses/{courseId}")).StatusCode);

        // Admin từ chối rồi duyệt lại
        var admin = await app.LoginAsync("admin@eduviet.local");
        var pending = await (await admin.GetAsync("/api/admin/courses?status=Pending")).OkJsonAsync();
        Assert.Contains(pending.GetProperty("items").EnumerateArray(), c => c.GetProperty("id").GetGuid() == courseId);

        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.PostAsJsonAsync($"/api/admin/courses/{courseId}/reject", new { reason = "Cần thêm bài tập" })).StatusCode);
        Assert.Equal("Rejected", app.Db(db => db.Courses.First(c => c.Id == courseId).Status.ToString()));
        Assert.Equal(HttpStatusCode.NoContent, (await instructor.PostAsync($"/api/instructor/courses/{courseId}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/admin/courses/{courseId}/approve", null)).StatusCode);

        var slug = app.Db(db => db.Courses.First(c => c.Id == courseId).Slug);
        var publicDetail = await (await app.Anonymous().GetAsync($"/api/courses/{slug}")).OkJsonAsync();
        Assert.Equal(350000m, publicDetail.GetProperty("effectivePrice").GetDecimal());

        // Giảng viên nhận thông báo
        var notes = await (await instructor.GetAsync("/api/notifications")).OkJsonAsync();
        Assert.Contains(notes.GetProperty("items").EnumerateArray(), n => n.GetProperty("title").GetString() == "Khóa học đã được duyệt");
    }

    [Fact]
    public async Task Admin_overview_users_roles_and_coupons()
    {
        var admin = await app.LoginAsync("admin@eduviet.local");

        var overview = await (await admin.GetAsync("/api/admin/overview")).OkJsonAsync();
        Assert.True(overview.GetProperty("totalRevenue").GetDecimal() > 0);
        Assert.Equal(12, overview.GetProperty("monthly").GetArrayLength());
        Assert.True(overview.GetProperty("pendingCourses").GetInt32() >= 1);

        var students = await (await admin.GetAsync("/api/admin/users?role=Student&pageSize=100")).OkJsonAsync();
        var target = students.GetProperty("items").EnumerateArray().First(u => u.GetProperty("email").GetString() == "baoanh@eduviet.local");
        var targetId = target.GetProperty("id").GetGuid();

        var promoted = await (await admin.PutAsJsonAsync($"/api/admin/users/{targetId}/roles", new { roles = new[] { "Student", "Instructor" } })).OkJsonAsync();
        Assert.Contains("Instructor", promoted.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));

        // Không tự gỡ quyền Admin
        var adminId = app.Db(db => db.Users.First(u => u.Email == "admin@eduviet.local").Id);
        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.PutAsJsonAsync($"/api/admin/users/{adminId}/roles", new { roles = new[] { "Student" } })).StatusCode);

        // Khóa tài khoản → không đăng nhập được
        (await admin.PutAsJsonAsync($"/api/admin/users/{targetId}/active", new { isActive = false })).EnsureSuccessStatusCode();
        var login = await app.Anonymous().PostAsJsonAsync("/api/auth/login", new { email = "baoanh@eduviet.local", password = TestApp.Password });
        Assert.NotEqual(HttpStatusCode.OK, login.StatusCode);
        (await admin.PutAsJsonAsync($"/api/admin/users/{targetId}/active", new { isActive = true })).EnsureSuccessStatusCode();
        await app.LoginAsync("baoanh@eduviet.local");

        // CRUD mã giảm giá
        var coupon = await (await admin.PostAsJsonAsync("/api/admin/coupons", new
        {
            code = "tet2027", description = "Tết", discountType = "Percentage", discountValue = 20, maxDiscountAmount = 200000,
            minOrderAmount = 0, perUserLimit = 1, startsAt = DateTime.UtcNow.AddDays(-1), expiresAt = DateTime.UtcNow.AddDays(30), isActive = true,
        })).OkJsonAsync();
        Assert.Equal("TET2027", coupon.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/coupons", new
        {
            code = "TET2027", discountType = "FixedAmount", discountValue = 1000, minOrderAmount = 0, perUserLimit = 1,
            startsAt = DateTime.UtcNow, expiresAt = DateTime.UtcNow.AddDays(1), isActive = true,
        })).StatusCode);

        var orders = await (await admin.GetAsync("/api/admin/orders?status=Paid")).OkJsonAsync();
        Assert.True(orders.GetProperty("totalCount").GetInt32() > 0);

        // Danh mục: tạo, không xóa được khi còn khóa học
        var cat = await (await admin.PostAsJsonAsync("/api/admin/categories", new { name = "Âm nhạc", displayOrder = 9, isActive = true })).OkJsonAsync();
        Assert.Equal("am-nhac", cat.GetProperty("slug").GetString());
        var lapTrinh = app.Db(db => db.Categories.First(c => c.Slug == "lap-trinh").Id);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/admin/categories/{lapTrinh}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/categories/{cat.GetProperty("id").GetGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Instructor_replies_to_review()
    {
        var instructor = await app.LoginAsync("lethu.design@eduviet.local");
        var reviews = await (await instructor.GetAsync("/api/instructor/reviews")).OkJsonAsync();
        var unreplied = reviews.GetProperty("items").EnumerateArray().First(r => r.GetProperty("instructorReply").ValueKind == JsonValueKind.Null);
        var id = unreplied.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent,
            (await instructor.PostAsJsonAsync($"/api/instructor/reviews/{id}/reply", new { reply = "Cảm ơn bạn!" })).StatusCode);

        var otherInstructor = await app.LoginAsync("giangvien@eduviet.local");
        Assert.Equal(HttpStatusCode.NotFound,
            (await otherInstructor.PostAsJsonAsync($"/api/instructor/reviews/{id}/reply", new { reply = "Không phải khóa của tôi" })).StatusCode);
    }
}
