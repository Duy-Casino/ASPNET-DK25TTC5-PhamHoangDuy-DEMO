using FluentValidation;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Courses;

// ---------- Public ----------

public sealed record InstructorBriefDto(Guid Id, string FullName, string? AvatarUrl, string? Headline);
public sealed record CategoryBriefDto(Guid Id, string Name, string Slug);

public sealed record CourseSummaryDto(
    Guid Id, string Title, string Slug, string? ShortDescription, string? ThumbnailUrl, CourseLevel Level,
    decimal Price, decimal? DiscountPrice, decimal EffectivePrice,
    decimal AverageRating, int ReviewCount, int EnrollmentCount, int TotalLessons, int TotalDurationSeconds,
    bool IsFeatured, DateTime? PublishedAt, InstructorBriefDto Instructor, CategoryBriefDto Category);

public sealed class CourseSearchQuery : PagedQuery
{
    public string? Keyword { get; set; }
    public string? Category { get; set; }
    public CourseLevel? Level { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public decimal? MinRating { get; set; }
    public bool? Free { get; set; }
    /// <summary>newest | popular | rating | price_asc | price_desc</summary>
    public string? Sort { get; set; }
    public Guid? InstructorId { get; set; }
}

public sealed record CurriculumLessonDto(Guid Id, string Title, LessonType Type, int DurationSeconds, bool IsPreview, string? PreviewVideoUrl);
public sealed record CurriculumSectionDto(Guid Id, string Title, string? Description, int SortOrder, int LessonCount, int DurationSeconds, IReadOnlyList<CurriculumLessonDto> Lessons);

public sealed record InstructorProfileDto(
    Guid Id, string FullName, string? AvatarUrl, string? Headline, string? Bio,
    int TotalCourses, int TotalStudents, int TotalReviews, decimal AverageRating, bool IsVerified = false);

public sealed record CourseDetailDto(
    Guid Id, string Title, string Slug, string? ShortDescription, string? Description,
    IReadOnlyList<string> Requirements, IReadOnlyList<string> Outcomes,
    string? ThumbnailUrl, string? PreviewVideoUrl, string Language, CourseLevel Level,
    decimal Price, decimal? DiscountPrice, decimal EffectivePrice, CourseStatus Status,
    decimal AverageRating, int ReviewCount, int EnrollmentCount, int TotalLessons, int TotalDurationSeconds,
    int QuizCount, DateTime? PublishedAt, DateTime? UpdatedAt,
    CategoryBriefDto Category, InstructorProfileDto Instructor,
    IReadOnlyList<CurriculumSectionDto> Sections, IReadOnlyDictionary<int, int> RatingDistribution,
    bool IsEnrolled, bool IsInCart, bool IsOwner);

public sealed record ReviewDto(
    Guid Id, Guid UserId, string UserFullName, string? UserAvatarUrl, byte Rating, string? Comment,
    string? InstructorReply, DateTime? RepliedAt, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record TestimonialDto(Guid Id, string UserFullName, string? UserAvatarUrl, byte Rating, string Comment, string CourseTitle, string CourseSlug);

public sealed record HomeCategoryDto(Guid Id, string Name, string Slug, string? IconUrl, int CourseCount);

public sealed record HomeDto(
    IReadOnlyList<CourseSummaryDto> Featured,
    IReadOnlyList<CourseSummaryDto> Newest,
    IReadOnlyList<CourseSummaryDto> BestSelling,
    IReadOnlyList<HomeCategoryDto> Categories,
    IReadOnlyList<InstructorProfileDto> FeaturedInstructors,
    IReadOnlyList<TestimonialDto> Testimonials,
    HomeStatsDto Stats);

public sealed record HomeStatsDto(int TotalCourses, int TotalStudents, int TotalInstructors, int TotalCertificates);

// ---------- Instructor ----------

public sealed record SaveCourseRequest(
    string Title, Guid CategoryId, string? ShortDescription, string? Description,
    IReadOnlyList<string>? Requirements, IReadOnlyList<string>? Outcomes,
    string? ThumbnailUrl, string? PreviewVideoUrl, string? Language, CourseLevel Level,
    decimal Price, decimal? DiscountPrice);

public sealed record InstructorCourseListItemDto(
    Guid Id, string Title, string Slug, string? ThumbnailUrl, CourseStatus Status, string? RejectionReason,
    decimal Price, decimal? DiscountPrice, int TotalLessons, int EnrollmentCount, decimal AverageRating, int ReviewCount,
    DateTime CreatedAt, DateTime? UpdatedAt, DateTime? SubmittedAt, DateTime? PublishedAt);

public sealed record LessonResourceDto(Guid Id, string FileName, string FileUrl, string? ContentType, long FileSize);

public sealed record LessonEditDto(
    Guid Id, Guid SectionId, string Title, LessonType Type, string? Content, string? VideoUrl, string? VideoPublicId,
    int DurationSeconds, int SortOrder, bool IsPreview, bool IsRequired, IReadOnlyList<LessonResourceDto> Resources);

public sealed record SectionEditDto(Guid Id, string Title, string? Description, int SortOrder, IReadOnlyList<LessonEditDto> Lessons);

public sealed record CourseEditDto(
    Guid Id, string Title, string Slug, Guid CategoryId, string? ShortDescription, string? Description,
    IReadOnlyList<string> Requirements, IReadOnlyList<string> Outcomes, string? ThumbnailUrl, string? PreviewVideoUrl,
    string Language, CourseLevel Level, decimal Price, decimal? DiscountPrice, CourseStatus Status, string? RejectionReason,
    bool IsEditable, Guid InstructorId, IReadOnlyList<SectionEditDto> Sections);

public sealed record SaveSectionRequest(string Title, string? Description);
public sealed record SaveLessonRequest(
    string Title, LessonType Type, string? Content, string? VideoUrl, string? VideoPublicId,
    int DurationSeconds, bool IsPreview, bool IsRequired = true);
public sealed record ReorderRequest(IReadOnlyList<Guid> OrderedIds);
public sealed record AddResourceRequest(string FileName, string FileUrl, string? PublicId, string? ContentType, long FileSize);

// ---------- Admin ----------

public sealed class AdminCourseQuery : PagedQuery
{
    public string? Keyword { get; set; }
    public CourseStatus? Status { get; set; }
}

public sealed record AdminCourseListItemDto(
    Guid Id, string Title, string Slug, string? ThumbnailUrl, CourseStatus Status, string InstructorName, string CategoryName,
    decimal Price, decimal? DiscountPrice, bool IsFeatured, bool IsDeleted, int EnrollmentCount, int TotalLessons,
    DateTime CreatedAt, DateTime? SubmittedAt, DateTime? PublishedAt);

public sealed record RejectCourseRequest(string Reason);
public sealed record SetFeaturedRequest(bool IsFeatured);

// ---------- Validators ----------

public sealed class SaveCourseRequestValidator : AbstractValidator<SaveCourseRequest>
{
    public SaveCourseRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Vui lòng nhập tên khóa học.").MaximumLength(200);
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Vui lòng chọn danh mục.");
        RuleFor(x => x.ShortDescription).MaximumLength(500);
        RuleFor(x => x.Description).MaximumLength(50000);
        RuleFor(x => x.Requirements).Must(l => l is null || l.Count <= 20).WithMessage("Tối đa 20 yêu cầu.");
        RuleForEach(x => x.Requirements).MaximumLength(300);
        RuleFor(x => x.Outcomes).Must(l => l is null || l.Count <= 20).WithMessage("Tối đa 20 kết quả đạt được.");
        RuleForEach(x => x.Outcomes).MaximumLength(300);
        RuleFor(x => x.ThumbnailUrl).Must(BeHttpsOrNull).WithMessage("Ảnh đại diện phải là URL https.");
        RuleFor(x => x.PreviewVideoUrl).Must(BeHttpsOrNull).WithMessage("Video giới thiệu phải là URL https.");
        RuleFor(x => x.Language).MaximumLength(10);
        RuleFor(x => x.Level).IsInEnum();
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100_000_000).WithMessage("Giá không hợp lệ.");
        RuleFor(x => x.DiscountPrice)
            .GreaterThanOrEqualTo(0)
            .LessThan(x => x.Price).WithMessage("Giá khuyến mãi phải nhỏ hơn giá gốc.")
            .When(x => x.DiscountPrice.HasValue);
    }

