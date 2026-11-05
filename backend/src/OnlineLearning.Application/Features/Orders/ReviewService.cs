using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Courses;
using OnlineLearning.Application.Features.Notifications;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Orders;

public interface IReviewService
{
    Task<Result<ReviewDto>> GetMineAsync(Guid userId, Guid courseId, CancellationToken ct = default);
    Task<Result<ReviewDto>> CreateAsync(Guid userId, Guid courseId, SaveReviewRequest request, CancellationToken ct = default);
    Task<Result<ReviewDto>> UpdateAsync(Guid userId, Guid courseId, SaveReviewRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid userId, Guid courseId, CancellationToken ct = default);
    Task<PagedResult<InstructorReviewDto>> ListForInstructorAsync(Guid instructorId, Guid? courseId, PagedQuery query, CancellationToken ct = default);
    Task<Result> ReplyAsync(Guid instructorId, Guid reviewId, ReplyReviewRequest request, bool isAdmin, CancellationToken ct = default);
    Task<Result> SetHiddenAsync(Guid reviewId, bool hidden, CancellationToken ct = default);
}

public sealed class ReviewService(IAppDbContext db, IDateTimeProvider clock, INotificationService notifications) : IReviewService
{
    public async Task<Result<ReviewDto>> GetMineAsync(Guid userId, Guid courseId, CancellationToken ct = default)
    {
        var r = await db.Reviews.AsNoTracking().Include(x => x.User)
            .FirstOrDefaultAsync(x => x.CourseId == courseId && x.UserId == userId, ct);
        return r is null ? Error.NotFound("Bạn chưa đánh giá khóa học này.") : ToDto(r);
    }

    public async Task<Result<ReviewDto>> CreateAsync(Guid userId, Guid courseId, SaveReviewRequest request, CancellationToken ct = default)
    {
        // BR-09: chỉ học viên đã ghi danh mới được đánh giá, mỗi người một lần
        if (!await db.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseId == courseId && e.Status != EnrollmentStatus.Revoked, ct))
            return Error.Forbidden("Bạn cần sở hữu khóa học để đánh giá.");
        if (await db.Reviews.AnyAsync(r => r.UserId == userId && r.CourseId == courseId, ct))
            return Error.Conflict("Bạn đã đánh giá khóa học này. Hãy sửa đánh giá cũ.");

        var course = await db.Courses.FirstAsync(c => c.Id == courseId, ct);
        var review = new Review
        {
            CourseId = courseId, UserId = userId, Rating = request.Rating, Comment = request.Comment?.Trim(),
        };
        db.Reviews.Add(review);
        notifications.Add(course.InstructorId, "Đánh giá mới",
            $"Khóa học \"{course.Title}\" nhận đánh giá {request.Rating} sao.", NotificationType.Review, "/giang-vien/danh-gia");
        await db.SaveChangesAsync(ct);
        await RecalculateRatingAsync(courseId, ct);
        return await GetMineAsync(userId, courseId, ct);
    }

    public async Task<Result<ReviewDto>> UpdateAsync(Guid userId, Guid courseId, SaveReviewRequest request, CancellationToken ct = default)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.UserId == userId && r.CourseId == courseId, ct);
        if (review is null) return Error.NotFound("Bạn chưa đánh giá khóa học này.");

        review.Rating = request.Rating;
        review.Comment = request.Comment?.Trim();
        await db.SaveChangesAsync(ct);
        await RecalculateRatingAsync(courseId, ct);
        return await GetMineAsync(userId, courseId, ct);
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid courseId, CancellationToken ct = default)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.UserId == userId && r.CourseId == courseId, ct);
        if (review is null) return Result.Failure(Error.NotFound("Bạn chưa đánh giá khóa học này."));

        db.Reviews.Remove(review);
        await db.SaveChangesAsync(ct);
        await RecalculateRatingAsync(courseId, ct);
        return Result.Success();
    }

    public Task<PagedResult<InstructorReviewDto>> ListForInstructorAsync(Guid instructorId, Guid? courseId, PagedQuery query, CancellationToken ct = default) =>
        db.Reviews.AsNoTracking()
            .Where(r => r.Course.InstructorId == instructorId && (courseId == null || r.CourseId == courseId))
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new InstructorReviewDto(r.Id, r.CourseId, r.Course.Title, r.User.FullName, r.User.AvatarUrl, r.Rating,
                r.Comment, r.InstructorReply, r.RepliedAt, r.IsHidden, r.CreatedAt))
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

    public async Task<Result> ReplyAsync(Guid instructorId, Guid reviewId, ReplyReviewRequest request, bool isAdmin, CancellationToken ct = default)
    {
        var review = await db.Reviews.Include(r => r.Course).FirstOrDefaultAsync(r => r.Id == reviewId, ct);
        if (review is null || (!isAdmin && review.Course.InstructorId != instructorId))
            return Result.Failure(Error.NotFound("Không tìm thấy đánh giá."));

        review.InstructorReply = request.Reply.Trim();
        review.RepliedAt = clock.UtcNow;
        notifications.Add(review.UserId, "Giảng viên đã phản hồi",
            $"Giảng viên đã trả lời đánh giá của bạn cho khóa học \"{review.Course.Title}\".", NotificationType.Review,
            $"/khoa-hoc/{review.Course.Slug}");
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetHiddenAsync(Guid reviewId, bool hidden, CancellationToken ct = default)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId, ct);
        if (review is null) return Result.Failure(Error.NotFound("Không tìm thấy đánh giá."));
        review.IsHidden = hidden;
        await db.SaveChangesAsync(ct);
        await RecalculateRatingAsync(review.CourseId, ct);
        return Result.Success();
    }

    private async Task RecalculateRatingAsync(Guid courseId, CancellationToken ct)
    {
        var ratings = await db.Reviews.Where(r => r.CourseId == courseId && !r.IsHidden).Select(r => (int)r.Rating).ToListAsync(ct);
        var course = await db.Courses.FirstAsync(c => c.Id == courseId, ct);
        course.ReviewCount = ratings.Count;
        course.AverageRating = ratings.Count == 0 ? 0 : Math.Round((decimal)ratings.Average(), 2);
        await db.SaveChangesAsync(ct);
    }

    private static ReviewDto ToDto(Review r) => new(r.Id, r.UserId, r.User.FullName, r.User.AvatarUrl, r.Rating, r.Comment,
        r.InstructorReply, r.RepliedAt, r.CreatedAt, r.UpdatedAt);
}
