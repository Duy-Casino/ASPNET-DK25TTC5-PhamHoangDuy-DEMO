using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Dashboards;

// ---------- Học viên ----------
public sealed record StudentSummaryDto(
    int TotalCourses, int InProgress, int Completed, int NotStarted, int Certificates, int QuizzesPassed,
    decimal TotalSpent, int CompletedLessons, int LearningSecondsCompleted);

public sealed record CertificateListItemDto(Guid Id, string CertificateCode, string CourseTitle, string CourseSlug, DateTime IssuedAt, decimal? FinalScore, string? PdfUrl);

// ---------- Giảng viên ----------
public sealed record InstructorMonthlyDto(string Month, int Enrollments, decimal Revenue);
public sealed record InstructorCourseStatDto(Guid CourseId, string Title, CourseStatus Status, int Enrollments, decimal Revenue, decimal AverageRating, int ReviewCount, decimal AverageProgress);
public sealed record InstructorOverviewDto(
    int TotalCourses, int PublishedCourses, int PendingCourses, int TotalStudents, int EnrollmentsLast30Days,
    decimal TotalRevenue, decimal RevenueLast30Days, decimal AverageRating, int TotalReviews,
    IReadOnlyList<InstructorMonthlyDto> Monthly, IReadOnlyList<InstructorCourseStatDto> Courses);

public sealed record InstructorStudentDto(Guid UserId, string FullName, string Email, string? AvatarUrl, Guid CourseId, string CourseTitle,
    decimal ProgressPercent, EnrollmentStatus Status, DateTime EnrolledAt, DateTime? LastAccessedAt);

public sealed class InstructorStudentQuery : PagedQuery
{
    public Guid? CourseId { get; set; }
    public string? Keyword { get; set; }
}

public interface IDashboardService
{
    Task<StudentSummaryDto> GetStudentSummaryAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<CertificateListItemDto>> GetMyCertificatesAsync(Guid userId, CancellationToken ct = default);
    Task<InstructorOverviewDto> GetInstructorOverviewAsync(Guid instructorId, CancellationToken ct = default);
    Task<PagedResult<InstructorStudentDto>> GetInstructorStudentsAsync(Guid instructorId, InstructorStudentQuery query, CancellationToken ct = default);
}

public sealed class DashboardService(IAppDbContext db, IDateTimeProvider clock) : IDashboardService
{
    public async Task<StudentSummaryDto> GetStudentSummaryAsync(Guid userId, CancellationToken ct = default)
    {
        var enrollments = await db.Enrollments.AsNoTracking()
            .Where(e => e.UserId == userId && e.Status != EnrollmentStatus.Revoked)
            .Select(e => new { e.Status, Started = e.LessonProgresses.Any(p => p.IsCompleted) })
            .ToListAsync(ct);

        var completedLessons = await db.LessonProgress.AsNoTracking()
            .Where(p => p.Enrollment.UserId == userId && p.IsCompleted)
            .Select(p => p.Lesson.DurationSeconds).ToListAsync(ct);

        var spent = await db.Orders.AsNoTracking().Where(o => o.UserId == userId && o.Status == OrderStatus.Paid)
            .Select(o => o.TotalAmount).ToListAsync(ct);

        return new StudentSummaryDto(
            enrollments.Count,
            enrollments.Count(e => e.Status == EnrollmentStatus.Active && e.Started),
            enrollments.Count(e => e.Status == EnrollmentStatus.Completed),
            enrollments.Count(e => !e.Started && e.Status == EnrollmentStatus.Active),
            await db.Certificates.CountAsync(c => c.UserId == userId && !c.IsRevoked, ct),
            await db.QuizAttempts.Where(a => a.UserId == userId && a.IsPassed).Select(a => a.QuizId).Distinct().CountAsync(ct),
            spent.Sum(), completedLessons.Count, completedLessons.Sum());
    }

