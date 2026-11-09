using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace OnlineLearning.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class InstructorTests(TestApp app)
{
    private Guid UserId(string email) => app.Db(db => db.Users.First(u => u.Email == email).Id);

    [Fact]
    public async Task Public_profile_has_experience_stats_and_verified_flag()
    {
        var id = UserId("giangvien@eduviet.local");
        var p = await (await app.Anonymous().GetAsync($"/api/instructors/{id}")).OkJsonAsync();

        Assert.Equal("Nguyễn Minh Khoa", p.GetProperty("fullName").GetString());
        Assert.True(p.GetProperty("isVerified").GetBoolean());
        Assert.Equal(10, p.GetProperty("yearsOfExperience").GetInt32());
        Assert.Contains(p.GetProperty("expertise").EnumerateArray(), e => e.GetString() == "ASP.NET Core");
        Assert.True(p.GetProperty("experiences").GetArrayLength() >= 3);
        Assert.True(p.GetProperty("totalCourses").GetInt32() > 0);
        Assert.Equal(5, p.GetProperty("ratingDistribution").EnumerateObject().Count());
        // Chỉ 1 cuộc trao đổi mẫu → chưa đủ để công bố tỷ lệ phản hồi
        Assert.Equal(System.Text.Json.JsonValueKind.Null, p.GetProperty("response").ValueKind);

        // Học viên (không phải giảng viên) không có trang hồ sơ giảng viên
        var student = UserId("hocvien@eduviet.local");
        Assert.Equal(HttpStatusCode.NotFound, (await app.Anonymous().GetAsync($"/api/instructors/{student}")).StatusCode);
    }

    [Fact]
    public async Task Instructor_updates_own_profile_and_validation_rejects_bad_input()
    {
        var client = await app.LoginAsync("lethu.design@eduviet.local");

        var bad = await client.PutAsJsonAsync("/api/instructor/profile", new
        {
            yearsOfExperience = 99,
            linkedInUrl = "javascript:alert(1)",
            experiences = new[] { new { type = "Work", title = "", startYear = 2020, endYear = 2010 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var ok = await (await client.PutAsJsonAsync("/api/instructor/profile", new
        {
            headline = "Product Designer",
            bio = "Thiết kế sản phẩm số.",
            yearsOfExperience = 7,
            expertise = new[] { "Figma", " figma ", "UX Writing" },
            linkedInUrl = "https://www.linkedin.com/in/example",
            experiences = new object[]
            {
                new { type = "Work", title = "Product Designer", organization = "Công ty A", startYear = 2021 },
                new { type = "Certification", title = "Google UX Design", organization = "Google", startYear = 2022, credentialUrl = "https://example.com/cert" },
            },
        })).OkJsonAsync();

        Assert.Equal(7, ok.GetProperty("yearsOfExperience").GetInt32());
        // Trùng lặp không phân biệt hoa thường được gộp
        Assert.Equal(["Figma", "UX Writing"], ok.GetProperty("expertise").EnumerateArray().Select(e => e.GetString()!).ToArray());
        Assert.Equal(2, ok.GetProperty("experiences").GetArrayLength());
        Assert.Equal("https://www.linkedin.com/in/example", ok.GetProperty("links").GetProperty("linkedIn").GetString());

        // Học viên không gọi được API hồ sơ giảng viên
        var student = await app.LoginAsync("minhtuan@eduviet.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/instructor/profile")).StatusCode);
    }

    [Fact]
    public async Task Demo_instructors_have_avatar_and_avatar_path_is_restricted()
    {
        var id = UserId("phamquang.data@eduviet.local");
        var p = await (await app.Anonymous().GetAsync($"/api/instructors/{id}")).OkJsonAsync();
        Assert.Equal("/avatars/demo/pham-quang-huy.svg", p.GetProperty("avatarUrl").GetString());

        var client = await app.LoginAsync("phamquang.data@eduviet.local");
        foreach (var bad in new[] { "/avatars/khac/x.svg", "/avatars/demo/../../secret", "http://example.com/a.png", "javascript:alert(1)" })
        {
            var res = await client.PutAsJsonAsync("/api/instructor/profile", new { avatarUrl = bad });
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }
    }

    [Fact]
    public async Task Student_and_instructor_exchange_messages_with_unread_and_notifications()
    {
        var instructorId = UserId("phamquang.data@eduviet.local");
        var courseId = app.Db(db => db.Courses.First(c => c.InstructorId == instructorId).Id);
        var student = await app.LoginAsync("baoanh@eduviet.local");
        var instructor = await app.LoginAsync("phamquang.data@eduviet.local");

        // Không tự nhắn cho mình; không nhắn cho người không phải giảng viên
        var self = await instructor.PostAsJsonAsync("/api/conversations", new { instructorId, message = "test" });
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
        var notInstructor = await student.PostAsJsonAsync("/api/conversations", new { instructorId = UserId("hoangnam@eduviet.local"), message = "Chào" });
        Assert.Equal(HttpStatusCode.NotFound, notInstructor.StatusCode);
        var empty = await student.PostAsJsonAsync("/api/conversations", new { instructorId, message = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var unreadBefore = (await (await instructor.GetAsync("/api/conversations/unread-count")).OkJsonAsync()).GetProperty("count").GetInt32();

        var started = await (await student.PostAsJsonAsync("/api/conversations",
            new { instructorId, courseId, message = "Thầy ơi, khóa Excel có dạy Power Query không ạ?" })).OkJsonAsync();
        var conversationId = started.GetProperty("id").GetGuid();
        Assert.Equal(courseId, started.GetProperty("course").GetProperty("id").GetGuid());
        Assert.Single(started.GetProperty("messages").EnumerateArray());

        // Giảng viên thấy chưa đọc + nhận thông báo dẫn tới hộp thư giảng dạy
        var unreadAfter = (await (await instructor.GetAsync("/api/conversations/unread-count")).OkJsonAsync()).GetProperty("count").GetInt32();
        Assert.Equal(unreadBefore + 1, unreadAfter);
        var notes = await (await instructor.GetAsync("/api/notifications")).OkJsonAsync();
        Assert.Contains(notes.GetProperty("items").EnumerateArray(),
            n => n.GetProperty("link").GetString() == $"/giang-day/tin-nhan/{conversationId}");

        // Người ngoài không đọc được
        var outsider = await app.LoginAsync("ducthinh@eduviet.local");
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/conversations/{conversationId}")).StatusCode);

        // Giảng viên mở → hết chưa đọc; trả lời
        var opened = await (await instructor.GetAsync($"/api/conversations/{conversationId}")).OkJsonAsync();
        Assert.True(opened.GetProperty("iAmInstructor").GetBoolean());
        Assert.Equal(unreadBefore, (await (await instructor.GetAsync("/api/conversations/unread-count")).OkJsonAsync()).GetProperty("count").GetInt32());
        (await instructor.PostAsJsonAsync($"/api/conversations/{conversationId}/messages", new { body = "Có em nhé, chương 4." })).EnsureSuccessStatusCode();

        // Mở lại từ khóa khác vẫn cùng một cuộc trao đổi
        var again = await (await student.PostAsJsonAsync("/api/conversations", new { instructorId, message = "Em cảm ơn thầy!" })).OkJsonAsync();
        Assert.Equal(conversationId, again.GetProperty("id").GetGuid());
        Assert.Equal(3, again.GetProperty("messages").GetArrayLength());

        var list = await (await student.GetAsync("/api/conversations")).OkJsonAsync();
        var item = list.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == conversationId);
        Assert.Equal("Phạm Quang Huy", item.GetProperty("other").GetProperty("fullName").GetString());
        Assert.True(item.GetProperty("lastMessageIsMine").GetBoolean());
    }

    [Fact]
    public async Task Admin_verifies_only_instructors_and_instructor_is_notified()
    {
        var admin = await app.LoginAsync("admin@eduviet.local");
        var studentId = UserId("ngoclan@eduviet.local");
        var notInstructor = await admin.PutAsJsonAsync($"/api/admin/users/{studentId}/instructor-verified", new { isVerified = true });
        Assert.Equal(HttpStatusCode.Conflict, notInstructor.StatusCode);

        var thu = UserId("lethu.design@eduviet.local");
        var res = await (await admin.PutAsJsonAsync($"/api/admin/users/{thu}/instructor-verified", new { isVerified = true })).OkJsonAsync();
        Assert.True(res.GetProperty("isVerifiedInstructor").GetBoolean());
        Assert.True(app.Db(db => db.Notifications.Any(n => n.UserId == thu && n.Title.Contains("xác minh"))));

        var profile = await (await app.Anonymous().GetAsync($"/api/instructors/{thu}")).OkJsonAsync();
        Assert.True(profile.GetProperty("isVerified").GetBoolean());

        var instructor = await app.LoginAsync("giangvien@eduviet.local");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await instructor.PutAsJsonAsync($"/api/admin/users/{thu}/instructor-verified", new { isVerified = false })).StatusCode);
    }
}
