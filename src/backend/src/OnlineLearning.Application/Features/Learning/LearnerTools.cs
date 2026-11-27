using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Common.Options;
using OnlineLearning.Application.Features.Notifications;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Learning;

// =====================================================================
// DTO
// =====================================================================
public sealed record QaAuthorDto(Guid Id, string FullName, string? AvatarUrl);
public sealed record LessonAnswerDto(Guid Id, string Content, QaAuthorDto Author, bool IsInstructor, bool IsMine, DateTime CreatedAt);
public sealed record LessonQuestionDto(Guid Id, string Content, QaAuthorDto Author, bool IsMine, bool IsResolved, DateTime CreatedAt,
    IReadOnlyList<LessonAnswerDto> Answers, bool CanModerate);
public sealed record InstructorQuestionDto(Guid Id, Guid CourseId, string CourseTitle, string CourseSlug, Guid LessonId, string LessonTitle,
    string Content, string AuthorName, int AnswerCount, bool HasInstructorAnswer, bool IsResolved, DateTime CreatedAt, DateTime LastActivityAt);
public sealed record LessonNoteDto(Guid Id, Guid LessonId, int? PositionSeconds, string Content, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record PostContentRequest(string Content);
public sealed record SaveNoteRequest(string Content, int? PositionSeconds);
public sealed record SetResolvedRequest(bool IsResolved);
public sealed record ReminderSettingsDto(bool ReminderEmailsEnabled);

public sealed class InstructorQuestionQuery : PagedQuery
{
    /// <summary>true: chỉ câu hỏi chưa có giảng viên trả lời và chưa được đánh dấu đã giải đáp.</summary>
    public bool? Unanswered { get; set; }
    public Guid? CourseId { get; set; }
}

public sealed class PostContentRequestValidator : AbstractValidator<PostContentRequest>
{
    public PostContentRequestValidator() =>
        RuleFor(x => x.Content).Must(c => !string.IsNullOrWhiteSpace(c) && c.Trim().Length >= 5)
            .WithMessage("Nội dung tối thiểu 5 ký tự.").MaximumLength(2000).WithMessage("Nội dung tối đa 2.000 ký tự.");
}

public sealed class SaveNoteRequestValidator : AbstractValidator<SaveNoteRequest>
{
    public SaveNoteRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().WithMessage("Nhập nội dung ghi chú.").MaximumLength(2000).WithMessage("Ghi chú tối đa 2.000 ký tự.");
        RuleFor(x => x.PositionSeconds).InclusiveBetween(0, 24 * 3600).When(x => x.PositionSeconds.HasValue);
    }
}

// =====================================================================
// Hỏi đáp và ghi chú theo bài học
// =====================================================================
public interface ILessonQaService
{
    Task<Result<PagedResult<LessonQuestionDto>>> ListAsync(Guid userId, Guid lessonId, PagedQuery query, CancellationToken ct = default);
    Task<Result<LessonQuestionDto>> AskAsync(Guid userId, Guid lessonId, PostContentRequest request, CancellationToken ct = default);
    Task<Result<LessonAnswerDto>> AnswerAsync(Guid userId, Guid questionId, PostContentRequest request, CancellationToken ct = default);
    Task<Result> SetResolvedAsync(Guid userId, Guid questionId, bool resolved, CancellationToken ct = default);
    Task<Result> DeleteQuestionAsync(Guid userId, Guid questionId, CancellationToken ct = default);
    Task<Result> DeleteAnswerAsync(Guid userId, Guid answerId, CancellationToken ct = default);
    Task<PagedResult<InstructorQuestionDto>> ListForInstructorAsync(Guid instructorId, bool isAdmin, InstructorQuestionQuery query, CancellationToken ct = default);

