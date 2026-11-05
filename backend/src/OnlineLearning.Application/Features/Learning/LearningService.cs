using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Learning;

// ---------------- DTOs ----------------

public sealed record MyCourseDto(
    Guid CourseId, string Title, string Slug, string? ThumbnailUrl, string InstructorName,
    decimal ProgressPercent, int CompletedLessons, int TotalLessons, EnrollmentStatus Status,
    Guid? LastLessonId, string? LastLessonTitle, DateTime EnrolledAt, DateTime? LastAccessedAt, DateTime? CompletedAt,
    bool HasCertificate);

public sealed class MyCoursesQuery : PagedQuery
{
    /// <summary>all | in-progress | not-started | completed</summary>
    public string? Filter { get; set; }
}

public sealed record LearnLessonDto(Guid Id, string Title, LessonType Type, int DurationSeconds, bool IsPreview, bool IsRequired, bool IsCompleted);
public sealed record LearnQuizBriefDto(Guid Id, string Title, bool IsRequired, bool IsFinalExam, decimal PassingScore, decimal? BestScore, bool IsPassed, int AttemptsUsed, int? MaxAttempts);
public sealed record LearnSectionDto(Guid Id, string Title, IReadOnlyList<LearnLessonDto> Lessons, IReadOnlyList<LearnQuizBriefDto> Quizzes);

public sealed record LearnCourseDto(
    Guid CourseId, string Title, string Slug, string InstructorName,
    decimal ProgressPercent, int CompletedLessons, int RequiredLessons, EnrollmentStatus Status,
    Guid? ResumeLessonId, bool IsPreviewMode, bool CanGetCertificate, bool HasCertificate,
    IReadOnlyList<LearnSectionDto> Sections, IReadOnlyList<LearnQuizBriefDto> CourseQuizzes);

public sealed record LearnResourceDto(Guid Id, string FileName, string FileUrl, string? ContentType, long FileSize);

public sealed record LessonContentDto(
    Guid Id, Guid SectionId, string SectionTitle, string Title, LessonType Type, string? Content, string? VideoUrl,
    int DurationSeconds, bool IsCompleted, int LastPositionSeconds, IReadOnlyList<LearnResourceDto> Resources,
    Guid? PreviousLessonId, Guid? NextLessonId);

public sealed record ProgressDto(decimal ProgressPercent, int CompletedLessons, int RequiredLessons, EnrollmentStatus Status, bool CourseCompleted, string? CertificateCode = null);
public sealed record SavePositionRequest(int PositionSeconds);

public interface ILearningService
{
    Task<PagedResult<MyCourseDto>> GetMyCoursesAsync(Guid userId, MyCoursesQuery query, CancellationToken ct = default);
    Task<Result<LearnCourseDto>> GetCourseAsync(Guid userId, string slug, CancellationToken ct = default);
    Task<Result<LessonContentDto>> GetLessonAsync(Guid userId, Guid lessonId, CancellationToken ct = default);
    Task<Result<ProgressDto>> SetCompletedAsync(Guid userId, Guid lessonId, bool completed, CancellationToken ct = default);
    Task<Result> SavePositionAsync(Guid userId, Guid lessonId, int positionSeconds, CancellationToken ct = default);
    /// <summary>Tính lại % hoàn thành và trạng thái hoàn thành khóa học (dùng chung với Quiz).</summary>
    Task<ProgressDto> RecalculateAsync(Guid enrollmentId, CancellationToken ct = default);
}

