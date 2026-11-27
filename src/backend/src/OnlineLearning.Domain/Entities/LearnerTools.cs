using OnlineLearning.Domain.Common;

namespace OnlineLearning.Domain.Entities;

/// <summary>Câu hỏi của học viên dưới một bài học; học viên cùng khóa và giảng viên cùng xem, cùng trả lời.</summary>
public class LessonQuestion : AuditableEntity
{
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public Guid CourseId { get; set; }

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string Content { get; set; } = string.Empty;
    /// <summary>Người hỏi hoặc giảng viên đánh dấu đã được giải đáp.</summary>
    public bool IsResolved { get; set; }
    public DateTime LastActivityAt { get; set; }

    public ICollection<LessonAnswer> Answers { get; set; } = [];
}

public class LessonAnswer : AuditableEntity
{
    public Guid QuestionId { get; set; }
    public LessonQuestion Question { get; set; } = null!;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string Content { get; set; } = string.Empty;
    /// <summary>Trả lời của giảng viên khóa học (hiện nổi bật).</summary>
    public bool IsInstructor { get; set; }
}

/// <summary>Ghi chú cá nhân của học viên trong bài học, có thể gắn mốc thời gian video.</summary>
public class LessonNote : AuditableEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public Guid CourseId { get; set; }

    /// <summary>Giây trong video khi ghi chú (null với bài viết).</summary>
    public int? PositionSeconds { get; set; }
    public string Content { get; set; } = string.Empty;
}
