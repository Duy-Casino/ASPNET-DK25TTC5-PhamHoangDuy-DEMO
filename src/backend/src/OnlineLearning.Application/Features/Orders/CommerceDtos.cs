using FluentValidation;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Courses;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Orders;

// ---------- Cart ----------
public sealed record AddToCartRequest(Guid CourseId);
public sealed record CartItemDto(Guid CourseId, CourseSummaryDto Course, DateTime AddedAt);
public sealed record CartDto(IReadOnlyList<CartItemDto> Items, int Count, decimal SubTotal, decimal OriginalTotal);

// ---------- Pricing / coupon ----------
/// <summary>UsePoints: học viên chọn trừ điểm thưởng. Points: số điểm muốn dùng; để trống thì dùng tối đa cho phép.</summary>
/// <summary>BundleId: báo giá cả combo (bỏ qua CourseIds). IsGift: mua tặng (không chặn khóa người mua đã sở hữu).</summary>
public sealed record PriceQuoteRequest(IReadOnlyList<Guid>? CourseIds, string? CouponCode, bool UsePoints = false, int? Points = null,
    Guid? BundleId = null, bool IsGift = false);
public sealed record PriceLineDto(Guid CourseId, string Title, decimal OriginalPrice, decimal Price, decimal DiscountAllocated, decimal FinalPrice);
/// <summary>Thông tin dùng điểm cho đơn đang báo giá. MaxUsable = min(số dư, số điểm ứng với MaxPercent % giá trị đơn sau mã giảm giá).</summary>
public sealed record CheckoutPointsDto(bool Enabled, int Balance, int MaxUsable, int PointsPerUnit, decimal AmountPerUnit, int MaxPercent);
public sealed record PriceQuoteDto(IReadOnlyList<PriceLineDto> Lines, decimal SubTotal, decimal DiscountAmount, decimal TotalAmount, string? CouponCode, string? CouponMessage,
    int PointsUsed, decimal PointsDiscountAmount, CheckoutPointsDto Points);

// ---------- Orders ----------
/// <summary>Frontend chỉ gửi danh sách khóa học (hoặc lấy từ giỏ) + mã giảm giá. Không gửi giá.</summary>
public sealed record CreateOrderRequest(IReadOnlyList<Guid>? CourseIds, bool FromCart, string? CouponCode, string? Note,
    bool UsePoints = false, int? Points = null, Guid? BundleId = null, GiftRequest? Gift = null);

/// <summary>Mua tặng: người nhận nhận email có mã và đăng nhập bằng đúng email này để nhận khóa học.</summary>
public sealed record GiftRequest(string RecipientEmail, string RecipientName, string? Message);

public sealed record OrderItemDto(Guid CourseId, string CourseTitle, string? CourseSlug, string? ThumbnailUrl,
    decimal OriginalPrice, decimal Price, decimal DiscountAllocated, decimal FinalPrice);
public sealed record PaymentDto(Guid Id, PaymentProvider Provider, decimal Amount, PaymentStatus Status, string TransactionRef,
    string? ProviderTransactionId, string? BankCode, string? ResponseCode, DateTime CreatedAt, DateTime? PaidAt);
public sealed record OrderDto(Guid Id, string OrderCode, OrderStatus Status, decimal SubTotal, decimal DiscountAmount,
    int PointsUsed, decimal PointsDiscountAmount, decimal TotalAmount, string? CouponCode, string? Note, DateTime CreatedAt, DateTime ExpiresAt, DateTime? PaidAt, DateTime? CancelledAt,
    IReadOnlyList<OrderItemDto> Items, IReadOnlyList<PaymentDto> Payments, OrderGiftDto? Gift = null, string? BundleTitle = null);
/// <summary>Thông tin quà tặng của đơn. RedeemUrl chỉ có khi quà đã gửi và chưa nhận.</summary>
public sealed record OrderGiftDto(string RecipientEmail, string RecipientName, string? Message, GiftStatus Status,
    DateTime? SentAt, DateTime? RedeemedAt, string? RedeemUrl);
public sealed record OrderListItemDto(Guid Id, string OrderCode, OrderStatus Status, decimal TotalAmount, int ItemCount,
    string FirstCourseTitle, DateTime CreatedAt, DateTime? PaidAt);

