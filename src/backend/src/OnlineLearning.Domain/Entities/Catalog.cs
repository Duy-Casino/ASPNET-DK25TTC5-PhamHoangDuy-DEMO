using OnlineLearning.Domain.Common;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Domain.Entities;

public class Category : AuditableEntity
{
    public Guid? ParentId { get; set; }
    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = [];

    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Course> Courses { get; set; } = [];
}

public class Course : AuditableEntity
{
    public Guid InstructorId { get; set; }
    public AppUser Instructor { get; set; } = null!;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    /// <summary>Mảng JSON các chuỗi.</summary>
    public string? Requirements { get; set; }
    /// <summary>Mảng JSON các chuỗi.</summary>
    public string? Outcomes { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? PreviewVideoUrl { get; set; }
    public string Language { get; set; } = "vi";
    public CourseLevel Level { get; set; } = CourseLevel.Beginner;

    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }

    public CourseStatus Status { get; set; } = CourseStatus.Draft;
    public string? RejectionReason { get; set; }
    public bool IsFeatured { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    public int TotalDurationSeconds { get; set; }
    public int TotalLessons { get; set; }
    public int EnrollmentCount { get; set; }
    public int ReviewCount { get; set; }
    public decimal AverageRating { get; set; }

    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<CourseSection> Sections { get; set; } = [];
    public ICollection<Quiz> Quizzes { get; set; } = [];
    public ICollection<Enrollment> Enrollments { get; set; } = [];
    public ICollection<Review> Reviews { get; set; } = [];

    /// <summary>BR-01: giá bán thực tế (luôn tính ở Backend).</summary>
    public decimal EffectivePrice => DiscountPrice is { } d && d < Price ? d : Price;

    /// <summary>Nội dung chính chỉ được sửa khi Draft hoặc Rejected.</summary>
    public bool IsEditable => Status is CourseStatus.Draft or CourseStatus.Rejected;
}

public class CourseSection : AuditableEntity
{
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }

    public ICollection<Lesson> Lessons { get; set; } = [];
    public ICollection<Quiz> Quizzes { get; set; } = [];
}

public class Lesson : AuditableEntity
{
    public Guid SectionId { get; set; }
    public CourseSection Section { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public LessonType Type { get; set; } = LessonType.Video;
    public string? Content { get; set; }
    public string? VideoUrl { get; set; }
    public string? VideoPublicId { get; set; }
    public int DurationSeconds { get; set; }
    public int SortOrder { get; set; }
    public bool IsPreview { get; set; }
    public bool IsRequired { get; set; } = true;

    public ICollection<LessonResource> Resources { get; set; } = [];
}

public class LessonResource : AuditableEntity
{
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string? PublicId { get; set; }
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
}

public class Review : AuditableEntity
{
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public byte Rating { get; set; }
    public string? Comment { get; set; }
    public bool IsHidden { get; set; }
    public string? InstructorReply { get; set; }
    public DateTime? RepliedAt { get; set; }
}
