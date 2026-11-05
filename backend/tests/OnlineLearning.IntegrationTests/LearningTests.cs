using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OnlineLearning.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class LearningTests(TestApp app)
{
    private const string FreeCourse = "ky-nang-thuyet-trinh-chuyen-nghiep";

    private async Task<(HttpClient Client, string Email)> NewStudentWithFreeCourseAsync()
    {
        var email = $"learn{Guid.NewGuid():N}@test.local";
        var client = app.Anonymous();
        var reg = await (await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Học Viên Học", email, password = "Matkhau123" })).OkJsonAsync();
        client.DefaultRequestHeaders.Authorization = new("Bearer", reg.GetProperty("accessToken").GetString());
        var courseId = app.Db(db => db.Courses.First(c => c.Slug == FreeCourse).Id);
        var order = await (await client.PostAsJsonAsync("/api/orders", new { courseIds = new[] { courseId } })).OkJsonAsync();
        Assert.Equal("Paid", order.GetProperty("status").GetString());
        return (client, email);
    }

    [Fact]
    public async Task Lesson_content_is_protected_until_enrolled()
    {
        var outsider = await app.LoginAsync("ducthinh@eduviet.local");
        var lessons = app.Db(db => db.Lessons.Where(l => l.Section.Course.Slug == "power-bi-phan-tich-du-lieu-kinh-doanh")
            .Select(l => new { l.Id, l.IsPreview }).ToList());

        var locked = lessons.First(l => !l.IsPreview);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/learning/lessons/{locked.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync("/api/learning/courses/power-bi-phan-tich-du-lieu-kinh-doanh")).StatusCode);

        // Bài học thử mở cho người đã đăng nhập
        var preview = lessons.First(l => l.IsPreview);
        Assert.Equal(HttpStatusCode.OK, (await outsider.GetAsync($"/api/learning/lessons/{preview.Id}")).StatusCode);

        // Giảng viên sở hữu xem được ở chế độ xem trước
        var owner = await app.LoginAsync("phamquang.data@eduviet.local");
        var course = await (await owner.GetAsync("/api/learning/courses/power-bi-phan-tich-du-lieu-kinh-doanh")).OkJsonAsync();
        Assert.True(course.GetProperty("isPreviewMode").GetBoolean());
    }

    [Fact]
    public async Task Progress_resume_quiz_attempt_limit_and_completion()
    {
        var (client, email) = await NewStudentWithFreeCourseAsync();

        var course = await (await client.GetAsync($"/api/learning/courses/{FreeCourse}")).OkJsonAsync();
        var lessonIds = course.GetProperty("sections").EnumerateArray()
            .SelectMany(s => s.GetProperty("lessons").EnumerateArray()).Select(l => l.GetProperty("id").GetGuid()).ToList();
        var quizId = course.GetProperty("courseQuizzes")[0].GetProperty("id").GetGuid();
        Assert.Equal(0m, course.GetProperty("progressPercent").GetDecimal());

        // Mở bài 2 → được ghi nhận là bài gần nhất
        var lesson = await (await client.GetAsync($"/api/learning/lessons/{lessonIds[1]}")).OkJsonAsync();
        Assert.Equal(lessonIds[0], lesson.GetProperty("previousLessonId").GetGuid());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/learning/lessons/{lessonIds[1]}/position", new { positionSeconds = 95 })).StatusCode);
        course = await (await client.GetAsync($"/api/learning/courses/{FreeCourse}")).OkJsonAsync();
        Assert.Equal(lessonIds[1], course.GetProperty("resumeLessonId").GetGuid());
        lesson = await (await client.GetAsync($"/api/learning/lessons/{lessonIds[1]}")).OkJsonAsync();
        Assert.Equal(95, lesson.GetProperty("lastPositionSeconds").GetInt32());

        // Hoàn thành toàn bộ bài học → 100% nhưng chưa "Completed" vì còn bài kiểm tra bắt buộc
        JsonElement progress = default;
        foreach (var id in lessonIds)
            progress = await (await client.PostAsync($"/api/learning/lessons/{id}/complete", null)).OkJsonAsync();
        Assert.Equal(100m, progress.GetProperty("progressPercent").GetDecimal());
        Assert.Equal("Active", progress.GetProperty("status").GetString());

        // Làm bài: đề không chứa đáp án đúng
        var take = await (await client.PostAsync($"/api/learning/quizzes/{quizId}/attempts", null)).OkJsonAsync();
        var raw = take.GetRawText();
        Assert.DoesNotContain("isCorrect", raw, StringComparison.OrdinalIgnoreCase);
        Assert.True(take.GetProperty("deadline").ValueKind == JsonValueKind.String);

        var correct = app.Db(db => db.AnswerOptions.Where(o => o.Question.QuizId == quizId && o.IsCorrect)
            .Select(o => new { o.QuestionId, o.Id }).ToList());
        var wrong = app.Db(db => db.AnswerOptions.Where(o => o.Question.QuizId == quizId && !o.IsCorrect)
            .Select(o => new { o.QuestionId, o.Id }).ToList())
            .GroupBy(o => o.QuestionId).Select(g => new { questionId = g.Key, optionIds = new[] { g.First().Id } }).ToArray();

        // Lượt 1: sai hết
        var r1 = await (await client.PostAsJsonAsync($"/api/learning/attempts/{take.GetProperty("attemptId").GetGuid()}/submit", new { answers = wrong })).OkJsonAsync();
        Assert.False(r1.GetProperty("isPassed").GetBoolean());
        Assert.Equal(0m, r1.GetProperty("scorePercent").GetDecimal());
        Assert.True(r1.GetProperty("questions")[0].GetProperty("options").EnumerateArray().Any(o => o.GetProperty("isCorrect").GetBoolean()));

        // Nộp lại lượt đã nộp → trả kết quả cũ, không chấm lại
        var again = await (await client.PostAsJsonAsync($"/api/learning/attempts/{take.GetProperty("attemptId").GetGuid()}/submit",
            new { answers = correct.Select(c => new { questionId = c.QuestionId, optionIds = new[] { c.Id } }) })).OkJsonAsync();
        Assert.False(again.GetProperty("isPassed").GetBoolean());

        // Lượt 2: đúng hết → đạt, khóa học hoàn thành
        var t2 = await (await client.PostAsync($"/api/learning/quizzes/{quizId}/attempts", null)).OkJsonAsync();
        var r2 = await (await client.PostAsJsonAsync($"/api/learning/attempts/{t2.GetProperty("attemptId").GetGuid()}/submit",
            new { answers = correct.Select(c => new { questionId = c.QuestionId, optionIds = new[] { c.Id } }) })).OkJsonAsync();
        Assert.True(r2.GetProperty("isPassed").GetBoolean());
        Assert.Equal(100m, r2.GetProperty("scorePercent").GetDecimal());
        Assert.True(r2.GetProperty("courseCompleted").GetBoolean());
        Assert.Equal("Completed", app.Db(db => db.Enrollments.First(e => e.User.Email == email).Status.ToString()));

        // Lượt 3 rồi hết lượt (tối đa 3)
        var t3 = await (await client.PostAsync($"/api/learning/quizzes/{quizId}/attempts", null)).OkJsonAsync();
        (await client.PostAsJsonAsync($"/api/learning/attempts/{t3.GetProperty("attemptId").GetGuid()}/submit", new { answers = Array.Empty<object>() })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/learning/quizzes/{quizId}/attempts", null)).StatusCode);

        var intro = await (await client.GetAsync($"/api/learning/quizzes/{quizId}")).OkJsonAsync();
        Assert.False(intro.GetProperty("canAttempt").GetBoolean());
        Assert.Equal(100m, intro.GetProperty("bestScore").GetDecimal());

        // Bỏ đánh dấu một bài → khóa học quay về Active
        await client.DeleteAsync($"/api/learning/lessons/{lessonIds[0]}/complete");
        Assert.Equal("Active", app.Db(db => db.Enrollments.First(e => e.User.Email == email).Status.ToString()));

        // Dashboard học viên
        var summary = await (await client.GetAsync("/api/learning/summary")).OkJsonAsync();
        Assert.Equal(1, summary.GetProperty("totalCourses").GetInt32());
        Assert.Equal(1, summary.GetProperty("quizzesPassed").GetInt32());
        var results = await (await client.GetAsync("/api/learning/quiz-results")).OkJsonAsync();
        Assert.Equal(3, results.GetProperty("totalCount").GetInt32());
        var mine = await (await client.GetAsync("/api/learning/courses?filter=in-progress")).OkJsonAsync();
        Assert.Equal(1, mine.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Other_user_cannot_read_or_submit_my_attempt()
    {
        var (client, _) = await NewStudentWithFreeCourseAsync();
        var quizId = app.Db(db => db.Quizzes.First(q => q.Course.Slug == FreeCourse).Id);
        var take = await (await client.PostAsync($"/api/learning/quizzes/{quizId}/attempts", null)).OkJsonAsync();
        var attemptId = take.GetProperty("attemptId").GetGuid();

        var (other, _) = await NewStudentWithFreeCourseAsync();
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.PostAsJsonAsync($"/api/learning/attempts/{attemptId}/submit", new { answers = Array.Empty<object>() })).StatusCode);
    }

    [Fact]
    public async Task Instructor_authors_quiz_and_sees_stats()
    {
        var instructor = await app.LoginAsync("giangvien@eduviet.local");
        var categoryId = app.Db(db => db.Categories.First(c => c.Slug == "lap-trinh").Id);
        var course = await (await instructor.PostAsJsonAsync("/api/instructor/courses", new
        {
            title = $"Quiz test {Guid.NewGuid():N}"[..24], categoryId, level = "Beginner", price = 0,
        })).OkJsonAsync();
        var courseId = course.GetProperty("id").GetGuid();

        // Validation: câu đúng-sai phải có đúng 2 đáp án, 1 đáp án đúng
        var bad = await instructor.PostAsJsonAsync($"/api/instructor/courses/{courseId}/quizzes", new
        {
            title = "Sai", passingScore = 50, isRequired = true, isFinalExam = false, shuffleQuestions = false,
            questions = new[] { new { content = "?", type = "TrueFalse", points = 1, options = new[] { new { content = "Đúng", isCorrect = true }, new { content = "Sai", isCorrect = true } } } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var quiz = await (await instructor.PostAsJsonAsync($"/api/instructor/courses/{courseId}/quizzes", new
        {
            title = "Kiểm tra chương 1", passingScore = 60, maxAttempts = 2, timeLimitMinutes = 10, isRequired = true, isFinalExam = false, shuffleQuestions = true,
            questions = new object[]
            {
                new { content = "2 + 2 = ?", type = "SingleChoice", points = 1, explanation = "Cộng cơ bản", options = new[] { new { content = "3", isCorrect = false }, new { content = "4", isCorrect = true } } },
                new { content = "Chọn số chẵn", type = "MultipleChoice", points = 2, options = new[] { new { content = "2", isCorrect = true }, new { content = "3", isCorrect = false }, new { content = "4", isCorrect = true } } },
            },
        })).OkJsonAsync();
        Assert.Equal(2, quiz.GetProperty("questions").GetArrayLength());
        var quizId = quiz.GetProperty("id").GetGuid();

        var list = await (await instructor.GetAsync($"/api/instructor/courses/{courseId}/quizzes")).OkJsonAsync();
        Assert.Equal(1, list.GetArrayLength());

        var other = await app.LoginAsync("lethu.design@eduviet.local");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/instructor/quizzes/{quizId}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await instructor.DeleteAsync($"/api/instructor/quizzes/{quizId}")).StatusCode);

        var overview = await (await instructor.GetAsync("/api/instructor/overview")).OkJsonAsync();
        Assert.Equal(12, overview.GetProperty("monthly").GetArrayLength());
        Assert.True(overview.GetProperty("totalRevenue").GetDecimal() > 0);
        var students = await (await instructor.GetAsync("/api/instructor/students")).OkJsonAsync();
        Assert.True(students.GetProperty("totalCount").GetInt32() > 0);
    }
}
