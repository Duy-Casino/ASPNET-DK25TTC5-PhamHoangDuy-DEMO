using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OnlineLearning.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class CertificateTests(TestApp app)
{
    private const string FreeCourse = "ky-nang-thuyet-trinh-chuyen-nghiep";

    /// <summary>Học viên mới: mua khóa miễn phí, học hết bài, đạt bài kiểm tra cuối khóa.</summary>
    private async Task<(HttpClient Client, string Email, JsonElement LastProgress, JsonElement QuizResult)> CompleteFreeCourseAsync()
    {
        var email = $"cert{Guid.NewGuid():N}@test.local";
        var client = app.Anonymous();
        var reg = await (await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Nguyễn Thị Chứng Chỉ", email, password = "Matkhau123" })).OkJsonAsync();
        client.DefaultRequestHeaders.Authorization = new("Bearer", reg.GetProperty("accessToken").GetString());
        var courseId = app.Db(db => db.Courses.First(c => c.Slug == FreeCourse).Id);
        (await client.PostAsJsonAsync("/api/orders", new { courseIds = new[] { courseId } })).EnsureSuccessStatusCode();

        var course = await (await client.GetAsync($"/api/learning/courses/{FreeCourse}")).OkJsonAsync();
        var quizId = course.GetProperty("courseQuizzes")[0].GetProperty("id").GetGuid();

        // Làm bài đạt trước, sau đó hoàn thành bài cuối cùng → chứng chỉ được cấp ở bước hoàn thành bài
        var take = await (await client.PostAsync($"/api/learning/quizzes/{quizId}/attempts", null)).OkJsonAsync();
        var correct = app.Db(db => db.AnswerOptions.Where(o => o.Question.QuizId == quizId && o.IsCorrect).Select(o => new { o.QuestionId, o.Id }).ToList());
        var quiz = await (await client.PostAsJsonAsync($"/api/learning/attempts/{take.GetProperty("attemptId").GetGuid()}/submit",
            new { answers = correct.Select(c => new { questionId = c.QuestionId, optionIds = new[] { c.Id } }) })).OkJsonAsync();
        Assert.True(quiz.GetProperty("isPassed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, quiz.GetProperty("certificateCode").ValueKind); // chưa học xong bài

        JsonElement progress = default;
        foreach (var id in course.GetProperty("sections").EnumerateArray().SelectMany(s => s.GetProperty("lessons").EnumerateArray()).Select(l => l.GetProperty("id").GetGuid()))
            progress = await (await client.PostAsync($"/api/learning/lessons/{id}/complete", null)).OkJsonAsync();
        return (client, email, progress, quiz);
    }

    [Fact]
    public async Task Completing_course_issues_one_certificate_with_pdf_and_public_verification()
    {
        var (client, email, progress, _) = await CompleteFreeCourseAsync();

        Assert.True(progress.GetProperty("courseCompleted").GetBoolean());
        var code = progress.GetProperty("certificateCode").GetString()!;
        Assert.Matches(new Regex(@"^EDV-\d{4}-[2-9A-HJ-NP-Z]{8}$"), code);
        Assert.Equal(1, app.Db(db => db.Certificates.Count(c => c.User.Email == email)));

        // Học lại / bỏ đánh dấu rồi hoàn thành lại không cấp chứng chỉ thứ hai
        var lessonId = app.Db(db => db.Lessons.First(l => l.Section.Course.Slug == FreeCourse).Id);
        await client.DeleteAsync($"/api/learning/lessons/{lessonId}/complete");
        var again = await (await client.PostAsync($"/api/learning/lessons/{lessonId}/complete", null)).OkJsonAsync();
        Assert.Equal(code, again.GetProperty("certificateCode").GetString());
        Assert.Equal(1, app.Db(db => db.Certificates.Count(c => c.User.Email == email)));

        // Chứng chỉ có tên, khóa học, điểm thi cuối khóa
        var mine = await (await client.GetAsync("/api/learning/certificates")).OkJsonAsync();
        var cert = mine.EnumerateArray().Single();
        Assert.Equal(100m, cert.GetProperty("finalScore").GetDecimal());
        var certId = cert.GetProperty("id").GetGuid();

        // Tải PDF
        var pdf = await client.GetAsync($"/api/certificates/{certId}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 5000);

        // Người khác không tải được bằng id
        var other = await app.LoginAsync("ducthinh@eduviet.local");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/certificates/{certId}/pdf")).StatusCode);

        // Xác minh công khai (không cần đăng nhập), không phân biệt hoa thường
        var verify = await (await app.Anonymous().GetAsync($"/api/certificates/verify/{code.ToLowerInvariant()}")).OkJsonAsync();
        Assert.Equal("Nguyễn Thị Chứng Chỉ", verify.GetProperty("studentName").GetString());
        Assert.False(verify.GetProperty("isRevoked").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await app.Anonymous().GetAsync($"/api/certificates/verify/{code}/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Anonymous().GetAsync("/api/certificates/verify/EDV-2026-ZZZZZZZZ")).StatusCode);

        // Admin thu hồi → trang xác minh báo đã thu hồi, PDF công khai bị chặn
        var admin = await app.LoginAsync("admin@eduviet.local");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/admin/certificates/{certId}/revoke", new { reason = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/admin/certificates/{certId}/revoke", new { reason = "Gian lận bài kiểm tra" })).StatusCode);
        var revoked = await (await app.Anonymous().GetAsync($"/api/certificates/verify/{code}")).OkJsonAsync();
        Assert.True(revoked.GetProperty("isRevoked").GetBoolean());
        Assert.Equal("Gian lận bài kiểm tra", revoked.GetProperty("revokedReason").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await app.Anonymous().GetAsync($"/api/certificates/verify/{code}/pdf")).StatusCode);
        Assert.Empty((await (await client.GetAsync("/api/learning/certificates")).OkJsonAsync()).EnumerateArray());

        // Khôi phục
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/admin/certificates/{certId}/restore", null)).StatusCode);
        Assert.False((await (await app.Anonymous().GetAsync($"/api/certificates/verify/{code}")).OkJsonAsync()).GetProperty("isRevoked").GetBoolean());

        var list = await (await admin.GetAsync($"/api/admin/certificates?keyword={code}")).OkJsonAsync();
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/certificates")).StatusCode);
    }

    [Fact]
    public async Task Incomplete_course_has_no_certificate()
    {
        var student = await app.LoginAsync("hocvien@eduviet.local");
        var mine = await (await student.GetAsync("/api/learning/certificates")).OkJsonAsync();
        Assert.Empty(mine.EnumerateArray());

        var admin = await app.LoginAsync("admin@eduviet.local");
        var res = await (await admin.PostAsync("/api/admin/certificates/backfill", null)).OkJsonAsync();
        Assert.True(res.GetProperty("issued").GetInt32() >= 0);
    }
}
