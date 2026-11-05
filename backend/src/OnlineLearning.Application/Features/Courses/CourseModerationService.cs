using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Notifications;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Courses;

public interface ICourseModerationService
{
    Task<PagedResult<AdminCourseListItemDto>> ListAsync(AdminCourseQuery query, CancellationToken ct = default);
    Task<Result> ApproveAsync(Guid courseId, CancellationToken ct = default);
    Task<Result> RejectAsync(Guid courseId, RejectCourseRequest request, CancellationToken ct = default);
    Task<Result> UnpublishAsync(Guid courseId, CancellationToken ct = default);
    Task<Result> SetFeaturedAsync(Guid courseId, bool isFeatured, CancellationToken ct = default);
}

/// <summary>Admin kiểm duyệt khóa học: Pending → Published / Rejected; gỡ khóa học đã xuất bản.</summary>
public sealed class CourseModerationService(IAppDbContext db, IDateTimeProvider clock, INotificationService notifications) : ICourseModerationService
{
    private static readonly Error NotFound = Error.NotFound("Không tìm thấy khóa học.");

    public Task<PagedResult<AdminCourseListItemDto>> ListAsync(AdminCourseQuery q, CancellationToken ct = default)
    {
        var query = db.Courses.AsNoTracking().AsQueryable();
        if (q.Status is { } status) query = query.Where(c => c.Status == status && !c.IsDeleted);
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(c => c.Title.Contains(kw) || c.Instructor.FullName.Contains(kw) || c.Instructor.Email!.Contains(kw));
        }

        return query
            .OrderBy(c => c.Status == CourseStatus.Pending ? 0 : 1)
            .ThenByDescending(c => c.SubmittedAt ?? c.CreatedAt)
            .Select(c => new AdminCourseListItemDto(c.Id, c.Title, c.Slug, c.ThumbnailUrl, c.Status, c.Instructor.FullName,
                c.Category.Name, c.Price, c.DiscountPrice, c.IsFeatured, c.IsDeleted, c.EnrollmentCount, c.TotalLessons,
                c.CreatedAt, c.SubmittedAt, c.PublishedAt))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);
    }

    public async Task<Result> ApproveAsync(Guid courseId, CancellationToken ct = default)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted, ct);
        if (course is null) return Result.Failure(NotFound);
        if (course.Status != CourseStatus.Pending) return Result.Failure(Error.Conflict("Chỉ duyệt được khóa học đang chờ duyệt."));

        course.Status = CourseStatus.Published;
        course.PublishedAt ??= clock.UtcNow;
        course.RejectionReason = null;
        notifications.Add(course.InstructorId, "Khóa học đã được duyệt",
            $"Khóa học \"{course.Title}\" đã được xuất bản.", NotificationType.Course, $"/khoa-hoc/{course.Slug}");
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RejectAsync(Guid courseId, RejectCourseRequest request, CancellationToken ct = default)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted, ct);
        if (course is null) return Result.Failure(NotFound);
        if (course.Status != CourseStatus.Pending) return Result.Failure(Error.Conflict("Chỉ từ chối được khóa học đang chờ duyệt."));

        course.Status = CourseStatus.Rejected;
        course.RejectionReason = request.Reason.Trim();
        notifications.Add(course.InstructorId, "Khóa học bị từ chối",
            $"Khóa học \"{course.Title}\" chưa được duyệt: {course.RejectionReason}", NotificationType.Course,
            $"/giang-vien/khoa-hoc/{course.Id}");
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UnpublishAsync(Guid courseId, CancellationToken ct = default)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted, ct);
        if (course is null) return Result.Failure(NotFound);
        if (course.Status != CourseStatus.Published) return Result.Failure(Error.Conflict("Khóa học chưa được xuất bản."));

        course.Status = CourseStatus.Draft;
        course.IsFeatured = false;
        notifications.Add(course.InstructorId, "Khóa học bị gỡ",
            $"Khóa học \"{course.Title}\" đã được quản trị viên chuyển về bản nháp.", NotificationType.Course,
            $"/giang-vien/khoa-hoc/{course.Id}");
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetFeaturedAsync(Guid courseId, bool isFeatured, CancellationToken ct = default)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted, ct);
        if (course is null) return Result.Failure(NotFound);
        if (isFeatured && course.Status != CourseStatus.Published)
            return Result.Failure(Error.Conflict("Chỉ khóa học đã xuất bản mới được đặt nổi bật."));

        course.IsFeatured = isFeatured;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