    public async Task<IReadOnlyList<CertificateListItemDto>> GetMyCertificatesAsync(Guid userId, CancellationToken ct = default) =>
        await db.Certificates.AsNoTracking()
            .Where(c => c.UserId == userId && !c.IsRevoked)
            .OrderByDescending(c => c.IssuedAt)
            .Select(c => new CertificateListItemDto(c.Id, c.CertificateCode, c.CourseTitle, c.Course.Slug, c.IssuedAt, c.FinalScore, c.PdfUrl))
            .ToListAsync(ct);

    public async Task<InstructorOverviewDto> GetInstructorOverviewAsync(Guid instructorId, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var last30 = now.AddDays(-30);
        var from12 = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);

        var courses = await db.Courses.AsNoTracking()
            .Where(c => c.InstructorId == instructorId && !c.IsDeleted)
            .Select(c => new
            {
                c.Id, c.Title, c.Status, c.AverageRating, c.ReviewCount,
                Enrollments = c.Enrollments.Count,
                Progress = c.Enrollments.Select(e => e.ProgressPercent).ToList(),
            })
            .ToListAsync(ct);

        // Doanh thu = FinalPrice của các dòng đơn đã thanh toán thuộc khóa học của giảng viên (BR-12)
        var sales = await db.OrderItems.AsNoTracking()
            .Where(i => i.InstructorId == instructorId && i.Order.Status == OrderStatus.Paid)
            .Select(i => new { i.CourseId, i.FinalPrice, PaidAt = i.Order.PaidAt!.Value })
            .ToListAsync(ct);
        var enrollmentDates = await db.Enrollments.AsNoTracking()
            .Where(e => e.Course.InstructorId == instructorId)
            .Select(e => new { e.UserId, e.EnrolledAt })
            .ToListAsync(ct);

        var monthly = Enumerable.Range(0, 12).Select(i =>
        {
            var m = from12.AddMonths(i);
            bool InMonth(DateTime d) => d.Year == m.Year && d.Month == m.Month;
            return new InstructorMonthlyDto($"{m:yyyy-MM}",
                enrollmentDates.Count(e => InMonth(e.EnrolledAt)),
                sales.Where(s => InMonth(s.PaidAt)).Sum(s => s.FinalPrice));
        }).ToList();

        var reviewTotal = courses.Sum(c => c.ReviewCount);
        var avgRating = reviewTotal == 0 ? 0 : Math.Round(courses.Sum(c => c.AverageRating * c.ReviewCount) / reviewTotal, 2);

        return new InstructorOverviewDto(
            courses.Count,
            courses.Count(c => c.Status == CourseStatus.Published),
            courses.Count(c => c.Status == CourseStatus.Pending),
            enrollmentDates.Select(e => e.UserId).Distinct().Count(),
            enrollmentDates.Count(e => e.EnrolledAt >= last30),
            sales.Sum(s => s.FinalPrice),
            sales.Where(s => s.PaidAt >= last30).Sum(s => s.FinalPrice),
            avgRating, reviewTotal, monthly,
            courses.Select(c => new InstructorCourseStatDto(c.Id, c.Title, c.Status, c.Enrollments,
                sales.Where(s => s.CourseId == c.Id).Sum(s => s.FinalPrice), c.AverageRating, c.ReviewCount,
                c.Progress.Count == 0 ? 0 : Math.Round(c.Progress.Average(), 1)))
                .OrderByDescending(c => c.Revenue).ToList());
    }

    public Task<PagedResult<InstructorStudentDto>> GetInstructorStudentsAsync(Guid instructorId, InstructorStudentQuery q, CancellationToken ct = default)
    {
        var query = db.Enrollments.AsNoTracking().Where(e => e.Course.InstructorId == instructorId);
        if (q.CourseId is { } cid) query = query.Where(e => e.CourseId == cid);
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(e => e.User.FullName.Contains(kw) || e.User.Email!.Contains(kw));
        }

        return query.OrderByDescending(e => e.EnrolledAt)
            .Select(e => new InstructorStudentDto(e.UserId, e.User.FullName, e.User.Email!, e.User.AvatarUrl, e.CourseId, e.Course.Title,
                e.ProgressPercent, e.Status, e.EnrolledAt, e.LastAccessedAt))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);
    }
}