    Task<Result<IReadOnlyList<LessonNoteDto>>> ListNotesAsync(Guid userId, Guid lessonId, CancellationToken ct = default);
    Task<Result<LessonNoteDto>> AddNoteAsync(Guid userId, Guid lessonId, SaveNoteRequest request, CancellationToken ct = default);
    Task<Result<LessonNoteDto>> UpdateNoteAsync(Guid userId, Guid noteId, SaveNoteRequest request, CancellationToken ct = default);
    Task<Result> DeleteNoteAsync(Guid userId, Guid noteId, CancellationToken ct = default);
}

public sealed class LessonQaService(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock, INotificationService notifications)
    : ILessonQaService
{
    private static readonly Error NoAccess = Error.Forbidden("Bạn cần sở hữu khóa học để tham gia hỏi đáp.");

    private sealed record Access(Guid LessonId, string LessonTitle, Guid CourseId, string CourseSlug, string CourseTitle, Guid InstructorId, bool IsStaff);

    /// <summary>Học viên đã ghi danh (chưa bị thu hồi), giảng viên của khóa hoặc Admin.</summary>
    private async Task<Result<Access>> AccessAsync(Guid userId, Guid lessonId, CancellationToken ct)
    {
        var l = await db.Lessons.AsNoTracking().Where(x => x.Id == lessonId)
            .Select(x => new { x.Id, x.Title, x.Section.CourseId, x.Section.Course.Slug, CourseTitle = x.Section.Course.Title, x.Section.Course.InstructorId })
            .FirstOrDefaultAsync(ct);
        if (l is null) return Error.NotFound("Không tìm thấy bài học.");
        var staff = l.InstructorId == userId || currentUser.IsInRole(Roles.Admin);
        if (!staff && !await db.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseId == l.CourseId && e.Status != EnrollmentStatus.Revoked, ct))
            return NoAccess;
        return new Access(l.Id, l.Title, l.CourseId, l.Slug, l.CourseTitle, l.InstructorId, staff);
    }

    private static string LessonLink(Access a, string tab) => $"/hoc/{a.CourseSlug}/bai/{a.LessonId}?tab={tab}";

    public async Task<Result<PagedResult<LessonQuestionDto>>> ListAsync(Guid userId, Guid lessonId, PagedQuery q, CancellationToken ct = default)
    {
        var access = await AccessAsync(userId, lessonId, ct);
        if (access.IsFailure) return access.Error!;
        var staff = access.Value!.IsStaff;
        return await db.LessonQuestions.AsNoTracking().Where(x => x.LessonId == lessonId)
            .OrderBy(x => x.IsResolved).ThenByDescending(x => x.LastActivityAt)
            .Select(x => new LessonQuestionDto(x.Id, x.Content, new QaAuthorDto(x.UserId, x.User.FullName, x.User.AvatarUrl),
                x.UserId == userId, x.IsResolved, x.CreatedAt,
                x.Answers.OrderBy(a => a.CreatedAt).Select(a => new LessonAnswerDto(a.Id, a.Content,
                    new QaAuthorDto(a.UserId, a.User.FullName, a.User.AvatarUrl), a.IsInstructor, a.UserId == userId, a.CreatedAt)).ToList(),
                staff || x.UserId == userId))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);
    }

    public async Task<Result<LessonQuestionDto>> AskAsync(Guid userId, Guid lessonId, PostContentRequest r, CancellationToken ct = default)
    {
        var access = await AccessAsync(userId, lessonId, ct);
        if (access.IsFailure) return access.Error!;
        var a = access.Value!;
        var now = clock.UtcNow;
        var q = new LessonQuestion { LessonId = lessonId, CourseId = a.CourseId, UserId = userId, Content = r.Content.Trim(), LastActivityAt = now };
        db.LessonQuestions.Add(q);
        if (a.InstructorId != userId)
            notifications.Add(a.InstructorId, "Câu hỏi mới trong bài học",
                $"Học viên hỏi trong bài \"{a.LessonTitle}\" ({a.CourseTitle}).", NotificationType.Message, LessonLink(a, "hoi-dap"));
        await db.SaveChangesAsync(ct);
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new QaAuthorDto(u.Id, u.FullName, u.AvatarUrl)).FirstAsync(ct);
        return new LessonQuestionDto(q.Id, q.Content, user, true, false, q.CreatedAt, [], true);
    }

    public async Task<Result<LessonAnswerDto>> AnswerAsync(Guid userId, Guid questionId, PostContentRequest r, CancellationToken ct = default)
    {
        var q = await db.LessonQuestions.FirstOrDefaultAsync(x => x.Id == questionId, ct);
        if (q is null) return Error.NotFound("Không tìm thấy câu hỏi.");
        var access = await AccessAsync(userId, q.LessonId, ct);
        if (access.IsFailure) return access.Error!;
        var a = access.Value!;

        var answer = new LessonAnswer { QuestionId = q.Id, UserId = userId, Content = r.Content.Trim(), IsInstructor = a.InstructorId == userId };
        db.LessonAnswers.Add(answer);
        q.LastActivityAt = clock.UtcNow;
        var name = await db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstAsync(ct);
        if (q.UserId != userId)
            notifications.Add(q.UserId, answer.IsInstructor ? "Giảng viên đã trả lời câu hỏi của bạn" : "Có trả lời mới cho câu hỏi của bạn",
                $"{name} đã trả lời câu hỏi trong bài \"{a.LessonTitle}\".", NotificationType.Message, LessonLink(a, "hoi-dap"));
        await db.SaveChangesAsync(ct);
        var author = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new QaAuthorDto(u.Id, u.FullName, u.AvatarUrl)).FirstAsync(ct);
        return new LessonAnswerDto(answer.Id, answer.Content, author, answer.IsInstructor, true, answer.CreatedAt);
    }

    public async Task<Result> SetResolvedAsync(Guid userId, Guid questionId, bool resolved, CancellationToken ct = default)
    {
        var q = await db.LessonQuestions.FirstOrDefaultAsync(x => x.Id == questionId, ct);
        if (q is null) return Result.Failure(Error.NotFound("Không tìm thấy câu hỏi."));
        var access = await AccessAsync(userId, q.LessonId, ct);
        if (access.IsFailure) return Result.Failure(access.Error!);
        if (q.UserId != userId && !access.Value!.IsStaff) return Result.Failure(Error.Forbidden("Chỉ người hỏi hoặc giảng viên được đánh dấu."));
        q.IsResolved = resolved;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteQuestionAsync(Guid userId, Guid questionId, CancellationToken ct = default)
    {
        var q = await db.LessonQuestions.FirstOrDefaultAsync(x => x.Id == questionId, ct);
        if (q is null) return Result.Failure(Error.NotFound("Không tìm thấy câu hỏi."));
        var access = await AccessAsync(userId, q.LessonId, ct);
        if (access.IsFailure) return Result.Failure(access.Error!);
        if (q.UserId != userId && !access.Value!.IsStaff) return Result.Failure(Error.Forbidden());
        db.LessonQuestions.Remove(q);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAnswerAsync(Guid userId, Guid answerId, CancellationToken ct = default)
    {
        var answer = await db.LessonAnswers.Include(x => x.Question).FirstOrDefaultAsync(x => x.Id == answerId, ct);
        if (answer is null) return Result.Failure(Error.NotFound("Không tìm thấy câu trả lời."));
        var access = await AccessAsync(userId, answer.Question.LessonId, ct);
        if (access.IsFailure) return Result.Failure(access.Error!);
        if (answer.UserId != userId && !access.Value!.IsStaff) return Result.Failure(Error.Forbidden());
        db.LessonAnswers.Remove(answer);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public Task<PagedResult<InstructorQuestionDto>> ListForInstructorAsync(Guid instructorId, bool isAdmin, InstructorQuestionQuery q, CancellationToken ct = default)
    {
        var query = db.LessonQuestions.AsNoTracking().Where(x => isAdmin || x.Lesson.Section.Course.InstructorId == instructorId);
        if (q.CourseId is { } cid) query = query.Where(x => x.CourseId == cid);
        if (q.Unanswered == true) query = query.Where(x => !x.IsResolved && !x.Answers.Any(a => a.IsInstructor));
        return query.OrderByDescending(x => x.LastActivityAt)
            .Select(x => new InstructorQuestionDto(x.Id, x.CourseId, x.Lesson.Section.Course.Title, x.Lesson.Section.Course.Slug, x.LessonId, x.Lesson.Title,
                x.Content, x.User.FullName, x.Answers.Count, x.Answers.Any(a => a.IsInstructor), x.IsResolved, x.CreatedAt, x.LastActivityAt))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);
    }

    // ---------- Ghi chú (chỉ chủ ghi chú thấy) ----------
    private static LessonNoteDto ToDto(LessonNote n) => new(n.Id, n.LessonId, n.PositionSeconds, n.Content, n.CreatedAt, n.UpdatedAt);

    public async Task<Result<IReadOnlyList<LessonNoteDto>>> ListNotesAsync(Guid userId, Guid lessonId, CancellationToken ct = default)
    {
        var access = await AccessAsync(userId, lessonId, ct);
        if (access.IsFailure) return access.Error!;
        var notes = await db.LessonNotes.AsNoTracking().Where(n => n.UserId == userId && n.LessonId == lessonId)
            .OrderBy(n => n.PositionSeconds == null).ThenBy(n => n.PositionSeconds).ThenBy(n => n.CreatedAt).ToListAsync(ct);
        return notes.Select(ToDto).ToList();
    }

    public async Task<Result<LessonNoteDto>> AddNoteAsync(Guid userId, Guid lessonId, SaveNoteRequest r, CancellationToken ct = default)
    {
        var access = await AccessAsync(userId, lessonId, ct);
        if (access.IsFailure) return access.Error!;
        if (await db.LessonNotes.CountAsync(n => n.UserId == userId && n.LessonId == lessonId, ct) >= 200)
            return Error.Validation("Mỗi bài học lưu tối đa 200 ghi chú.");
        var note = new LessonNote { UserId = userId, LessonId = lessonId, CourseId = access.Value!.CourseId, PositionSeconds = r.PositionSeconds, Content = r.Content.Trim() };
        db.LessonNotes.Add(note);
        await db.SaveChangesAsync(ct);
        return ToDto(note);
    }

    public async Task<Result<LessonNoteDto>> UpdateNoteAsync(Guid userId, Guid noteId, SaveNoteRequest r, CancellationToken ct = default)
    {
        var note = await db.LessonNotes.FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId, ct);
        if (note is null) return Error.NotFound("Không tìm thấy ghi chú.");
        note.Content = r.Content.Trim();
        note.PositionSeconds = r.PositionSeconds;
        await db.SaveChangesAsync(ct);
        return ToDto(note);
    }

    public async Task<Result> DeleteNoteAsync(Guid userId, Guid noteId, CancellationToken ct = default)
    {
        var deleted = await db.LessonNotes.Where(n => n.Id == noteId && n.UserId == userId).ExecuteDeleteAsync(ct);
        return deleted == 0 ? Result.Failure(Error.NotFound("Không tìm thấy ghi chú.")) : Result.Success();
    }
}

