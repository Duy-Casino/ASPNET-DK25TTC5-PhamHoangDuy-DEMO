using OnlineLearning.Domain.Common;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Domain.Entities;

/// <summary>Một mục trong quá trình làm việc / học vấn / chứng nhận của giảng viên.</summary>
public class InstructorExperience : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public ExperienceType Type { get; set; }
    /// <summary>Vị trí, bằng cấp hoặc tên chứng nhận.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Công ty, trường hoặc đơn vị cấp.</summary>
    public string? Organization { get; set; }
    public int? StartYear { get; set; }
    /// <summary>Null = đến nay (với công việc) hoặc không có hạn (với chứng nhận).</summary>
    public int? EndYear { get; set; }
    public string? Description { get; set; }
    /// <summary>Đường dẫn xác minh chứng nhận (Credly, Microsoft Learn…), nếu có.</summary>
    public string? CredentialUrl { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// Cuộc trao đổi 1-1 giữa người học và giảng viên. Mỗi cặp (người học, giảng viên) có đúng một cuộc trao đổi;
/// CourseId ghi khóa học gần nhất mà người học hỏi tới.
/// </summary>
public class Conversation : BaseEntity
{
    public Guid StudentId { get; set; }
    public AppUser Student { get; set; } = null!;

    public Guid InstructorId { get; set; }
    public AppUser Instructor { get; set; } = null!;

    public Guid? CourseId { get; set; }
    public Course? Course { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
    public DateTime? StudentLastReadAt { get; set; }
    public DateTime? InstructorLastReadAt { get; set; }

    public ICollection<Message> Messages { get; set; } = [];
}

public class Message : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    public Guid SenderId { get; set; }
    public AppUser Sender { get; set; } = null!;

    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
