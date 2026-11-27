using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OnlineLearning.Domain.Constants;

namespace OnlineLearning.IntegrationTests;

/// <summary>Công cụ quản trị: kiểm duyệt đánh giá, hồ sơ giảng viên, nhật ký Admin, cài đặt hệ thống, thông báo hàng loạt, báo cáo, trạng thái hệ thống, xuất CSV.</summary>
[Collection(nameof(ApiCollection))]
public sealed class AdminToolsTests(TestApp app)
{
    private Task<HttpClient> AdminAsync() => app.LoginAsync("admin@eduviet.local");

    private async Task<(HttpClient Client, Guid UserId, string Email)> NewUserAsync(string name = "Người Dùng Thử")
    {
        var email = $"at{Guid.NewGuid():N}@test.local";
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var reg = await (await client.PostAsJsonAsync("/api/auth/register", new { fullName = name, email, password = "Matkhau123" })).OkJsonAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reg.GetProperty("accessToken").GetString());
        return (client, reg.GetProperty("user").GetProperty("id").GetGuid(), email);
    }

    [Fact]
    public async Task Admin_lists_reviews_with_filters_and_hides_one()
    {
        var admin = await AdminAsync();
        var list = await (await admin.GetAsync("/api/admin/reviews?pageSize=50")).OkJsonAsync();
        Assert.True(list.GetProperty("totalCount").GetInt32() > 0);
        var review = list.GetProperty("items")[0];
        var id = review.GetProperty("id").GetGuid();
        Assert.False(string.IsNullOrEmpty(review.GetProperty("courseTitle").GetString()));

        (await admin.PutAsJsonAsync($"/api/admin/reviews/{id}/hidden", new { isHidden = true })).EnsureSuccessStatusCode();
        var hidden = await (await admin.GetAsync("/api/admin/reviews?isHidden=true&pageSize=100")).OkJsonAsync();
        Assert.Contains(hidden.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        (await admin.PutAsJsonAsync($"/api/admin/reviews/{id}/hidden", new { isHidden = false })).EnsureSuccessStatusCode();

        var low = await (await admin.GetAsync("/api/admin/reviews?rating=1&pageSize=100")).OkJsonAsync();
        Assert.All(low.GetProperty("items").EnumerateArray(), x => Assert.Equal(1, x.GetProperty("rating").GetInt32()));

        // Thao tác ẩn/hiện được ghi vào nhật ký Admin
        Assert.True(app.Db(db => db.AdminAuditLogs.Any(l => l.Action == "Admin.SetReviewHidden" && l.Path.Contains(id.ToString()))));
    }

    [Fact]
    public async Task Student_applies_to_teach_and_admin_approval_grants_instructor_role()
    {
        var (student, userId, _) = await NewUserAsync("Ứng Viên Giảng Dạy");
        var mine = await (await student.GetAsync("/api/instructor-applications/me")).OkJsonAsync();
        Assert.False(mine.GetProperty("isInstructor").GetBoolean());
        Assert.Equal(JsonValueKind.Null, mine.GetProperty("latest").ValueKind);

        Assert.Equal(HttpStatusCode.BadRequest, (await student.PostAsJsonAsync("/api/instructor-applications",
            new { headline = "", bio = "ngắn", expertise = "", proposedTopic = "", portfolioUrl = (string?)null, yearsOfExperience = (int?)null })).StatusCode);

        var body = new
        {
            headline = "Kỹ sư dữ liệu 6 năm kinh nghiệm",
            bio = "Tôi đã làm việc với SQL Server, Python và Power BI tại nhiều doanh nghiệp, từng đào tạo nội bộ cho hơn 200 nhân viên.",
            expertise = "Phân tích dữ liệu, SQL, Power BI",
            proposedTopic = "Power BI từ cơ bản đến dashboard doanh nghiệp",
            portfolioUrl = "https://example.com/portfolio",
            yearsOfExperience = 6,
        };
        (await student.PostAsJsonAsync("/api/instructor-applications", body)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsJsonAsync("/api/instructor-applications", body)).StatusCode);
        Assert.True(app.Db(db => db.Notifications.Any(n => n.Link == "/quan-tri/dang-ky-giang-vien")));

        var admin = await AdminAsync();
        var pending = await (await admin.GetAsync("/api/admin/instructor-applications?status=Pending&pageSize=100")).OkJsonAsync();
        var appId = pending.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("userId").GetGuid() == userId).GetProperty("id").GetGuid();

        (await admin.PostAsJsonAsync($"/api/admin/instructor-applications/{appId}/approve", new { adminNote = "Hồ sơ tốt" })).EnsureSuccessStatusCode();
        Assert.True(app.Db(db => db.UserRoles.Any(ur => ur.UserId == userId && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Instructor))));
        Assert.Equal("Kỹ sư dữ liệu 6 năm kinh nghiệm", app.Db(db => db.Users.First(u => u.Id == userId).Headline));
        Assert.True(app.Db(db => db.Notifications.Any(n => n.UserId == userId && n.Link == "/giang-day/khoa-hoc")));

        // Duyệt lại hồ sơ đã xử lý → lỗi
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/admin/instructor-applications/{appId}/approve", new { adminNote = (string?)null })).StatusCode);
        mine = await (await student.GetAsync("/api/instructor-applications/me")).OkJsonAsync();
        Assert.True(mine.GetProperty("isInstructor").GetBoolean());
        Assert.Equal("Approved", mine.GetProperty("latest").GetProperty("status").GetString());
        Assert.True(app.Db(db => db.AdminAuditLogs.Any(l => l.Action == "AdminTools.ApproveApplication" && l.Succeeded)));
    }

    [Fact]
    public async Task Rejecting_application_requires_reason_and_allows_reapply()
    {
        var (student, userId, _) = await NewUserAsync("Ứng Viên Bị Từ Chối");
        var body = new
        {
            headline = "Giáo viên tiếng Anh", bio = "Tôi dạy tiếng Anh giao tiếp được 3 năm cho học sinh và người đi làm.",
            expertise = "Tiếng Anh giao tiếp", proposedTopic = "Tiếng Anh cho người mất gốc", portfolioUrl = (string?)null, yearsOfExperience = 3,
        };
        (await student.PostAsJsonAsync("/api/instructor-applications", body)).EnsureSuccessStatusCode();
        var admin = await AdminAsync();
        var appId = app.Db(db => db.InstructorApplications.First(a => a.UserId == userId).Id);

        var bad = await admin.PostAsJsonAsync($"/api/admin/instructor-applications/{appId}/reject", new { adminNote = "" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        // Thao tác thất bại cũng được ghi nhật ký
        Assert.True(app.Db(db => db.AdminAuditLogs.Any(l => l.Action == "AdminTools.RejectApplication" && !l.Succeeded && l.StatusCode == 400)));

        (await admin.PostAsJsonAsync($"/api/admin/instructor-applications/{appId}/reject", new { adminNote = "Vui lòng bổ sung video bài giảng mẫu." })).EnsureSuccessStatusCode();
        var mine = await (await student.GetAsync("/api/instructor-applications/me")).OkJsonAsync();
        Assert.Equal("Rejected", mine.GetProperty("latest").GetProperty("status").GetString());
        Assert.Equal("Vui lòng bổ sung video bài giảng mẫu.", mine.GetProperty("latest").GetProperty("adminNote").GetString());
        // Được nộp lại sau khi bị từ chối
        (await student.PostAsJsonAsync("/api/instructor-applications", body)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Audit_log_lists_filters_and_exports_admin_actions()
    {
        var admin = await AdminAsync();
        // Một thao tác ghi để chắc chắn có dữ liệu
        var catName = $"Danh mục nhật ký {Guid.NewGuid():N}"[..30];
        (await admin.PostAsJsonAsync("/api/admin/categories", new { name = catName, slug = (string?)null, description = (string?)null, iconUrl = (string?)null, parentId = (Guid?)null, displayOrder = 99, isActive = false })).EnsureSuccessStatusCode();

        var logs = await (await admin.GetAsync("/api/admin/audit-logs?keyword=" + Uri.EscapeDataString("Tạo danh mục") + "&pageSize=50")).OkJsonAsync();
        var entry = logs.GetProperty("items")[0];
        Assert.Equal("Tạo danh mục", entry.GetProperty("summary").GetString());
        Assert.Equal("POST", entry.GetProperty("httpMethod").GetString());
        Assert.Contains(catName, entry.GetProperty("payload").GetString());
        Assert.False(string.IsNullOrEmpty(entry.GetProperty("actorName").GetString()));

        // GET không bị ghi nhật ký
        Assert.False(app.Db(db => db.AdminAuditLogs.Any(l => l.HttpMethod == "GET")));

        var csv = await admin.GetAsync("/api/admin/audit-logs/export?keyword=" + Uri.EscapeDataString("Tạo danh mục"));
        csv.EnsureSuccessStatusCode();
        var text = await csv.Content.ReadAsStringAsync();
        Assert.Contains("Người thực hiện", text);
        Assert.Contains("Tạo danh mục", text);

        // Học viên không xem được nhật ký
        var (student, _, _) = await NewUserAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/admin/audit-logs")).StatusCode);
    }

    [Fact]
    public async Task Saving_settings_applies_live_and_reverting_restores_default()
    {
        var admin = await AdminAsync();
        try
        {
            var all = await (await admin.GetAsync("/api/admin/settings")).OkJsonAsync();
            var share = all.EnumerateArray().Single(x => x.GetProperty("key").GetString() == "Revenue:InstructorSharePercent");
            Assert.False(share.GetProperty("isOverridden").GetBoolean());

            // Giá trị ngoài khoảng cho phép → lỗi, khóa không tồn tại → lỗi
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/admin/settings",
                new { values = new Dictionary<string, string?> { ["Revenue:InstructorSharePercent"] = "150" } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/admin/settings",
                new { values = new Dictionary<string, string?> { ["Khong:TonTai"] = "1" } })).StatusCode);

            (await admin.PutAsJsonAsync("/api/admin/settings", new
            {
                values = new Dictionary<string, string?>
                {
                    ["Revenue:InstructorSharePercent"] = "65",
                    ["Revenue:RefundRequestDays"] = "10",
                    ["Invoice:SellerName"] = "Công ty EduViet Thử Nghiệm",
                },
            })).EnsureSuccessStatusCode();

            var pub = await (await app.Anonymous().GetAsync("/api/settings/public")).OkJsonAsync();
            Assert.Equal(65m, pub.GetProperty("instructorSharePercent").GetDecimal());
            Assert.Equal(10, pub.GetProperty("refundRequestDays").GetInt32());
            Assert.Equal("Công ty EduViet Thử Nghiệm", pub.GetProperty("sellerName").GetString());

            // Dịch vụ doanh thu đọc được giá trị mới mà không cần khởi động lại
            var instructor = await app.LoginAsync("giangvien@eduviet.local");
            var summary = await (await instructor.GetAsync("/api/instructor/earnings/summary")).OkJsonAsync();
            Assert.Equal(65m, summary.GetProperty("sharePercent").GetDecimal());
            Assert.True(app.Db(db => db.AdminAuditLogs.Any(l => l.Action == "AdminTools.SaveSettings" && l.Succeeded)));
        }
        finally
        {
            (await admin.PutAsJsonAsync("/api/admin/settings", new
            {
                values = new Dictionary<string, string?> { ["Revenue:InstructorSharePercent"] = null, ["Revenue:RefundRequestDays"] = "", ["Invoice:SellerName"] = null },
            })).EnsureSuccessStatusCode();
        }
        var reverted = await (await app.Anonymous().GetAsync("/api/settings/public")).OkJsonAsync();
        Assert.Equal(70m, reverted.GetProperty("instructorSharePercent").GetDecimal());
        Assert.False(app.Db(db => db.SystemSettings.Any(s => s.Key == "Revenue:InstructorSharePercent")));
    }

    [Fact]
    public async Task Announcement_reaches_target_audience_by_notification_and_email()
    {
        var admin = await AdminAsync();
        var courseId = app.Db(db => db.Courses.First(c => c.Slug == "ky-nang-thuyet-trinh-chuyen-nghiep").Id);
        var (student, userId, email) = await NewUserAsync("Học Viên Nhận Thông Báo");
        (await student.PostAsJsonAsync("/api/orders", new { courseIds = new[] { courseId }, fromCart = false })).EnsureSuccessStatusCode();

        var preview = await (await admin.GetAsync($"/api/admin/announcements/preview?audience=CourseStudents&courseId={courseId}")).OkJsonAsync();
        var expected = preview.GetProperty("recipients").GetInt32();
        Assert.True(expected >= 1);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/announcements",
            new { audience = "CourseStudents", courseId, title = "", message = "x", link = "https://ngoai.vn", sendEmail = false })).StatusCode);

        var title = $"Lịch học bù {Guid.NewGuid():N}"[..24];
        var sent = await (await admin.PostAsJsonAsync("/api/admin/announcements", new
        {
            audience = "CourseStudents", courseId, title, message = "Buổi hỏi đáp trực tuyến sẽ diễn ra tối thứ Sáu.", link = "/khoa-hoc-cua-toi", sendEmail = true,
        })).OkJsonAsync();
        Assert.Equal(expected, sent.GetProperty("recipientCount").GetInt32());
        Assert.True(app.Db(db => db.Notifications.Any(n => n.UserId == userId && n.Title == title && n.Link == "/khoa-hoc-cua-toi")));
        Assert.Contains(app.Emails.All, m => m.To == email && m.Subject.Contains(title));

        var history = await (await admin.GetAsync("/api/admin/announcements")).OkJsonAsync();
        Assert.Contains(history.GetProperty("items").EnumerateArray(), x => x.GetProperty("title").GetString() == title);

        var everyone = await (await admin.GetAsync("/api/admin/announcements/preview?audience=Everyone")).OkJsonAsync();
        Assert.True(everyone.GetProperty("recipients").GetInt32() > expected);
    }

    [Fact]
    public async Task Reports_status_and_exports_return_data()
    {
        var admin = await AdminAsync();
        var report = await (await admin.GetAsync("/api/admin/reports")).OkJsonAsync();
        Assert.True(report.GetProperty("totals").GetProperty("revenue").GetDecimal() > 0);
        Assert.Equal(12, report.GetProperty("monthly").GetArrayLength());
        Assert.True(report.GetProperty("topCourses").GetArrayLength() > 0);
        Assert.True(report.GetProperty("topInstructors").GetArrayLength() > 0);

        var empty = await (await admin.GetAsync("/api/admin/reports?from=2001-01-01&to=2001-01-31")).OkJsonAsync();
        Assert.Equal(0m, empty.GetProperty("totals").GetProperty("revenue").GetDecimal());

        var courseId = report.GetProperty("topCourses")[0].GetProperty("courseId").GetGuid();
        var lessons = await (await admin.GetAsync($"/api/admin/reports/courses/{courseId}/lessons")).OkJsonAsync();
        Assert.True(lessons.GetProperty("lessons").GetArrayLength() > 0);

        var csv = await (await admin.GetAsync("/api/admin/reports/export")).Content.ReadAsStringAsync();
        Assert.Contains("Khóa học", csv);

        var users = await admin.GetAsync("/api/admin/users/export?keyword=eduviet");
        users.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", users.Content.Headers.ContentType!.MediaType);
        Assert.Contains("admin@eduviet.local", await users.Content.ReadAsStringAsync());

        var enrollments = await admin.GetAsync("/api/admin/learning/enrollments/export");
        enrollments.EnsureSuccessStatusCode();
        Assert.Contains("Tiến độ (%)", await enrollments.Content.ReadAsStringAsync());

        var status = await (await admin.GetAsync("/api/admin/system")).OkJsonAsync();
        Assert.Contains(status.GetProperty("components").EnumerateArray(), c => c.GetProperty("name").GetString()!.Contains("Cơ sở dữ liệu") && c.GetProperty("status").GetString() == "ok");
        Assert.Equal(3, status.GetProperty("jobs").GetArrayLength());

        var run = await admin.PostAsync("/api/admin/system/jobs/order-expiry/run", null);
        run.EnsureSuccessStatusCode();
        status = await (await admin.GetAsync("/api/admin/system")).OkJsonAsync();
        var job = status.GetProperty("jobs").EnumerateArray().Single(j => j.GetProperty("key").GetString() == "order-expiry");
        Assert.True(job.GetProperty("lastSucceeded").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync("/api/admin/system/jobs/khong-co/run", null)).StatusCode);

        // Giảng viên chỉ xem thống kê bài học của khóa mình
        var instructor = await app.LoginAsync("giangvien@eduviet.local");
        var own = app.Db(db => db.Courses.First(c => c.Instructor.Email == "giangvien@eduviet.local").Id);
        (await instructor.GetAsync($"/api/instructor/courses/{own}/lesson-stats")).EnsureSuccessStatusCode();
        var other = app.Db(db => db.Courses.First(c => c.Instructor.Email != "giangvien@eduviet.local").Id);
        Assert.NotEqual(HttpStatusCode.OK, (await instructor.GetAsync($"/api/instructor/courses/{other}/lesson-stats")).StatusCode);
    }
}