public sealed class OrderQuery : PagedQuery
{
    public OrderStatus? Status { get; set; }
    public string? Keyword { get; set; }
    /// <summary>Lọc đơn tạo từ ngày (giờ Việt Nam) — dùng cho Admin.</summary>
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed record AdminOrderListItemDto(Guid Id, string OrderCode, OrderStatus Status, string UserFullName, string UserEmail,
    decimal SubTotal, decimal DiscountAmount, decimal TotalAmount, string? CouponCode, int ItemCount, DateTime CreatedAt, DateTime? PaidAt);

// ---------- Coupons (Admin) ----------
public sealed record SaveCouponRequest(string Code, string? Description, DiscountType DiscountType, decimal DiscountValue,
    decimal? MaxDiscountAmount, decimal MinOrderAmount, int? UsageLimit, int PerUserLimit, Guid? CourseId,
    DateTime StartsAt, DateTime ExpiresAt, bool IsActive);
public sealed record CouponDto(Guid Id, string Code, string? Description, DiscountType DiscountType, decimal DiscountValue,
    decimal? MaxDiscountAmount, decimal MinOrderAmount, int? UsageLimit, int UsedCount, int PerUserLimit,
    Guid? CourseId, string? CourseTitle, DateTime StartsAt, DateTime ExpiresAt, bool IsActive, DateTime CreatedAt);

public sealed class CouponQuery : PagedQuery
{
    public string? Keyword { get; set; }
    public bool? IsActive { get; set; }
}

// ---------- Reviews ----------
public sealed record SaveReviewRequest(byte Rating, string? Comment);
public sealed record ReplyReviewRequest(string Reply);
public sealed record InstructorReviewDto(Guid Id, Guid CourseId, string CourseTitle, string UserFullName, string? UserAvatarUrl,
    byte Rating, string? Comment, string? InstructorReply, DateTime? RepliedAt, bool IsHidden, DateTime CreatedAt);

// ---------- Validators ----------
public sealed class AddToCartRequestValidator : AbstractValidator<AddToCartRequest>
{
    public AddToCartRequestValidator() => RuleFor(x => x.CourseId).NotEmpty();
}

public sealed class PriceQuoteRequestValidator : AbstractValidator<PriceQuoteRequest>
{
    public PriceQuoteRequestValidator()
    {
        RuleFor(x => x.CourseIds).NotEmpty().WithMessage("Chưa chọn khóa học.").When(x => x.BundleId is null);
        RuleFor(x => x.CourseIds).Must(l => l is null || l.Count <= 50);
        RuleFor(x => x.CouponCode).MaximumLength(50);
        RuleFor(x => x.Points).InclusiveBetween(1, 10_000_000).When(x => x.UsePoints && x.Points.HasValue).WithMessage("Số điểm không hợp lệ.");
    }
}

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CourseIds).NotEmpty().WithMessage("Chưa chọn khóa học.").When(x => !x.FromCart && x.BundleId is null);
        RuleFor(x => x.CourseIds).Must(l => l is null || l.Count <= 50);
        RuleFor(x => x.CouponCode).MaximumLength(50);
        RuleFor(x => x.Note).MaximumLength(500);
        When(x => x.Gift is not null, () =>
        {
            RuleFor(x => x.Gift!.RecipientEmail).NotEmpty().WithMessage("Nhập email người nhận.").EmailAddress().WithMessage("Email người nhận không hợp lệ.").MaximumLength(256);
            RuleFor(x => x.Gift!.RecipientName).NotEmpty().WithMessage("Nhập tên người nhận.").MaximumLength(100);
            RuleFor(x => x.Gift!.Message).MaximumLength(500);
        });
        RuleFor(x => x.Points).InclusiveBetween(1, 10_000_000).When(x => x.UsePoints && x.Points.HasValue).WithMessage("Số điểm không hợp lệ.");
    }
}

public sealed class SaveCouponRequestValidator : AbstractValidator<SaveCouponRequest>
{
    public SaveCouponRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50).Matches("^[A-Za-z0-9_-]+$").WithMessage("Mã chỉ gồm chữ, số, _ và -.");
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.DiscountType).IsInEnum();
        RuleFor(x => x.DiscountValue).GreaterThan(0);
        RuleFor(x => x.DiscountValue).LessThanOrEqualTo(100).When(x => x.DiscountType == DiscountType.Percentage)
            .WithMessage("Phần trăm giảm tối đa 100.");
        RuleFor(x => x.MaxDiscountAmount).GreaterThan(0).When(x => x.MaxDiscountAmount.HasValue);
        RuleFor(x => x.MinOrderAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UsageLimit).GreaterThan(0).When(x => x.UsageLimit.HasValue);
        RuleFor(x => x.PerUserLimit).GreaterThan(0);
        RuleFor(x => x.ExpiresAt).GreaterThan(x => x.StartsAt).WithMessage("Ngày hết hạn phải sau ngày bắt đầu.");
    }
}

public sealed class SaveReviewRequestValidator : AbstractValidator<SaveReviewRequest>
{
    public SaveReviewRequestValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween((byte)1, (byte)5).WithMessage("Điểm đánh giá từ 1 đến 5.");
        RuleFor(x => x.Comment).MaximumLength(2000);
    }
}

public sealed class ReplyReviewRequestValidator : AbstractValidator<ReplyReviewRequest>
{
    public ReplyReviewRequestValidator() => RuleFor(x => x.Reply).NotEmpty().MaximumLength(2000);
}
