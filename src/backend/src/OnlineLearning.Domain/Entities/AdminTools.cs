using OnlineLearning.Domain.Common;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Domain.Entities;

/// <summary>Hồ sơ đăng ký làm giảng viên của người dùng; Admin duyệt thì được cấp vai trò Instructor.</summary>
public class InstructorApplication : AuditableEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string Headline { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Expertise { get; set; } = string.Empty;
    /// <summary>Chủ đề khóa học dự định dạy.</summary>
    public string ProposedTopic { get; set; } = string.Empty;
    public string? PortfolioUrl { get; set; }
    public int? YearsOfExperience { get; set; }

    public InstructorApplicationStatus Status { get; set; } = InstructorApplicationStatus.Pending;
    public string? AdminNote { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public Guid? ProcessedById { get; set; }
}

/// <summary>Nhật ký mọi thao tác thay đổi dữ liệu của Admin (ghi tự động ở tầng API).</summary>
public class AdminAuditLog : BaseEntity
{
    public Guid ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    /// <summary>Mã thao tác, ví dụ "Admin.SetRoles".</summary>
    public string Action { get; set; } = string.Empty;
    /// <summary>Mô tả tiếng Việt của thao tác.</summary>
    public string Summary { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    /// <summary>Dữ liệu gửi lên (JSON, đã rút gọn).</summary>
    public string? Payload { get; set; }
    public int StatusCode { get; set; }
    public bool Succeeded { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Cài đặt hệ thống do Admin chỉnh trên giao diện; ghi đè giá trị trong appsettings.json.</summary>
public class SystemSetting
{
    /// <summary>Khóa cấu hình dạng "Revenue:InstructorSharePercent".</summary>
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
}

/// <summary>Thông báo hàng loạt Admin gửi tới một nhóm người dùng.</summary>
public class Announcement : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Link { get; set; }
    public AnnouncementAudience Audience { get; set; }
    public Guid? CourseId { get; set; }
    public bool SendEmail { get; set; }
    public int RecipientCount { get; set; }
    public int EmailCount { get; set; }
    public Guid CreatedById { get; set; }
}
