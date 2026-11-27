using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineLearning.Application.Features.Learning;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.IntegrationTests;

/// <summary>Tiện ích cho người học: video mẫu, hỏi đáp theo bài, ghi chú theo mốc thời gian, nhắc học.</summary>
[Collection(nameof(ApiCollection))]
public sealed class LearnerToolsTests(TestApp app)
{
    private const string FreeCourse = "ky-nang-thuyet-trinh-chuyen-nghiep";   // của Lê Anh Thư

    private async Task<(HttpClient Client, Guid UserId, string Email)> EnrolledStudentAsync()
    {
        var email = $"lt{Guid.NewGuid():N}@test.local";
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var reg = await (await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Học Viên Hỏi Bài", email, password = "Matkhau123" })).OkJsonAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reg.GetProperty("accessToken").GetString());
        var courseId = app.Db(db => db.Courses.First(c => c.Slug == FreeCourse).Id);
        (await client.PostAsJsonAsync("/api/orders", new { courseIds = new[] { courseId }, fromCart = false })).EnsureSuccessStatusCode();   // khóa 0đ: ghi danh ngay
        return (client, reg.GetProperty("user").GetProperty("id").GetGuid(), email);
    }

    private Guid FirstLessonId() => app.Db(db => db.Lessons.Where(l => l.Section.Course.Slug == FreeCourse)
        .OrderBy(l => l.Section.SortOrder).ThenBy(l => l.SortOrder).First().Id);

    [Fact]
    public void Sample_lessons_use_the_bundled_video_instead_of_the_blocked_external_url()
    {
        app.Db(db =>
        {
            Assert.False(db.Lessons.Any(l => l.VideoUrl != null && l.VideoUrl.Contains("gtv-videos-bucket")));
            Assert.True(db.Lessons.Any(l => l.VideoUrl == "/videos/demo/bai-giang-mau.mp4"));
            return 0;
        });
    }

    [Fact]
    public async Task Lesson_qa_is_shared_with_classmates_and_instructor_and_notifies_both_ways()
    {
        var (asker, askerId, _) = await EnrolledStudentAsync();
        var (classmate, _, _) = await EnrolledStudentAsync();
        var lessonId = FirstLessonId();

        // Người chưa mua không xem, không hỏi được
        var outsider = await app.LoginAsync("hoangnam@eduviet.local");
        var enrolledOutsider = app.Db(db => db.Enrollments.Any(e => e.User.Email == "hoangnam@eduviet.local" && e.Course.Slug == FreeCourse));
        if (!enrolledOutsider)
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/learning/lessons/{lessonId}/questions")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await asker.PostAsJsonAsync($"/api/learning/lessons/{lessonId}/questions", new { content = "Hỏi" })).StatusCode);
        var q = await (await asker.PostAsJsonAsync($"/api/learning/lessons/{lessonId}/questions",
            new { content = "Mục tiêu thuyết trình nên viết ngắn gọn thế nào?" })).OkJsonAsync();
        var questionId = q.GetProperty("id").GetGuid();
        var instructorId = app.Db(db => db.Courses.Where(c => c.Slug == FreeCourse).Select(c => c.InstructorId).First());
        Assert.True(app.Db(db => db.Notifications.Any(n => n.UserId == instructorId && n.Title == "Câu hỏi mới trong bài học")));

        // Bạn cùng khóa thấy câu hỏi và trả lời được
        var list = await (await classmate.GetAsync($"/api/learning/lessons/{lessonId}/questions")).OkJsonAsync();
        var seen = list.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == questionId);
        Assert.False(seen.GetProperty("isMine").GetBoolean());
        Assert.False(seen.GetProperty("canModerate").GetBoolean());
        (await classmate.PostAsJsonAsync($"/api/learning/questions/{questionId}/answers", new { content = "Mình viết trong một câu, có số liệu cụ thể." })).EnsureSuccessStatusCode();
        // Không phải người hỏi/giảng viên thì không đánh dấu, không xóa được
        Assert.Equal(HttpStatusCode.Forbidden, (await classmate.PutAsJsonAsync($"/api/learning/questions/{questionId}/resolved", new { isResolved = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await classmate.DeleteAsync($"/api/learning/questions/{questionId}")).StatusCode);

        // Giảng viên trả lời → câu trả lời được đánh dấu của giảng viên, người hỏi nhận thông báo
        var instructor = await app.LoginAsync("lethu.design@eduviet.local");
        var inbox = await (await instructor.GetAsync("/api/instructor/questions?unanswered=true&pageSize=100")).OkJsonAsync();
        Assert.Contains(inbox.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == questionId);
        var answer = await (await instructor.PostAsJsonAsync($"/api/learning/questions/{questionId}/answers", new { content = "Một câu, có động từ hành động và kết quả đo được." })).OkJsonAsync();
        Assert.True(answer.GetProperty("isInstructor").GetBoolean());
        Assert.True(app.Db(db => db.Notifications.Any(n => n.UserId == askerId && n.Title == "Giảng viên đã trả lời câu hỏi của bạn")));
        var after = await (await instructor.GetAsync("/api/instructor/questions?unanswered=true&pageSize=100")).OkJsonAsync();
        Assert.DoesNotContain(after.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == questionId);

        // Người hỏi đánh dấu đã giải đáp; danh sách có đủ 2 câu trả lời theo thứ tự
        (await asker.PutAsJsonAsync($"/api/learning/questions/{questionId}/resolved", new { isResolved = true })).EnsureSuccessStatusCode();
        var final = await (await asker.GetAsync($"/api/learning/lessons/{lessonId}/questions")).OkJsonAsync();
        var mine = final.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == questionId);
        Assert.True(mine.GetProperty("isResolved").GetBoolean());
        Assert.Equal(2, mine.GetProperty("answers").GetArrayLength());
        Assert.True(mine.GetProperty("answers")[1].GetProperty("isInstructor").GetBoolean());

        // Giảng viên khóa khác không thấy câu hỏi trong hộp của mình
        var otherInstructor = await app.LoginAsync("phamquang.data@eduviet.local");
        var otherInbox = await (await otherInstructor.GetAsync("/api/instructor/questions?pageSize=100")).OkJsonAsync();
        Assert.DoesNotContain(otherInbox.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == questionId);
    }

    [Fact]
    public async Task Notes_are_private_sorted_by_video_time_and_editable_by_owner_only()
    {
        var (student, _, _) = await EnrolledStudentAsync();
        var (other, _, _) = await EnrolledStudentAsync();
        var lessonId = FirstLessonId();

        var late = await (await student.PostAsJsonAsync($"/api/learning/lessons/{lessonId}/notes", new { content = "Ví dụ mở đầu bằng câu hỏi", positionSeconds = 95 })).OkJsonAsync();
        (await student.PostAsJsonAsync($"/api/learning/lessons/{lessonId}/notes", new { content = "Quy tắc 10-20-30", positionSeconds = 12 })).EnsureSuccessStatusCode();
        (await student.PostAsJsonAsync($"/api/learning/lessons/{lessonId}/notes", new { content = "Ghi chú chung cho bài", positionSeconds = (int?)null })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await student.PostAsJsonAsync($"/api/learning/lessons/{lessonId}/notes", new { content = "", positionSeconds = 1 })).StatusCode);

        var notes = await (await student.GetAsync($"/api/learning/lessons/{lessonId}/notes")).OkJsonAsync();
        Assert.Equal(new int?[] { 12, 95, null }, notes.EnumerateArray().Select(n => n.GetProperty("positionSeconds").ValueKind == System.Text.Json.JsonValueKind.Null ? (int?)null : n.GetProperty("positionSeconds").GetInt32()));

        // Người khác không thấy và không sửa/xóa được ghi chú
        Assert.Equal(0, (await (await other.GetAsync($"/api/learning/lessons/{lessonId}/notes")).OkJsonAsync()).GetArrayLength());
        var noteId = late.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/learning/notes/{noteId}", new { content = "Sửa trộm", positionSeconds = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/learning/notes/{noteId}")).StatusCode);

        var updated = await (await student.PutAsJsonAsync($"/api/learning/notes/{noteId}", new { content = "Mở đầu bằng câu hỏi gây tò mò", positionSeconds = 96 })).OkJsonAsync();
        Assert.Equal(96, updated.GetProperty("positionSeconds").GetInt32());
        (await student.DeleteAsync($"/api/learning/notes/{noteId}")).EnsureSuccessStatusCode();
        Assert.Equal(2, (await (await student.GetAsync($"/api/learning/lessons/{lessonId}/notes")).OkJsonAsync()).GetArrayLength());
    }

    [Fact]
    public async Task Study_reminder_is_sent_once_per_inactive_period_and_respects_opt_out()
    {
        var (student, studentId, email) = await EnrolledStudentAsync();
        var (quiet, quietId, quietEmail) = await EnrolledStudentAsync();
        (await quiet.PutAsJsonAsync("/api/account/reminder-settings", new { reminderEmailsEnabled = false })).EnsureSuccessStatusCode();
        Assert.False((await (await quiet.GetAsync("/api/account/reminder-settings")).OkJsonAsync()).GetProperty("reminderEmailsEnabled").GetBoolean());

        int Sent(string to) => app.Emails.All.Count(m => m.To == to && m.Subject.StartsWith("Học tiếp"));
        async Task RunAsync()
        {
            using var scope = app.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IStudyReminderService>().SendDueRemindersAsync();
        }

        try
        {
            await RunAsync();
            Assert.Equal(0, Sent(email));   // mới ghi danh

            app.Clock.Offset = TimeSpan.FromDays(8);
            await RunAsync();
            Assert.Equal(1, Sent(email));
            Assert.Equal(0, Sent(quietEmail));   // đã tắt email nhắc nhở
            Assert.Contains("ky-nang-thuyet-trinh-chuyen-nghiep", app.Emails.All.Last(m => m.To == email).Body);
            Assert.True(app.Db(db => db.Notifications.Any(n => n.UserId == studentId && n.Title == "Học tiếp nhé!")));
            await RunAsync();
            Assert.Equal(1, Sent(email));   // cùng một đợt bỏ dở: không nhắc lại

            // Quay lại học rồi lại bỏ dở 8 ngày → nhắc lần hai
            (await student.GetAsync($"/api/learning/lessons/{FirstLessonId()}")).EnsureSuccessStatusCode();
            app.Clock.Offset = TimeSpan.FromDays(17);
            await RunAsync();
            Assert.Equal(2, Sent(email));
        }
        finally { app.Clock.Offset = TimeSpan.Zero; }
        _ = quietId;
    }
}