    /// <summary>URL https tuyệt đối, hoặc đường dẫn ảnh đã tải lên hệ thống (/uploads/...).</summary>
    internal static bool BeHttpsOrNull(string? url) =>
        string.IsNullOrWhiteSpace(url) ||
        (url.Length <= 500 && ((Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps)
                               || (url.StartsWith("/uploads/", StringComparison.Ordinal) && !url.Contains("..", StringComparison.Ordinal))));
}

public sealed class SaveSectionRequestValidator : AbstractValidator<SaveSectionRequest>
{
    public SaveSectionRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Vui lòng nhập tên chương.").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class SaveLessonRequestValidator : AbstractValidator<SaveLessonRequest>
{
    public SaveLessonRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Vui lòng nhập tên bài học.").MaximumLength(200);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Content).MaximumLength(200000);
        RuleFor(x => x.VideoUrl).Must(SaveCourseRequestValidator.BeHttpsOrNull).WithMessage("Video phải là URL https.");
        RuleFor(x => x.VideoPublicId).MaximumLength(300);
        RuleFor(x => x.DurationSeconds).InclusiveBetween(0, 24 * 3600);
        RuleFor(x => x.VideoUrl).NotEmpty().When(x => x.Type == LessonType.Video && string.IsNullOrEmpty(x.VideoPublicId))
            .WithMessage("Bài học video cần có video.");
        RuleFor(x => x.Content).NotEmpty().When(x => x.Type == LessonType.Article)
            .WithMessage("Bài viết cần có nội dung.");
    }
}

public sealed class ReorderRequestValidator : AbstractValidator<ReorderRequest>
{
    public ReorderRequestValidator() =>
        RuleFor(x => x.OrderedIds).NotEmpty().Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Danh sách bị trùng.");
}

public sealed class AddResourceRequestValidator : AbstractValidator<AddResourceRequest>
{
    public AddResourceRequestValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.FileUrl).NotEmpty().Must(SaveCourseRequestValidator.BeHttpsOrNull).WithMessage("Tệp phải là URL https.")
            .When(x => string.IsNullOrWhiteSpace(x.PublicId));
        RuleFor(x => x.PublicId).MaximumLength(300);
        RuleFor(x => x.ContentType).MaximumLength(100);
        RuleFor(x => x.FileSize).InclusiveBetween(0, 500L * 1024 * 1024);
    }
}

public sealed class RejectCourseRequestValidator : AbstractValidator<RejectCourseRequest>
{
    public RejectCourseRequestValidator() =>
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Vui lòng nhập lý do từ chối.").MaximumLength(1000);
}
