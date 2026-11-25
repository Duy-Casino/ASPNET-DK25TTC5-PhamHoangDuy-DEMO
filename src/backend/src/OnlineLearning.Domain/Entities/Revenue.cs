using OnlineLearning.Domain.Common;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Domain.Entities;

/// <summary>
/// Sổ thu nhập của giảng viên (ghi sổ kiểu bút toán, không sửa/xóa): mỗi khóa học bán được sinh một dòng Sale (+),
/// khi đơn bị hoàn tiền sinh thêm một dòng Refund (−) cùng giá trị. Tiền chỉ được rút sau <see cref="AvailableAt"/>
/// (hết thời hạn yêu cầu hoàn tiền).
/// </summary>
public class InstructorEarning : AuditableEntity
{
    public Guid InstructorId { get; set; }
    public AppUser Instructor { get; set; } = null!;

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid OrderItemId { get; set; }
    public OrderItem OrderItem { get; set; } = null!;

    public Guid CourseId { get; set; }

    // Ảnh chụp tại thời điểm ghi sổ
    public string OrderCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;

    public EarningType Type { get; set; }

    /// <summary>Số tiền học viên thực trả cho khóa học (sau mã giảm giá và điểm thưởng). Âm với dòng Refund.</summary>
    public decimal GrossAmount { get; set; }
    /// <summary>Tỷ lệ % giảng viên được hưởng tại thời điểm bán.</summary>
    public decimal SharePercent { get; set; }
    /// <summary>Số tiền giảng viên được hưởng. Âm với dòng Refund.</summary>
    public decimal Amount { get; set; }

    /// <summary>Thời điểm khoản tiền được tính vào số dư có thể rút.</summary>
    public DateTime AvailableAt { get; set; }
}

/// <summary>Yêu cầu rút tiền của giảng viên; Admin chuyển khoản ngoài hệ thống rồi xác nhận "Đã chi".</summary>
public class InstructorPayout : AuditableEntity
{
    public Guid InstructorId { get; set; }
    public AppUser Instructor { get; set; } = null!;

    public decimal Amount { get; set; }
    public PayoutStatus Status { get; set; } = PayoutStatus.Requested;

    public string BankName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankAccountName { get; set; } = string.Empty;
    public string? Note { get; set; }

    /// <summary>Mã giao dịch chuyển khoản do Admin ghi khi xác nhận đã chi.</summary>
    public string? TransferReference { get; set; }
    public string? AdminNote { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public Guid? ProcessedById { get; set; }
}

/// <summary>Học viên yêu cầu hoàn tiền một đơn hàng; Admin duyệt (thực hiện hoàn tiền) hoặc từ chối.</summary>
public class RefundRequest : AuditableEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string Reason { get; set; } = string.Empty;
    public RefundRequestStatus Status { get; set; } = RefundRequestStatus.Pending;

    public string? AdminNote { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public Guid? ProcessedById { get; set; }
}