/// <summary>
/// Học trực tuyến: chỉ học viên đã ghi danh (hoặc giảng viên sở hữu / Admin ở chế độ xem trước) mới lấy được nội dung bài học.
/// Bài preview mở cho mọi người đã đăng nhập.
/// </summary>
public sealed class LearningService(
    IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock, IFileStorage storage,
    Certificates.ICertificateService certificates) : ILearningService
{
    private static readonly Error NotEnrolled = Error.Forbidden("Bạn chưa sở hữu khóa học này.");

    public async Task<PagedResult<MyCourseDto>> GetMyCoursesAsync(Guid userId, MyCoursesQuery q, CancellationToken ct = default)
    {
        var query = db.Enrollments.AsNoTracking().Where(e => e.UserId == userId && e.Status != EnrollmentStatus.Revoked);
        query = q.Filter switch
        {
            "completed" => query.Where(e => e.Status == EnrollmentStatus.Completed),
            "in-progress" => query.Where(e => e.Status == EnrollmentStatus.Active && e.LessonProgresses.Any(p => p.IsCompleted)),
            "not-started" => query.Where(e => !e.LessonProgresses.Any(p => p.IsCompleted)),
            _ => query,
        };

        return await query
            .OrderByDescending(e => e.LastAccessedAt ?? e.EnrolledAt)
            .Select(e => new MyCourseDto(
                e.CourseId, e.Course.Title, e.Course.Slug, e.Course.ThumbnailUrl, e.Course.Instructor.FullName,
                e.ProgressPercent, e.LessonProgresses.Count(p => p.IsCompleted), e.Course.TotalLessons, e.Status,
                e.LastLessonId, e.LastLesson != null ? e.LastLesson.Title : null, e.EnrolledAt, e.LastAccessedAt, e.CompletedAt,
                e.Certificate != null && !e.Certificate.IsRevoked))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);
    }

    public async Task<Result<LearnCourseDto>> GetCourseAsync(Guid userId, string slug, CancellationToken ct = default)
    {
        var course = await db.Courses.AsNoTracking()
            .Include(c => c.Instructor)
            .Include(c => c.Sections.OrderBy(s => s.SortOrder)).ThenInclude(s => s.Lessons.OrderBy(l => l.SortOrder))
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Slug == slug, ct);
        if (course is null) return Error.NotFound("Không tìm thấy khóa học.");

        var enrollment = await db.Enrollments.AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == course.Id && e.Status != EnrollmentStatus.Revoked, ct);
        var previewMode = enrollment is null && (course.InstructorId == userId || currentUser.IsInRole(Roles.Admin));
        if (enrollment is null && !previewMode) return NotEnrolled;
        if (course.IsDeleted && enrollment is null) return Error.NotFound("Không tìm thấy khóa học.");

        var completed = enrollment is null
            ? new HashSet<Guid>()
            : (await db.LessonProgress.Where(p => p.EnrollmentId == enrollment.Id && p.IsCompleted).Select(p => p.LessonId).ToListAsync(ct)).ToHashSet();

        var quizzes = await QuizBriefsAsync(course.Id, userId, ct);

        var sections = course.Sections.Select(s => new LearnSectionDto(
            s.Id, s.Title,
            s.Lessons.Select(l => new LearnLessonDto(l.Id, l.Title, l.Type, l.DurationSeconds, l.IsPreview, l.IsRequired, completed.Contains(l.Id))).ToList(),
            quizzes.Where(q => q.SectionId == s.Id).Select(q => q.Dto).ToList())).ToList();

        var allLessons = course.Sections.SelectMany(s => s.Lessons).ToList();
        var resume = enrollment?.LastLessonId
                     ?? allLessons.FirstOrDefault(l => !completed.Contains(l.Id))?.Id
                     ?? allLessons.FirstOrDefault()?.Id;
        var required = allLessons.Count(l => l.IsRequired);
        var hasCert = enrollment is not null && await db.Certificates.AnyAsync(c => c.EnrollmentId == enrollment.Id && !c.IsRevoked, ct);
        var requiredQuizzesPassed = quizzes.Where(q => q.Dto.IsRequired).All(q => q.Dto.IsPassed);

        return new LearnCourseDto(
            course.Id, course.Title, course.Slug, course.Instructor.FullName,
            enrollment?.ProgressPercent ?? 0, allLessons.Count(l => l.IsRequired && completed.Contains(l.Id)), required,
            enrollment?.Status ?? EnrollmentStatus.Active, resume, previewMode,
            enrollment is not null && enrollment.ProgressPercent >= 100 && requiredQuizzesPassed, hasCert,
            sections, quizzes.Where(q => q.SectionId is null).Select(q => q.Dto).ToList());
    }

    public async Task<Result<LessonContentDto>> GetLessonAsync(Guid userId, Guid lessonId, CancellationToken ct = default)
    {
        var lesson = await db.Lessons.AsNoTracking()
            .Include(l => l.Resources)
            .Include(l => l.Section).ThenInclude(s => s.Course)
            .FirstOrDefaultAsync(l => l.Id == lessonId, ct);
        if (lesson is null) return Error.NotFound("Không tìm thấy bài học.");

        var course = lesson.Section.Course;
        var enrollment = await db.Enrollments
            .FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == course.Id && e.Status != EnrollmentStatus.Revoked, ct);
        var canPreview = course.InstructorId == userId || currentUser.IsInRole(Roles.Admin) || (lesson.IsPreview && !course.IsDeleted);
        if (enrollment is null && !canPreview) return NotEnrolled;

        LessonProgress? progress = null;
        if (enrollment is not null)
        {
            progress = await db.LessonProgress.AsNoTracking()
                .FirstOrDefaultAsync(p => p.EnrollmentId == enrollment.Id && p.LessonId == lessonId, ct);
            // FR-LEARN-04: ghi nhớ bài gần nhất để "Tiếp tục học"
            enrollment.LastLessonId = lessonId;
            enrollment.LastAccessedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var ordered = await db.Lessons.AsNoTracking()
            .Where(l => l.Section.CourseId == course.Id)
            .OrderBy(l => l.Section.SortOrder).ThenBy(l => l.SortOrder)
            .Select(l => l.Id).ToListAsync(ct);
        var index = ordered.IndexOf(lessonId);

        return new LessonContentDto(
            // Video/tài liệu riêng tư → URL có chữ ký, hết hạn; chỉ cấp sau khi đã kiểm tra quyền ở trên
            lesson.Id, lesson.SectionId, lesson.Section.Title, lesson.Title, lesson.Type, lesson.Content,
            lesson.VideoPublicId is { Length: > 0 } vid ? storage.GetSignedUrl(vid, StoredFileKind.Video) : lesson.VideoUrl,
            lesson.DurationSeconds, progress?.IsCompleted ?? false, progress?.LastPositionSeconds ?? 0,
            lesson.Resources.Select(r => new LearnResourceDto(r.Id, r.FileName, r.PublicId is { Length: > 0 } pid ? storage.GetSignedUrl(pid, StoredFileKind.Document) : r.FileUrl, r.ContentType, r.FileSize)).ToList(),
            index > 0 ? ordered[index - 1] : null,
            index >= 0 && index < ordered.Count - 1 ? ordered[index + 1] : null);
    }

    public async Task<Result<ProgressDto>> SetCompletedAsync(Guid userId, Guid lessonId, bool completed, CancellationToken ct = default)
    {
        var enrollment = await FindEnrollmentForLessonAsync(userId, lessonId, ct);
        if (enrollment is null) return NotEnrolled;

        var progress = await db.LessonProgress.FirstOrDefaultAsync(p => p.EnrollmentId == enrollment.Id && p.LessonId == lessonId, ct);
        if (progress is null)
        {
            progress = new LessonProgress { EnrollmentId = enrollment.Id, LessonId = lessonId };
            db.LessonProgress.Add(progress);
        }
        progress.IsCompleted = completed;
        progress.CompletedAt = completed ? progress.CompletedAt ?? clock.UtcNow : null;
        progress.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);

        return await RecalculateAsync(enrollment.Id, ct);
    }

    public async Task<Result> SavePositionAsync(Guid userId, Guid lessonId, int positionSeconds, CancellationToken ct = default)
    {
        var enrollment = await FindEnrollmentForLessonAsync(userId, lessonId, ct);
        if (enrollment is null) return Result.Failure(NotEnrolled);

        var progress = await db.LessonProgress.FirstOrDefaultAsync(p => p.EnrollmentId == enrollment.Id && p.LessonId == lessonId, ct);
        if (progress is null)
        {
            progress = new LessonProgress { EnrollmentId = enrollment.Id, LessonId = lessonId };
            db.LessonProgress.Add(progress);
        }
        progress.LastPositionSeconds = Math.Max(0, positionSeconds);
        progress.UpdatedAt = clock.UtcNow;
        enrollment.LastAccessedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<ProgressDto> RecalculateAsync(Guid enrollmentId, CancellationToken ct = default)
    {
        var enrollment = await db.Enrollments.FirstAsync(e => e.Id == enrollmentId, ct);
        var requiredIds = await db.Lessons.Where(l => l.Section.CourseId == enrollment.CourseId && l.IsRequired).Select(l => l.Id).ToListAsync(ct);
        var done = await db.LessonProgress.CountAsync(p => p.EnrollmentId == enrollmentId && p.IsCompleted && requiredIds.Contains(p.LessonId), ct);

        enrollment.ProgressPercent = requiredIds.Count == 0 ? 100 : Math.Round(done * 100m / requiredIds.Count, 2);

        // Hoàn thành khóa học = 100% bài bắt buộc + đạt mọi quiz bắt buộc (BR chứng chỉ, Giai đoạn 6 cấp PDF)
        var requiredQuizIds = await db.Quizzes.Where(q => q.CourseId == enrollment.CourseId && q.IsRequired).Select(q => q.Id).ToListAsync(ct);
        var passedQuizIds = await db.QuizAttempts
            .Where(a => a.UserId == enrollment.UserId && a.IsPassed && requiredQuizIds.Contains(a.QuizId))
            .Select(a => a.QuizId).Distinct().ToListAsync(ct);
        var completed = enrollment.ProgressPercent >= 100 && requiredQuizIds.All(passedQuizIds.Contains);

        if (completed && enrollment.Status == EnrollmentStatus.Active)
        {
            enrollment.Status = EnrollmentStatus.Completed;
            enrollment.CompletedAt = clock.UtcNow;
        }
        else if (!completed && enrollment.Status == EnrollmentStatus.Completed)
        {
            enrollment.Status = EnrollmentStatus.Active;
            enrollment.CompletedAt = null;
        }

        await db.SaveChangesAsync(ct);

        // FR-CERT-02: hoàn thành thì cấp chứng chỉ ngay (idempotent)
        string? certificateCode = null;
        if (completed)
            certificateCode = (await certificates.IssueIfEligibleAsync(enrollment.Id, ct))?.CertificateCode;

        return new ProgressDto(enrollment.ProgressPercent, done, requiredIds.Count, enrollment.Status, completed, certificateCode);
    }

    private Task<Enrollment?> FindEnrollmentForLessonAsync(Guid userId, Guid lessonId, CancellationToken ct) =>
        db.Enrollments.FirstOrDefaultAsync(e => e.UserId == userId && e.Status != EnrollmentStatus.Revoked
            && e.Course.Sections.Any(s => s.Lessons.Any(l => l.Id == lessonId)), ct);

    private async Task<List<(Guid? SectionId, LearnQuizBriefDto Dto)>> QuizBriefsAsync(Guid courseId, Guid userId, CancellationToken ct)
    {
        var quizzes = await db.Quizzes.AsNoTracking().Where(q => q.CourseId == courseId)
            .OrderBy(q => q.SortOrder)
            .Select(q => new
            {
                q.Id, q.SectionId, q.Title, q.IsRequired, q.IsFinalExam, q.PassingScore, q.MaxAttempts,
                Attempts = q.Attempts.Where(a => a.UserId == userId && a.SubmittedAt != null).Select(a => new { a.ScorePercent, a.IsPassed }).ToList(),
            })
            .ToListAsync(ct);

        return quizzes.Select(q => (q.SectionId, new LearnQuizBriefDto(q.Id, q.Title, q.IsRequired, q.IsFinalExam, q.PassingScore,
            q.Attempts.Count == 0 ? null : q.Attempts.Max(a => a.ScorePercent), q.Attempts.Any(a => a.IsPassed),
            q.Attempts.Count, q.MaxAttempts))).ToList();
    }
}
