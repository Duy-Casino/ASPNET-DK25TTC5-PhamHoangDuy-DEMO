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
public sealed record PriceQuoteRequest(IReadOnlyList<Guid> CourseIds, string? CouponCode);
public sealed record PriceLineDto(Guid CourseId, string Title, decimal OriginalPrice, decimal Price, decimal DiscountAllocated, decimal FinalPrice);
public sealed record PriceQuoteDto(IReadOnlyList<PriceLineDto> Lines, decimal SubTotal, decimal DiscountAmount, decimal TotalAmount, string? CouponCode, string? CouponMessage);

// ---------- Orders ----------
/// <summary>Frontend chỉ gửi danh sách khóa học (hoặc lấy từ giỏ) + mã giảm giá. Không gửi giá.</summary>
public sealed record CreateOrderRequest(IReadOnlyList<Guid>? CourseIds, bool FromCart, string? CouponCode, string? Note);

public sealed record OrderItemDto(Guid CourseId, string CourseTitle, string? CourseSlug, string? ThumbnailUrl,
    decimal OriginalPrice, decimal Price, decimal DiscountAllocated, decimal FinalPrice);
public sealed record PaymentDto(Guid Id, PaymentProvider Provider, decimal Amount, PaymentStatus Status, string TransactionRef,
    string? ProviderTransactionId, string? BankCode, string? ResponseCode, DateTime CreatedAt, DateTime? PaidAt);
public sealed record OrderDto(Guid Id, string OrderCode, OrderStatus Status, decimal SubTotal, decimal DiscountAmount, decimal TotalAmount,
    string? CouponCode, string? Note, DateTime CreatedAt, DateTime ExpiresAt, DateTime? PaidAt, DateTime? CancelledAt,
    IReadOnlyList<OrderItemDto> Items, IReadOnlyList<PaymentDto> Payments);
public sealed record OrderListItemDto(Guid Id, string OrderCode, OrderStatus Status, decimal TotalAmount, int ItemCount,
    string FirstCourseTitle, DateTime CreatedAt, DateTime? PaidAt);

public sealed class OrderQuery : PagedQuery
{
    public OrderStatus? Status { get; set; }
    public string? Keyword { get; set; }
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
        RuleFor(x => x.CourseIds).NotEmpty().WithMessage("Chưa chọn khóa học.").Must(l => l.Count <= 50);
        RuleFor(x => x.CouponCode).MaximumLength(50);
    }
}

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CourseIds).NotEmpty().WithMessage("Chưa chọn khóa học.").When(x => !x.FromCart);
        RuleFor(x => x.CourseIds).Must(l => l is null || l.Count <= 50);
        RuleFor(x => x.CouponCode).MaximumLength(50);
        RuleFor(x => x.Note).MaximumLength(500);
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
