using Microsoft.AspNetCore.Identity;
using OnlineLearning.Domain.Common;

namespace OnlineLearning.Domain.Entities;

public class AppUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Headline { get; set; }
    public string? Bio { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // ---------- Hồ sơ giảng viên ----------
    /// <summary>Số năm kinh nghiệm làm nghề/giảng dạy do giảng viên khai.</summary>
    public int? YearsOfExperience { get; set; }
    /// <summary>Chuyên môn, phân tách bằng dấu ";" (ví dụ "C#;ASP.NET Core;SQL Server").</summary>
    public string? Expertise { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? YoutubeUrl { get; set; }
    public string? FacebookUrl { get; set; }
    /// <summary>Admin đã đối chiếu danh tính và kinh nghiệm của giảng viên.</summary>
    public bool IsVerifiedInstructor { get; set; }
    public DateTime? InstructorVerifiedAt { get; set; }

    public ICollection<InstructorExperience> Experiences { get; set; } = [];

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<Course> TaughtCourses { get; set; } = [];
    public ICollection<Enrollment> Enrollments { get; set; } = [];
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<Review> Reviews { get; set; } = [];
    public ICollection<Certificate> Certificates { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public Cart? Cart { get; set; }
}

public class AppRole : IdentityRole<Guid>
{
    public AppRole() { }

    public AppRole(string name, string? description = null) : base(name)
    {
        Description = description;
    }

    public string? Description { get; set; }
}

/// <summary>Refresh token: chỉ lưu SHA-256 hash, không lưu chuỗi gốc.</summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedByIp { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive(DateTime utcNow) => RevokedAt is null && utcNow < ExpiresAt;
}

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Enums.NotificationType Type { get; set; } = Enums.NotificationType.System;
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
