using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.IntegrationTests;

/// <summary>Quản lý học tập của Admin: hết lượt bài kiểm tra cuối khóa, cấp thêm lượt, sửa tiến độ, cấp/thu hồi quyền học, yêu cầu hỗ trợ.</summary>
[Collection(nameof(ApiCollection))]
public sealed class LearningSupportTests(TestApp app)
{
    private const string FreeCourse = "ky-nang-thuyet-trinh-chuyen-nghiep";
    private const string PaidCourse = "sql-server-cho-lap-trinh-vien";

    private async Task<(HttpClient Client, Guid UserId, string Email)> NewUserAsync(bool enrollFree = true)
    {
        var email = $"ls{Guid.NewGuid():N}@test.local";
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var reg = await (await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Học Viên Cần Hỗ Trợ", email, password = "Matkhau123" })).OkJsonAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reg.GetProperty("accessToken").GetString());
        if (enrollFree)
        {
            var courseId = app.Db(db => db.Courses.First(c => c.Slug == FreeCourse).Id);
            (await client.PostAsJsonAsync("/api/orders", new { courseIds = new[] { courseId }, fromCart = false })).EnsureSuccessStatusCode();
        }
        return (client, reg.GetProperty("user").GetProperty("id").GetGuid(), email);
    }

    private Task<HttpClient> AdminAsync() => app.LoginAsync("admin@eduviet.local");
    private Guid FinalQuizId() => app.Db(db => db.Quizzes.First(q => q.Course.Slug == FreeCourse && q.IsRequired && q.MaxAttempts != null).Id);
    private Guid EnrollmentId(Guid userId, string slug) => app.Db(db => db.Enrollments.First(e => e.UserId == userId && e.Course.Slug == slug).Id);

    private static async Task FailAttemptAsync(HttpClient c, Guid quizId)
    {
        var attempt = await (await c.PostAsync($"/api/learning/quizzes/{quizId}/attempts", null)).OkJsonAsync();
        var result = await (await c.PostAsJsonAsync($"/api/learning/attempts/{attempt.GetProperty("attemptId").GetGuid()}/submit", new { answers = Array.Empty<object>() })).OkJsonAsync();
        Assert.False(result.GetProperty("isPassed").GetBoolean());
    }

    [Fact]
    public async Task Failing_final_exam_until_out_of_attempts_leads_to_retake_request_and_admin_grants_more()
    {
        var (student, userId, _) = await NewUserAsync();
        var quizId = FinalQuizId();
        var max = app.Db(db => db.Quizzes.First(q => q.Id == quizId).MaxAttempts!.Value);
        for (var i = 0; i < max; i++) await FailAttemptAsync(student, quizId);

        // Hết lượt: không bắt đầu được nữa, có thông báo hướng dẫn bước tiếp theo
        var blocked = await student.PostAsync($"/api/learning/quizzes/{quizId}/attempts", null);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("gửi yêu cầu cấp thêm lượt", await blocked.Content.ReadAsStringAsync());
        var intro = await (await student.GetAsync($"/api/learning/quizzes/{quizId}")).OkJsonAsync();
        Assert.False(intro.GetProperty("canAttempt").GetBoolean());
        Assert.False(intro.GetProperty("hasOpenRetakeRequest").GetBoolean());
        Assert.True(app.Db(db => db.Notifications.Any(n => n.UserId == userId && n.Title == "Đã hết lượt làm bài kiểm tra")));

        // Học viên gửi yêu cầu cấp thêm lượt (không gửi trùng)
        var courseId = app.Db(db => db.Courses.First(c => c.Slug == FreeCourse).Id);
        var body = new { courseId, type = "QuizRetake", message = "Em đã ôn lại toàn bộ bài học, xin thêm một lượt.", quizId, lessonId = (Guid?)null };
        (await student.PostAsJsonAsync("/api/learning/support-requests", body)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsJsonAsync("/api/learning/support-requests", body)).StatusCode);
        Assert.True((await (await student.GetAsync($"/api/learning/quizzes/{quizId}")).OkJsonAsync()).GetProperty("hasOpenRetakeRequest").GetBoolean());

        // Admin thấy học viên ở nhóm "kẹt bài kiểm tra"
        var admin = await AdminAsync();
        var overview = await (await admin.GetAsync("/api/admin/learning/overview")).OkJsonAsync();
        Assert.True(overview.GetProperty("stuck").GetInt32() >= 1);
        Assert.True(overview.GetProperty("openSupportRequests").GetInt32() >= 1);
        var stuck = await (await admin.GetAsync($"/api/admin/learning/enrollments?state=stuck&keyword={Uri.EscapeDataString("Học Viên Cần Hỗ Trợ")}&pageSize=100")).OkJsonAsync();
        var row = stuck.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("userId").GetGuid() == userId);
        Assert.Equal(max, row.GetProperty("finalAttemptsUsed").GetInt32());
        var enrollmentId = row.GetProperty("enrollmentId").GetGuid();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/admin/learning/enrollments/{enrollmentId}/grant-attempts",
            new { quizId, extraAttempts = 1, reason = "" })).StatusCode);
        (await admin.PostAsJsonAsync($"/api/admin/learning/enrollments/{enrollmentId}/grant-attempts",
            new { quizId, extraAttempts = 1, reason = "Học viên đã ôn tập, cho làm lại" })).EnsureSuccessStatusCode();

        // Học viên làm tiếp được; yêu cầu tự đóng; có thông báo
        var after = await (await student.GetAsync($"/api/learning/quizzes/{quizId}")).OkJsonAsync();
        Assert.True(after.GetProperty("canAttempt").GetBoolean());
        Assert.Equal(max + 1, after.GetProperty("maxAttempts").GetInt32());
        Assert.Equal(1, after.GetProperty("extraAttempts").GetInt32());
        Assert.False(after.GetProperty("hasOpenRetakeRequest").GetBoolean());
        var mine = await (await student.GetAsync($"/api/learning/support-requests?courseId={courseId}")).OkJsonAsync();
        Assert.Equal("Resolved", mine[0].GetProperty("status").GetString());
        Assert.True(app.Db(db => db.Notifications.Any(n => n.UserId == userId && n.Title == "Bạn được cấp thêm lượt làm bài")));
        (await student.PostAsync($"/api/learning/quizzes/{quizId}/attempts", null)).EnsureSuccessStatusCode();

        // Dòng thời gian ghi nhận các lượt chưa đạt và thao tác của Admin
        var detail = await (await admin.GetAsync($"/api/admin/learning/enrollments/{enrollmentId}")).OkJsonAsync();
        var kinds = detail.GetProperty("timeline").EnumerateArray().Select(t => t.GetProperty("kind").GetString()).ToList();
        Assert.Equal(max, kinds.Count(k => k == "quiz-fail"));
        Assert.Contains("admin", kinds);
        Assert.Contains("support", kinds);
        Assert.Contains("enroll", kinds);
        var quiz = detail.GetProperty("quizzes").EnumerateArray().Single(q => q.GetProperty("quizId").GetGuid() == quizId);
        Assert.Equal(1, quiz.GetProperty("extraAttempts").GetInt32());
    }

    [Fact]
    public async Task Admin_fixes_lesson_progress_resets_and_recalculates_with_audit_log()
    {
        var (student, userId, _) = await NewUserAsync();
        var enrollmentId = EnrollmentId(userId, FreeCourse);
        var admin = await AdminAsync();
        var detail = await (await admin.GetAsync($"/api/admin/learning/enrollments/{enrollmentId}")).OkJsonAsync();
        Assert.Equal("not-started", detail.GetProperty("summary").GetProperty("state").GetString());
        var lessonId = detail.GetProperty("lessons")[0].GetProperty("lessonId").GetGuid();

        (await admin.PostAsJsonAsync($"/api/admin/learning/enrollments/{enrollmentId}/lesson",
            new { lessonId, completed = true, reason = "Học viên báo đã xem hết nhưng chưa ghi nhận" })).EnsureSuccessStatusCode();
        var progressed = await (await admin.GetAsync($"/api/admin/learning/enrollments/{enrollmentId}")).OkJsonAsync();
        Assert.True(progressed.GetProperty("summary").GetProperty("progressPercent").GetDecimal() > 0);
        Assert.True(progressed.GetProperty("lessons")[0].GetProperty("isCompleted").GetBoolean());
        var learn = await (await student.GetAsync($"/api/learning/courses/{FreeCourse}")).OkJsonAsync();
        Assert.True(learn.GetProperty("progressPercent").GetDecimal() > 0);

        (await admin.PostAsJsonAsync($"/api/admin/learning/enrollments/{enrollmentId}/recalculate", new { reason = "Kiểm tra lại tiến độ" })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/admin/learning/enrollments/{enrollmentId}/reset-progress", new { reason = "Học viên muốn học lại từ đầu" })).EnsureSuccessStatusCode();
        var reset = await (await admin.GetAsync($"/api/admin/learning/enrollments/{enrollmentId}")).OkJsonAsync();
        Assert.Equal(0m, reset.GetProperty("summary").GetProperty("progressPercent").GetDecimal());

        var actions = app.Db(db => db.LearningActionLogs.Where(l => l.EnrollmentId == enrollmentId).OrderBy(l => l.CreatedAt).Select(l => l.Action).ToList());
        Assert.Equal(["CompleteLesson", "Recalculate", "ResetProgress"], actions);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/admin/learning/overview")).StatusCode);
    }

    [Fact]
    public async Task Admin_grants_access_manually_and_can_revoke_and_restore_it()
    {
        var (student, userId, email) = await NewUserAsync(enrollFree: false);
        var courseId = app.Db(db => db.Courses.First(c => c.Slug == PaidCourse).Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync($"/api/learning/courses/{PaidCourse}")).StatusCode);

        var admin = await AdminAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/learning/enrollments", new { email, courseId, reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/admin/learning/enrollments",
            new { email = "khongton@tai.local", courseId, reason = "Thanh toán chuyển khoản" })).StatusCode);
        var created = await (await admin.PostAsJsonAsync("/api/admin/learning/enrollments",
            new { email, courseId, reason = "Học viên chuyển khoản trực tiếp, cổng thanh toán lỗi" })).OkJsonAsync();
        var enrollmentId = created.GetProperty("enrollmentId").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/learning/enrollments",
            new { email, courseId, reason = "Cấp lại lần nữa" })).StatusCode);
        (await student.GetAsync($"/api/learning/courses/{PaidCourse}")).EnsureSuccessStatusCode();

        var list = await (await admin.GetAsync($"/api/admin/learning/enrollments?keyword={Uri.EscapeDataString(email)}")).OkJsonAsync();
        Assert.Equal("Admin cấp", list.GetProperty("items")[0].GetProperty("source").GetString());

        (await admin.PostAsJsonAsync($"/api/admin/learning/enrollments/{enrollmentId}/revoke", new { reason = "Giao dịch chuyển khoản bị hủy" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync($"/api/learning/courses/{PaidCourse}")).StatusCode);
        Assert.Equal(EnrollmentStatus.Revoked, app.Db(db => db.Enrollments.First(e => e.Id == enrollmentId).Status));
        (await admin.PostAsJsonAsync($"/api/admin/learning/enrollments/{enrollmentId}/restore", new { reason = "Đã nhận lại tiền chuyển khoản" })).EnsureSuccessStatusCode();
        (await student.GetAsync($"/api/learning/courses/{PaidCourse}")).EnsureSuccessStatusCode();
        _ = userId;
    }

    [Fact]
    public async Task Learning_issue_report_is_answered_by_admin_and_only_owners_can_report()
    {
        var (student, userId, _) = await NewUserAsync();
        var courseId = app.Db(db => db.Courses.First(c => c.Slug == FreeCourse).Id);
        var (stranger, _, _) = await NewUserAsync(enrollFree: false);
        var body = new { courseId, type = "VideoIssue", message = "Video bài 2 bị giật và dừng ở phút thứ 3.", quizId = (Guid?)null, lessonId = (Guid?)null };
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.PostAsJsonAsync("/api/learning/support-requests", body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await student.PostAsJsonAsync("/api/learning/support-requests", body with { message = "lỗi" })).StatusCode);
        var created = await (await student.PostAsJsonAsync("/api/learning/support-requests", body)).OkJsonAsync();

        var admin = await AdminAsync();
        var open = await (await admin.GetAsync("/api/admin/learning/support-requests?status=Open&pageSize=100")).OkJsonAsync();
        var item = open.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == created.GetProperty("id").GetGuid());
        Assert.Equal(EnrollmentId(userId, FreeCourse), item.GetProperty("enrollmentId").GetGuid());

        var id = created.GetProperty("id").GetGuid();
        (await admin.PostAsJsonAsync($"/api/admin/learning/support-requests/{id}/respond",
            new { status = "Resolved", response = "Đã kiểm tra, bạn hãy tải lại trang để nhận liên kết video mới." })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/admin/learning/support-requests/{id}/respond",
            new { status = "Rejected", response = "Từ chối lại lần nữa" })).StatusCode);
        var mine = await (await student.GetAsync("/api/learning/support-requests")).OkJsonAsync();
        Assert.Equal("Resolved", mine[0].GetProperty("status").GetString());
        Assert.True(app.Db(db => db.Notifications.Any(n => n.UserId == userId && n.Title == "Yêu cầu hỗ trợ đã được xử lý")));
    }
}