// =====================================================================
// Nhắc học
// =====================================================================
public sealed class StudyReminderOptions
{
    public const string SectionName = "StudyReminder";
    public bool Enabled { get; set; } = true;
    /// <summary>Nhắc khi học viên không vào học khóa đang dở quá số ngày này.</summary>
    public int InactiveDays { get; set; } = 7;
}

public interface IStudyReminderService
{
    /// <summary>Gửi một email (kèm thông báo) cho mỗi học viên có khóa học bỏ dở; mỗi đợt bỏ dở chỉ nhắc một lần. Trả về số email.</summary>
    Task<int> SendDueRemindersAsync(CancellationToken ct = default);
    Task<ReminderSettingsDto> GetSettingsAsync(Guid userId, CancellationToken ct = default);
    Task<ReminderSettingsDto> SetSettingsAsync(Guid userId, bool enabled, CancellationToken ct = default);
}

public sealed class StudyReminderService(
    IAppDbContext db, IEmailSender sender, INotificationService notifications, IDateTimeProvider clock,
    IOptions<StudyReminderOptions> options, IOptions<AppOptions> app, ILogger<StudyReminderService> logger) : IStudyReminderService
{
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);

    public async Task<int> SendDueRemindersAsync(CancellationToken ct = default)
    {
        if (!options.Value.Enabled) return 0;
        var now = clock.UtcNow;
        var cutoff = now.AddDays(-Math.Max(1, options.Value.InactiveDays));

        var due = await db.Enrollments
            .Where(e => e.Status == EnrollmentStatus.Active && e.ProgressPercent < 100
                        && e.Course.Status == CourseStatus.Published && !e.Course.IsDeleted
                        && e.User.IsActive && !e.User.ReminderEmailsDisabled && e.User.Email != null
                        && (e.LastAccessedAt ?? e.EnrolledAt) <= cutoff
                        && (e.StudyReminderSentAt == null || e.StudyReminderSentAt < (e.LastAccessedAt ?? e.EnrolledAt)))
            .OrderBy(e => e.UserId).ThenByDescending(e => e.ProgressPercent)
            .Take(1000)
            .Select(e => new { e.Id, e.UserId, e.User.Email, e.User.FullName, e.Course.Title, e.Course.Slug, e.ProgressPercent })
            .ToListAsync(ct);

        var client = app.Value.ClientUrl.TrimEnd('/');
        var sent = 0;
        foreach (var user in due.GroupBy(x => x.UserId))
        {
            var items = user.ToList();
            var first = items[0];
            var sb = new StringBuilder();
            sb.Append($"<p>Chào {Html.Encode(first.FullName)},</p><p>Đã một thời gian bạn chưa quay lại học. Chỉ cần 15 phút mỗi ngày để giữ nhịp:</p><ul>");
            foreach (var i in items.Take(5))
                sb.Append($"<li><a href=\"{client}/hoc/{i.Slug}\">{Html.Encode(i.Title)}</a> · đã học {Math.Round(i.ProgressPercent):0}%</li>");
            sb.Append($"</ul><p><a href=\"{client}/hoc/{first.Slug}\">Học tiếp ngay</a></p>");
            sb.Append($"<p style=\"color:#64748b;font-size:12px\">Không muốn nhận email nhắc nhở? Tắt tại <a href=\"{client}/tai-khoan\">Hồ sơ cá nhân</a>.</p>");
            try
            {
                await sender.SendAsync(first.Email!, $"Học tiếp \"{first.Title}\" cùng EduViet", sb.ToString(), ct);
                sent++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Không gửi được email nhắc học cho {UserId}", user.Key);
            }
            notifications.Add(user.Key, "Học tiếp nhé!", $"Bạn đang học dở \"{first.Title}\" ({Math.Round(first.ProgressPercent):0}%).",
                NotificationType.Course, $"/hoc/{first.Slug}");
            var ids = items.Select(i => i.Id).ToList();
            await db.Enrollments.Where(e => ids.Contains(e.Id)).ExecuteUpdateAsync(s => s.SetProperty(e => e.StudyReminderSentAt, now), ct);
            await db.SaveChangesAsync(ct);
        }
        if (sent > 0) logger.LogInformation("Đã gửi {Count} email nhắc học", sent);
        return sent;
    }

    public async Task<ReminderSettingsDto> GetSettingsAsync(Guid userId, CancellationToken ct = default) =>
        new(!await db.Users.Where(u => u.Id == userId).Select(u => u.ReminderEmailsDisabled).FirstAsync(ct));

    public async Task<ReminderSettingsDto> SetSettingsAsync(Guid userId, bool enabled, CancellationToken ct = default)
    {
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.ReminderEmailsDisabled, !enabled), ct);
        return new ReminderSettingsDto(enabled);
    }
}
