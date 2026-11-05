using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Common.Helpers;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Courses;

public interface ICatalogService
{
    Task<HomeDto> GetHomeAsync(CancellationToken ct = default);
    Task<PagedResult<CourseSummaryDto>> SearchAsync(CourseSearchQuery query, CancellationToken ct = default);
    Task<Result<CourseDetailDto>> GetDetailAsync(string slug, Guid? userId, bool isAdmin, CancellationToken ct = default);
    Task<Result<PagedResult<ReviewDto>>> GetReviewsAsync(Guid courseId, PagedQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<InstructorProfileDto>> GetFeaturedInstructorsAsync(int take, CancellationToken ct = default);
    Task<Result<InstructorProfileDto>> GetInstructorAsync(Guid instructorId, CancellationToken ct = default);
}

public sealed class CatalogService(IAppDbContext db, IFileStorage storage) : ICatalogService
{
    internal static readonly Expression<Func<Course, CourseSummaryDto>> ToSummary = c => new CourseSummaryDto(
        c.Id, c.Title, c.Slug, c.ShortDescription, c.ThumbnailUrl, c.Level,
        c.Price, c.DiscountPrice,
        c.DiscountPrice != null && c.DiscountPrice < c.Price ? c.DiscountPrice.Value : c.Price,
        c.AverageRating, c.ReviewCount, c.EnrollmentCount, c.TotalLessons, c.TotalDurationSeconds,
        c.IsFeatured, c.PublishedAt,
        new InstructorBriefDto(c.Instructor.Id, c.Instructor.FullName, c.Instructor.AvatarUrl, c.Instructor.Headline),
        new CategoryBriefDto(c.Category.Id, c.Category.Name, c.Category.Slug));

    private IQueryable<Course> Published => db.Courses.AsNoTracking()
        .Where(c => c.Status == CourseStatus.Published && !c.IsDeleted);

    public async Task<HomeDto> GetHomeAsync(CancellationToken ct = default)
    {
        var featured = await Published.Where(c => c.IsFeatured)
            .OrderByDescending(c => c.EnrollmentCount).Take(8).Select(ToSummary).ToListAsync(ct);
        var newest = await Published.OrderByDescending(c => c.PublishedAt).Take(8).Select(ToSummary).ToListAsync(ct);
        var bestSelling = await Published.OrderByDescending(c => c.EnrollmentCount).ThenByDescending(c => c.AverageRating)
            .Take(8).Select(ToSummary).ToListAsync(ct);

        var categories = await db.Categories.AsNoTracking()
            .Where(c => c.IsActive && c.ParentId == null)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new HomeCategoryDto(c.Id, c.Name, c.Slug, c.IconUrl,
                c.Courses.Count(x => x.Status == CourseStatus.Published && !x.IsDeleted) +
                c.Children.SelectMany(ch => ch.Courses).Count(x => x.Status == CourseStatus.Published && !x.IsDeleted)))
            .ToListAsync(ct);

        var testimonials = await db.Reviews.AsNoTracking()
            .Where(r => !r.IsHidden && r.Rating >= 4 && r.Comment != null && r.Comment.Length >= 30
                        && r.Course.Status == CourseStatus.Published && !r.Course.IsDeleted)
            .OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt)
            .Take(6)
            .Select(r => new TestimonialDto(r.Id, r.User.FullName, r.User.AvatarUrl, r.Rating, r.Comment!, r.Course.Title, r.Course.Slug))
            .ToListAsync(ct);

        var stats = new HomeStatsDto(
            await Published.CountAsync(ct),
            await db.Enrollments.Select(e => e.UserId).Distinct().CountAsync(ct),
            await Published.Select(c => c.InstructorId).Distinct().CountAsync(ct),
            await db.Certificates.CountAsync(c => !c.IsRevoked, ct));

