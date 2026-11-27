using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using OnlineLearning.Domain.Constants;

namespace OnlineLearning.IntegrationTests;

/// <summary>Tiện ích người học và giảng viên: phụ đề bài học, gợi ý tìm kiếm, giảng viên theo dõi học viên cần hỗ trợ, đăng nhập Google.</summary>
[Collection(nameof(ApiCollection))]
public sealed class LearnerInstructorToolsTests(TestApp app)
{
    private const string FreeCourse = "ky-nang-thuyet-trinh-chuyen-nghiep";

    private async Task<(HttpClient Client, Guid UserId)> NewStudentAsync(string name, bool enrollFree = true)
    {
        var email = $"lt{Guid.NewGuid():N}@test.local";
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var reg = await (await client.PostAsJsonAsync("/api/auth/register", new { fullName = name, email, password = "Matkhau123" })).OkJsonAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reg.GetProperty("accessToken").GetString());
        if (enrollFree)
        {
            var courseId = app.Db(db => db.Courses.First(c => c.Slug == FreeCourse).Id);
            (await client.PostAsJsonAsync("/api/orders", new { courseIds = new[] { courseId }, fromCart = false })).EnsureSuccessStatusCode();
        }
        return (client, reg.GetProperty("user").GetProperty("id").GetGuid());
    }

    private Task<HttpClient> InstructorOfFreeCourseAsync() =>
        app.LoginAsync(app.Db(db => db.Courses.Where(c => c.Slug == FreeCourse).Select(c => c.Instructor.Email).First())!);

    private static MultipartFormDataContent FileForm(string fileName, string content)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        return form;
    }

    [Fact]
    public async Task Instructor_uploads_srt_subtitle_converted_to_vtt_and_learner_gets_signed_url()
    {
        var instructor = await app.LoginAsync("giangvien@eduviet.local");
        var srt = "1\r\n00:00:01,000 --> 00:00:04,500\r\nXin chào các bạn\r\n\r\n2\r\n00:00:05,000 --> 00:00:08,000\r\nHôm nay chúng ta học SQL\r\n";

        // Không phải phụ đề → từ chối; học viên không được tải phụ đề
        Assert.Equal(HttpStatusCode.BadRequest, (await instructor.PostAsync("/api/uploads/subtitle", FileForm("loi.srt", "đây không phải phụ đề"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await instructor.PostAsync("/api/uploads/subtitle", FileForm("loi.vtt", "00:00:01.000 --> 00:00:02.000\nThiếu WEBVTT"))).StatusCode);
        var (student, _) = await NewStudentAsync("Học Viên Phụ Đề", enrollFree: false);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsync("/api/uploads/subtitle", FileForm("a.srt", srt))).StatusCode);

        var up = await (await instructor.PostAsync("/api/uploads/subtitle", FileForm("bai-1.srt", srt))).OkJsonAsync();
        Assert.Equal("subtitle", up.GetProperty("kind").GetString());
        Assert.EndsWith(".vtt", up.GetProperty("fileName").GetString());
        var publicId = up.GetProperty("publicId").GetString()!;

        // Gắn phụ đề vào bài video của khóa đã xuất bản (không cần mở khóa chỉnh sửa)
        var lessonId = app.Db(db => db.Lessons.Where(l => l.Type == Domain.Enums.LessonType.Video && l.Section.Course.Instructor.Email == "giangvien@eduviet.local"
            && l.Section.Course.Status == Domain.Enums.CourseStatus.Published).Select(l => l.Id).First());
        var set = await (await instructor.PutAsJsonAsync($"/api/instructor/lessons/{lessonId}/subtitle", new { subtitlePublicId = publicId })).OkJsonAsync();
        Assert.Equal(publicId, set.GetProperty("subtitlePublicId").GetString());
        Assert.Equal(publicId, app.Db(db => db.Lessons.First(l => l.Id == lessonId).SubtitlePublicId));

        // Học viên và giảng viên khác không gắn được phụ đề vào bài này
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PutAsJsonAsync($"/api/instructor/lessons/{lessonId}/subtitle", new { subtitlePublicId = publicId })).StatusCode);

        // Gỡ phụ đề
        var cleared = await (await instructor.PutAsJsonAsync($"/api/instructor/lessons/{lessonId}/subtitle", new { subtitlePublicId = (string?)null })).OkJsonAsync();
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("subtitlePublicId").ValueKind);
    }

    [Fact]
    public async Task Learner_lesson_content_includes_subtitle_url_and_next_lesson_title()
    {
        // Gắn phụ đề trực tiếp vào bài đầu tiên của khóa miễn phí rồi kiểm tra API học
        var instructor = await InstructorOfFreeCourseAsync();
        var up = await (await instructor.PostAsync("/api/uploads/subtitle",
            FileForm("intro.vtt", "WEBVTT\n\n00:00:00.500 --> 00:00:03.000\nChào mừng đến với khóa học\n"))).OkJsonAsync();
        var publicId = up.GetProperty("publicId").GetString()!;
        var lessonId = app.Db(db => db.Lessons.Where(l => l.Section.Course.Slug == FreeCourse && l.Type == Domain.Enums.LessonType.Video)
            .OrderBy(l => l.Section.SortOrder).ThenBy(l => l.SortOrder).Select(l => l.Id).First());
        app.Db(db =>
        {
            db.Lessons.First(l => l.Id == lessonId).SubtitlePublicId = publicId;
            return db.SaveChanges();
        });

        var (student, _) = await NewStudentAsync("Học Viên Xem Phụ Đề");
        var lesson = await (await student.GetAsync($"/api/learning/lessons/{lessonId}")).OkJsonAsync();
        var subtitleUrl = lesson.GetProperty("subtitleUrl").GetString();
        Assert.False(string.IsNullOrEmpty(subtitleUrl));
        if (lesson.GetProperty("nextLessonId").ValueKind != JsonValueKind.Null)
            Assert.False(string.IsNullOrEmpty(lesson.GetProperty("nextLessonTitle").GetString()));

        var vtt = await app.Anonymous().GetStringAsync(subtitleUrl);
        Assert.StartsWith("WEBVTT", vtt);
        Assert.Contains("Chào mừng đến với khóa học", vtt);

        app.Db(db =>
        {
            db.Lessons.First(l => l.Id == lessonId).SubtitlePublicId = null;
            return db.SaveChanges();
        });
    }

    [Fact]
    public async Task Search_suggestions_return_courses_instructors_and_categories()
    {
        var anon = app.Anonymous();
        var empty = await (await anon.GetAsync("/api/courses/suggest?q=a")).OkJsonAsync();
        Assert.Equal(0, empty.GetProperty("courses").GetArrayLength());

        var course = app.Db(db => db.Courses.Where(c => c.Status == Domain.Enums.CourseStatus.Published)
            .Select(c => new { c.Title, c.Slug, Instructor = c.Instructor.FullName }).First());
        var word = course.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries).First(w => w.Length >= 3);
        var res = await (await anon.GetAsync($"/api/courses/suggest?q={Uri.EscapeDataString(word)}")).OkJsonAsync();
        Assert.Contains(res.GetProperty("courses").EnumerateArray(), c => c.GetProperty("title").GetString()!.Contains(word, StringComparison.OrdinalIgnoreCase)
            || c.GetProperty("instructorName").GetString()!.Contains(word, StringComparison.OrdinalIgnoreCase));
        Assert.True(res.GetProperty("courses").GetArrayLength() <= 6);

        var byInstructor = await (await anon.GetAsync($"/api/courses/suggest?q={Uri.EscapeDataString(course.Instructor)}")).OkJsonAsync();
        var ins = byInstructor.GetProperty("instructors").EnumerateArray().First();
        Assert.Equal(course.Instructor, ins.GetProperty("fullName").GetString());
        Assert.True(ins.GetProperty("courseCount").GetInt32() >= 1);

        // Đường dẫn "suggest" không bị hiểu nhầm là slug khóa học
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/api/courses/suggest")).StatusCode);
    }

    [Fact]
    public async Task Instructor_sees_stuck_learner_in_own_course_and_grants_extra_attempt()
    {
        var (student, userId) = await NewStudentAsync("Học Viên Kẹt Kiểm Tra");
        var quizId = app.Db(db => db.Quizzes.First(q => q.Course.Slug == FreeCourse && q.IsRequired && q.MaxAttempts != null).Id);
        var max = app.Db(db => db.Quizzes.First(q => q.Id == quizId).MaxAttempts!.Value);
        for (var i = 0; i < max; i++)
        {
            var attempt = await (await student.PostAsync($"/api/learning/quizzes/{quizId}/attempts", null)).OkJsonAsync();
            (await student.PostAsJsonAsync($"/api/learning/attempts/{attempt.GetProperty("attemptId").GetGuid()}/submit", new { answers = Array.Empty<object>() })).EnsureSuccessStatusCode();
        }

        // Học viên thường không gọi được API giảng viên
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/instructor/learners")).StatusCode);

        var instructor = await InstructorOfFreeCourseAsync();
        var list = await (await instructor.GetAsync($"/api/instructor/learners?state=stuck&keyword={Uri.EscapeDataString("Học Viên Kẹt Kiểm Tra")}&pageSize=50")).OkJsonAsync();
        Assert.True(list.GetProperty("counts").GetProperty("stuck").GetInt32() >= 1);
        var row = list.GetProperty("page").GetProperty("items").EnumerateArray().Single(x => x.GetProperty("userId").GetGuid() == userId);
        Assert.Equal("stuck", row.GetProperty("state").GetString());
        var stuckQuiz = row.GetProperty("stuckQuiz");
        Assert.Equal(quizId, stuckQuiz.GetProperty("quizId").GetGuid());
        Assert.Equal(max, stuckQuiz.GetProperty("attemptsUsed").GetInt32());
        var enrollmentId = row.GetProperty("enrollmentId").GetGuid();

        // Giảng viên khác không thấy và không cấp lượt được
        var otherEmail = app.Db(db => db.Users.Where(u => u.Email != null && u.Email.EndsWith("@eduviet.local")
                && db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Instructor))
                && !db.Courses.Any(c => c.Slug == FreeCourse && c.InstructorId == u.Id))
            .Select(u => u.Email).FirstOrDefault());
        if (otherEmail is not null)
        {
            var other = await app.LoginAsync(otherEmail);
            var otherList = await (await other.GetAsync("/api/instructor/learners?pageSize=100&keyword=" + Uri.EscapeDataString("Học Viên Kẹt Kiểm Tra"))).OkJsonAsync();
            Assert.DoesNotContain(otherList.GetProperty("page").GetProperty("items").EnumerateArray(), x => x.GetProperty("userId").GetGuid() == userId);
            Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/instructor/learners/{enrollmentId}/grant-attempts",
                new { quizId, extraAttempts = 1, reason = "Cho làm lại bài" })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await instructor.PostAsJsonAsync($"/api/instructor/learners/{enrollmentId}/grant-attempts",
            new { quizId, extraAttempts = 5, reason = "Quá nhiều lượt" })).StatusCode);
        (await instructor.PostAsJsonAsync($"/api/instructor/learners/{enrollmentId}/grant-attempts",
            new { quizId, extraAttempts = 1, reason = "Đã trao đổi, cho làm lại một lượt" })).EnsureSuccessStatusCode();

        var intro = await (await student.GetAsync($"/api/learning/quizzes/{quizId}")).OkJsonAsync();
        Assert.True(intro.GetProperty("canAttempt").GetBoolean());
        Assert.True(app.Db(db => db.Notifications.Any(n => n.UserId == userId && n.Title == "Bạn được cấp thêm lượt làm bài")));
    }

    [Fact]
    public async Task Google_login_creates_account_links_existing_email_and_rejects_bad_tokens()
    {
        var anon = app.Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/auth/google", new { credential = "khong-hop-le" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync("/api/auth/google", new { credential = "" })).StatusCode);
        var unverified = $"gu{Guid.NewGuid():N}@gmail.test";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/auth/google", new { credential = $"google:sub-u:{unverified}:Chưa Xác Minh:unverified" })).StatusCode);

        // Tài khoản mới: vai trò Học viên, email đã xác nhận, không có mật khẩu
        var email = $"g{Guid.NewGuid():N}@gmail.test";
        var sub = Guid.NewGuid().ToString("N");
        var first = await (await anon.PostAsJsonAsync("/api/auth/google", new { credential = $"google:{sub}:{email}:Nguyễn Google" })).OkJsonAsync();
        Assert.False(string.IsNullOrEmpty(first.GetProperty("accessToken").GetString()));
        Assert.Equal("Nguyễn Google", first.GetProperty("user").GetProperty("fullName").GetString());
        Assert.Contains(Roles.Student, first.GetProperty("user").GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.True(app.Db(db => db.Users.First(u => u.Email == email).EmailConfirmed));
        Assert.Null(app.Db(db => db.Users.First(u => u.Email == email).PasswordHash));

        // Đăng nhập lại cùng tài khoản Google → cùng người dùng
        var again = await (await app.Anonymous().PostAsJsonAsync("/api/auth/google", new { credential = $"google:{sub}:{email}:Nguyễn Google" })).OkJsonAsync();
        Assert.Equal(first.GetProperty("user").GetProperty("id").GetGuid(), again.GetProperty("user").GetProperty("id").GetGuid());

        // Tài khoản đã đăng ký bằng mật khẩu cùng email → gắn liên kết Google, không tạo trùng
        var pwEmail = $"pw{Guid.NewGuid():N}@gmail.test";
        var reg = await (await app.Anonymous().PostAsJsonAsync("/api/auth/register", new { fullName = "Người Có Mật Khẩu", email = pwEmail, password = "Matkhau123" })).OkJsonAsync();
        var linked = await (await app.Anonymous().PostAsJsonAsync("/api/auth/google", new { credential = $"google:{Guid.NewGuid():N}:{pwEmail}:Tên Khác" })).OkJsonAsync();
        Assert.Equal(reg.GetProperty("user").GetProperty("id").GetGuid(), linked.GetProperty("user").GetProperty("id").GetGuid());
        Assert.Equal("Người Có Mật Khẩu", linked.GetProperty("user").GetProperty("fullName").GetString());
        Assert.Equal(1, app.Db(db => db.Users.Count(u => u.Email == pwEmail)));
        Assert.True(app.Db(db => db.Users.First(u => u.Email == pwEmail).EmailConfirmed));

        // Trang công khai biết Google đã bật để hiện nút
        var settings = await (await anon.GetAsync("/api/settings/public")).OkJsonAsync();
        Assert.Equal("test-client.apps.googleusercontent.com", settings.GetProperty("googleClientId").GetString());
    }
}
