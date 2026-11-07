using OnlineLearning.Domain.Common;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Domain.Entities;

/// <summary>Số dư điểm thưởng của một tài khoản. Chỉ thay đổi qua sổ cái điểm (RewardService).</summary>
public class UserPoints : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    /// <summary>Điểm hiện có (không âm).</summary>
    public int Balance { get; set; }
    /// <summary>Tổng điểm đã cộng (kể cả điều chỉnh dương của Admin).</summary>
    public int TotalEarned { get; set; }
    /// <summary>Tổng điểm đã trừ (đổi điểm, thu hồi, điều chỉnh âm).</summary>
    public int TotalSpent { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Một dòng trong sổ cái điểm. Không sửa/xóa — mọi điều chỉnh là một dòng mới.</summary>
public class PointTransaction : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    /// <summary>Dương = cộng, âm = trừ.</summary>
    public int Points { get; set; }
    public int BalanceAfter { get; set; }
    public PointTransactionType Type { get; set; }
    /// <summary>Khóa quy tắc đã áp dụng (purchase, daily_checkin…); null với điều chỉnh không gắn quy tắc.</summary>
    public string? RuleKey { get; set; }
    /// <summary>Khóa chống trùng, duy nhất theo người dùng (ví dụ "purchase:{orderId}", "checkin:2026-11-07").</summary>
    public string ReferenceKey { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>Admin thực hiện (điều chỉnh thủ công, hoàn tiền).</summary>
    public Guid? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Cấu hình một nhiệm vụ / quy tắc điểm. Quy tắc hệ thống (IsSystem) gắn với code nên không xóa được, chỉ bật/tắt.</summary>
public class PointRule : AuditableEntity
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Số điểm thưởng (hoặc số điểm cho mỗi đơn vị với quy tắc mua hàng / đổi điểm).</summary>
    public int Points { get; set; }
    /// <summary>Đơn vị tiền: mua hàng = số tiền ứng với Points điểm; đổi điểm = giá trị voucher ứng với Points điểm.</summary>
    public decimal? AmountUnit { get; set; }
    /// <summary>Ngưỡng: số ngày của chuỗi điểm danh; số điểm tối thiểu mỗi lần đổi.</summary>
    public int? Threshold { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSystem { get; set; }
    public int SortOrder { get; set; }
}

public class DailyCheckIn : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    /// <summary>Ngày điểm danh theo giờ Việt Nam (UTC+7).</summary>
    public DateOnly CheckInDate { get; set; }
    /// <summary>Số ngày liên tục tính đến ngày này.</summary>
    public int StreakCount { get; set; }
    public int PointsAwarded { get; set; }
    public int StreakBonusAwarded { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Một liên kết chia sẻ khóa học riêng của học viên.</summary>
public class CourseShare : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    /// <summary>Mã ngẫu nhiên trong liên kết (?s=…).</summary>
    public string Token { get; set; } = string.Empty;
    public ShareChannel Channel { get; set; }
    /// <summary>Băm (IP + trình duyệt) của người tạo, để loại lượt mở của chính họ.</summary>
    public string? CreatorKey { get; set; }
    public int OpenCount { get; set; }
    public DateTime? FirstOpenedAt { get; set; }
    /// <summary>Thời điểm lượt mở hợp lệ đầu tiên được thưởng điểm (nếu có).</summary>
    public DateTime? RewardedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<CourseShareOpen> Opens { get; set; } = [];
}

/// <summary>Lượt mở liên kết chia sẻ, mỗi người xem (băm IP + trình duyệt) tính một lần.</summary>
public class CourseShareOpen : BaseEntity
{
    public Guid CourseShareId { get; set; }
    public CourseShare CourseShare { get; set; } = null!;
    public string VisitorKey { get; set; } = string.Empty;
    public Guid? VisitorUserId { get; set; }
    public bool IsValid { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Đổi điểm lấy mã giảm giá (chỉ người đổi dùng được).</summary>
public class PointRedemption : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public int Points { get; set; }
    public decimal VoucherValue { get; set; }
    public Guid CouponId { get; set; }
    public Coupon Coupon { get; set; } = null!;
    public string CouponCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ReferralCode : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>Quan hệ giới thiệu: người giới thiệu nhận thưởng khi người được giới thiệu thanh toán đơn đầu tiên.</summary>
public class Referral : BaseEntity
{
    public Guid ReferrerId { get; set; }
    public AppUser Referrer { get; set; } = null!;
    public Guid ReferredUserId { get; set; }
    public AppUser ReferredUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? RewardedAt { get; set; }
    public Guid? RewardOrderId { get; set; }
}