        return new HomeDto(featured, newest, bestSelling, categories, await GetFeaturedInstructorsAsync(6, ct), testimonials, stats);
    }

    public async Task<PagedResult<CourseSummaryDto>> SearchAsync(CourseSearchQuery q, CancellationToken ct = default)
    {
        var query = Published;

        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(c => c.Title.Contains(kw) || (c.ShortDescription != null && c.ShortDescription.Contains(kw))
                                     || c.Instructor.FullName.Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(q.Category))
        {
            var slug = q.Category.Trim();
            query = query.Where(c => c.Category.Slug == slug || (c.Category.Parent != null && c.Category.Parent.Slug == slug));
        }

        if (q.Level is { } level) query = query.Where(c => c.Level == level);
        if (q.InstructorId is { } iid) query = query.Where(c => c.InstructorId == iid);
        if (q.MinRating is { } minRating) query = query.Where(c => c.AverageRating >= minRating);

        if (q.Free == true)
            query = query.Where(c => c.Price == 0 || c.DiscountPrice == 0);
        if (q.MinPrice is { } min)
            query = query.Where(c => (c.DiscountPrice != null && c.DiscountPrice < c.Price ? c.DiscountPrice.Value : c.Price) >= min);
        if (q.MaxPrice is { } max)
            query = query.Where(c => (c.DiscountPrice != null && c.DiscountPrice < c.Price ? c.DiscountPrice.Value : c.Price) <= max);

        query = q.Sort switch
        {
            "popular" => query.OrderByDescending(c => c.EnrollmentCount).ThenByDescending(c => c.AverageRating),
            "rating" => query.OrderByDescending(c => c.AverageRating).ThenByDescending(c => c.ReviewCount),
            "price_asc" => query.OrderBy(c => c.DiscountPrice != null && c.DiscountPrice < c.Price ? c.DiscountPrice.Value : c.Price),
            "price_desc" => query.OrderByDescending(c => c.DiscountPrice != null && c.DiscountPrice < c.Price ? c.DiscountPrice.Value : c.Price),
            _ => query.OrderByDescending(c => c.PublishedAt),
        };

        return await query.Select(ToSummary).ToPagedResultAsync(q.Page, q.PageSize, ct);
    }

    public async Task<Result<CourseDetailDto>> GetDetailAsync(string slug, Guid? userId, bool isAdmin, CancellationToken ct = default)
    {
        var course = await db.Courses.AsNoTracking()
            .Include(c => c.Category)
            .Include(c => c.Instructor)
            .Include(c => c.Sections.OrderBy(s => s.SortOrder))
                .ThenInclude(s => s.Lessons.OrderBy(l => l.SortOrder))
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Slug == slug && !c.IsDeleted, ct);

        if (course is null) return Error.NotFound("Không tìm thấy khóa học.");

        var isOwner = userId == course.InstructorId;
        if (course.Status != CourseStatus.Published && !isOwner && !isAdmin)
            return Error.NotFound("Không tìm thấy khóa học.");

        var isEnrolled = userId is { } uid && await db.Enrollments.AnyAsync(e => e.UserId == uid && e.CourseId == course.Id && e.Status != EnrollmentStatus.Revoked, ct);
        var isInCart = userId is { } uid2 && await db.CartItems.AnyAsync(i => i.Cart.UserId == uid2 && i.CourseId == course.Id, ct);

        var distribution = await db.Reviews.AsNoTracking()
            .Where(r => r.CourseId == course.Id && !r.IsHidden)
            .GroupBy(r => r.Rating)
            .Select(g => new { Rating = (int)g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var ratingDistribution = Enumerable.Range(1, 5)
            .ToDictionary(r => r, r => distribution.FirstOrDefault(d => d.Rating == r)?.Count ?? 0);

        var quizCount = await db.Quizzes.CountAsync(q => q.CourseId == course.Id, ct);
        var instructor = (await LoadInstructorProfilesAsync(db.Users.Where(u => u.Id == course.InstructorId), ct)).First();

        var sections = course.Sections.Select(s => new CurriculumSectionDto(
            s.Id, s.Title, s.Description, s.SortOrder, s.Lessons.Count, s.Lessons.Sum(l => l.DurationSeconds),
            s.Lessons.Select(l => new CurriculumLessonDto(l.Id, l.Title, l.Type, l.DurationSeconds, l.IsPreview,
                !l.IsPreview ? null : l.VideoPublicId is { Length: > 0 } vid ? storage.GetSignedUrl(vid, StoredFileKind.Video) : l.VideoUrl)).ToList())).ToList();

        return new CourseDetailDto(
            course.Id, course.Title, course.Slug, course.ShortDescription, course.Description,
            ContentSanitizer.FromJsonList(course.Requirements), ContentSanitizer.FromJsonList(course.Outcomes),
            course.ThumbnailUrl, course.PreviewVideoUrl, course.Language, course.Level,
            course.Price, course.DiscountPrice, course.EffectivePrice, course.Status,
            course.AverageRating, course.ReviewCount, course.EnrollmentCount, course.TotalLessons, course.TotalDurationSeconds,
            quizCount, course.PublishedAt, course.UpdatedAt,
            new CategoryBriefDto(course.Category.Id, course.Category.Name, course.Category.Slug),
            instructor, sections, ratingDistribution, isEnrolled, isInCart, isOwner);
    }

    public async Task<Result<PagedResult<ReviewDto>>> GetReviewsAsync(Guid courseId, PagedQuery query, CancellationToken ct = default)
    {
        if (!await Published.AnyAsync(c => c.Id == courseId, ct)) return Error.NotFound("Không tìm thấy khóa học.");

        return await db.Reviews.AsNoTracking()
            .Where(r => r.CourseId == courseId && !r.IsHidden)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewDto(r.Id, r.UserId, r.User.FullName, r.User.AvatarUrl, r.Rating, r.Comment,
                r.InstructorReply, r.RepliedAt, r.CreatedAt, r.UpdatedAt))
            .ToPagedResultAsync(query.Page, query.PageSize, ct);
    }

    public async Task<IReadOnlyList<InstructorProfileDto>> GetFeaturedInstructorsAsync(int take, CancellationToken ct = default)
    {
        var instructorIds = db.Courses.Where(c => c.Status == CourseStatus.Published && !c.IsDeleted).Select(c => c.InstructorId);
        var profiles = await LoadInstructorProfilesAsync(db.Users.Where(u => u.IsActive && instructorIds.Contains(u.Id)), ct);
        return profiles.OrderByDescending(p => p.TotalStudents).ThenByDescending(p => p.AverageRating).Take(take).ToList();
    }

    public async Task<Result<InstructorProfileDto>> GetInstructorAsync(Guid instructorId, CancellationToken ct = default)
    {
        var instructorRole = Roles.Instructor.ToUpperInvariant();
        var isInstructor = await (from ur in db.Set<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>()
                                  join r in db.Set<AppRole>() on ur.RoleId equals r.Id
                                  where ur.UserId == instructorId && r.NormalizedName == instructorRole
                                  select ur).AnyAsync(ct);
        if (!isInstructor) return Error.NotFound("Không tìm thấy giảng viên.");

        var profile = (await LoadInstructorProfilesAsync(db.Users.Where(u => u.Id == instructorId && u.IsActive), ct)).FirstOrDefault();
        return profile is null ? Error.NotFound("Không tìm thấy giảng viên.") : profile;
    }

    internal static async Task<List<InstructorProfileDto>> LoadInstructorProfilesAsync(IQueryable<AppUser> users, CancellationToken ct)
    {
        var rows = await users.AsNoTracking().Select(u => new
        {
            u.Id, u.FullName, u.AvatarUrl, u.Headline, u.Bio,
            Courses = u.TaughtCourses.Count(c => c.Status == CourseStatus.Published && !c.IsDeleted),
            Students = u.TaughtCourses.Where(c => !c.IsDeleted).Sum(c => c.EnrollmentCount),
            Reviews = u.TaughtCourses.Where(c => !c.IsDeleted).Sum(c => c.ReviewCount),
            Ratings = u.TaughtCourses.Where(c => !c.IsDeleted && c.ReviewCount > 0)
                .Select(c => new { c.AverageRating, c.ReviewCount }).ToList(),
        }).ToListAsync(ct);

        return rows.Select(r =>
        {
            var reviewTotal = r.Ratings.Sum(x => x.ReviewCount);
            var avg = reviewTotal == 0 ? 0 : Math.Round(r.Ratings.Sum(x => x.AverageRating * x.ReviewCount) / reviewTotal, 2);
            return new InstructorProfileDto(r.Id, r.FullName, r.AvatarUrl, r.Headline, r.Bio, r.Courses, r.Students, r.Reviews, avg);
        }).ToList();
    }
}
