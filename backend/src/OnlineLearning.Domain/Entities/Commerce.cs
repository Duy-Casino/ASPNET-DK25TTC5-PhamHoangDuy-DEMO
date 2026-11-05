using OnlineLearning.Domain.Common;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Domain.Entities;

public class Cart : AuditableEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public ICollection<CartItem> Items { get; set; } = [];
}

public class CartItem : BaseEntity
{
    public Guid CartId { get; set; }
    public Cart Cart { get; set; } = null!;

    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public DateTime AddedAt { get; set; }
}

public class Order : AuditableEntity
{
    /// <summary>Mã đơn hiển thị và gửi sang cổng thanh toán, ví dụ EDV261104A7K3.</summary>
    public string OrderCode { get; set; } = string.Empty;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public Guid? CouponId { get; set; }
    public Coupon? Coupon { get; set; }
    public string? CouponCode { get; set; }

    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string? Note { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<OrderItem> Items { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
}

public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    // Ảnh chụp tại thời điểm đặt hàng (BR-04)
    public string CourseTitle { get; set; } = string.Empty;
    public Guid InstructorId { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal Price { get; set; }
    public decimal DiscountAllocated { get; set; }
    public decimal FinalPrice { get; set; }
}

public class Payment : AuditableEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public PaymentProvider Provider { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>Mã giao dịch phía hệ thống gửi sang cổng (unique).</summary>
    public string TransactionRef { get; set; } = string.Empty;
    /// <summary>Mã giao dịch cổng trả về; unique theo Provider để chống xử lý lặp (BR-06).</summary>
    public string? ProviderTransactionId { get; set; }
    public string? BankCode { get; set; }
    public string? ResponseCode { get; set; }
    /// <summary>Tham số phản hồi (không chứa dữ liệu thẻ).</summary>
    public string? RawResponse { get; set; }
    public DateTime? PaidAt { get; set; }
}

public class Coupon : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public decimal MinOrderAmount { get; set; }
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public int PerUserLimit { get; set; } = 1;

    /// <summary>Null = áp dụng toàn sàn.</summary>
    public Guid? CourseId { get; set; }
    public Course? Course { get; set; }

    public DateTime StartsAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];

    public bool IsUsable(DateTime utcNow) =>
        IsActive && utcNow >= StartsAt && utcNow < ExpiresAt &&
        (UsageLimit is null || UsedCount < UsageLimit);

    /// <summary>BR-05: số tiền giảm trên tổng tiền hợp lệ, không vượt tổng.</summary>
    public decimal CalculateDiscount(decimal eligibleAmount)
    {
        if (eligibleAmount <= 0 || eligibleAmount < MinOrderAmount) return 0;

        var discount = DiscountType == DiscountType.Percentage
            ? Math.Round(eligibleAmount * DiscountValue / 100m, 0, MidpointRounding.AwayFromZero)
            : DiscountValue;

        if (MaxDiscountAmount is { } max && discount > max) discount = max;
        return Math.Min(discount, eligibleAmount);
    }
}
