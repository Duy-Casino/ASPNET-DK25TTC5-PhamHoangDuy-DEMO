using OnlineLearning.Domain.Common;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Domain.Entities;

/// <summary>Lượt làm bài cộng thêm cho một học viên ở một bài kiểm tra (Admin cấp khi học viên hết lượt mà chưa đạt).</summary>
public class QuizAttemptGrant : AuditableEntity
{
    public Guid QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public int ExtraAttempts { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid GrantedById { get; set; }
}

/// <summary>Yêu cầu hỗ trợ học tập do học viên gửi (xin thêm lượt kiểm tra, video lỗi, tiến độ không lưu, chưa nhận chứng chỉ...).</summary>
public class LearningSupportRequest : AuditableEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public Guid? QuizId { get; set; }
    public Guid? LessonId { get; set; }

    public SupportRequestType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public SupportRequestStatus Status { get; set; } = SupportRequestStatus.Open;

    public string? Response { get; set; }
    public Guid? ResolvedById { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

/// <summary>Nhật ký thao tác hỗ trợ học tập của Admin trên quyền học của học viên (ai, làm gì, lý do).</summary>
public class LearningActionLog : BaseEntity
{
    public Guid LearnerId { get; set; }
    public Guid CourseId { get; set; }
    public Guid? EnrollmentId { get; set; }
    public Guid ActorId { get; set; }

    /// <summary>Mã thao tác, ví dụ GrantAttempts, CompleteLesson, ResetProgress.</summary>
    public string Action { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}
