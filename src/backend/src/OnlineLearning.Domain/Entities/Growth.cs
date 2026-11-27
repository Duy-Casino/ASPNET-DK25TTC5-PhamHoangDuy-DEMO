using OnlineLearning.Domain.Common;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Domain.Entities;

/// <summary>Khóa học học viên lưu vào danh sách yêu thích.</summary>
public class WishlistItem : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}

/// <summary>Combo nhiều khóa học bán với giá ưu đãi (Admin tạo).</summary>
public class Bundle : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    /// <summary>Giá bán cả combo; được chia cho từng khóa theo tỷ lệ giá gốc khi đặt hàng.</summary>
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<BundleCourse> Courses { get; set; } = [];
}

public class BundleCourse : BaseEntity
{
    public Guid BundleId { get; set; }
    public Bundle Bundle { get; set; } = null!;

    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public int SortOrder { get; set; }
}

/// <summary>Quà tặng khóa học: tạo cùng đơn mua tặng, gửi cho người nhận sau khi thanh toán, nhận bằng mã.</summary>
public class CourseGift : AuditableEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid SenderId { get; set; }
    public AppUser Sender { get; set; } = null!;

    public string RecipientEmail { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string? Message { get; set; }

    /// <summary>Mã nhận quà (bí mật, gửi qua email cho người nhận).</summary>
    public string Code { get; set; } = string.Empty;
    public GiftStatus Status { get; set; } = GiftStatus.Pending;

    public DateTime? SentAt { get; set; }
    public DateTime? RedeemedAt { get; set; }
    public Guid? RedeemedById { get; set; }
}
