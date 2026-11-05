using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Orders;

public sealed record PricingLine(Course Course, decimal OriginalPrice, decimal Price, decimal DiscountAllocated, decimal FinalPrice);
public sealed record PricingResult(IReadOnlyList<PricingLine> Lines, decimal SubTotal, decimal DiscountAmount, decimal TotalAmount, Coupon? Coupon);

public interface IPricingService
{
    /// <summary>Tính giá hoàn toàn từ DB (không tin giá từ Frontend), áp BR-01, BR-02, BR-05.</summary>
    Task<Result<PricingResult>> CalculateAsync(Guid userId, IReadOnlyCollection<Guid> courseIds, string? couponCode, CancellationToken ct = default);
    Task<Result<PriceQuoteDto>> QuoteAsync(Guid userId, PriceQuoteRequest request, CancellationToken ct = default);
}

public sealed class PricingService(IAppDbContext db, IDateTimeProvider clock) : IPricingService
{
    private static readonly System.Globalization.CultureInfo Vi = new("vi-VN");

    public async Task<Result<PricingResult>> CalculateAsync(Guid userId, IReadOnlyCollection<Guid> courseIds, string? couponCode, CancellationToken ct = default)
    {
        var ids = courseIds.Distinct().ToList();
        if (ids.Count == 0) return Error.Validation("Chưa chọn khóa học.");

        var courses = await db.Courses.Where(c => ids.Contains(c.Id)).ToListAsync(ct);
        if (courses.Count != ids.Count || courses.Any(c => c.IsDeleted || c.Status != CourseStatus.Published))
            return Error.Validation("Có khóa học không tồn tại hoặc đã ngừng bán.");
        if (courses.Any(c => c.InstructorId == userId))
            return Error.Conflict("Bạn không thể mua khóa học của chính mình.");

        var owned = await db.Enrollments.Where(e => e.UserId == userId && ids.Contains(e.CourseId))
            .Select(e => e.Course.Title).ToListAsync(ct);
        if (owned.Count > 0)
            return Error.Conflict($"Bạn đã sở hữu: {string.Join(", ", owned)}.");

        // Giữ thứ tự theo yêu cầu để phân bổ giảm giá ổn định
        courses = ids.Select(id => courses.First(c => c.Id == id)).ToList();
        var subTotal = courses.Sum(c => c.EffectivePrice);

        Coupon? coupon = null;
        decimal discount = 0;
        var eligible = courses;

        if (!string.IsNullOrWhiteSpace(couponCode))
        {
            var code = couponCode.Trim().ToUpperInvariant();
            coupon = await db.Coupons.FirstOrDefaultAsync(c => c.Code == code, ct);
            var now = clock.UtcNow;
            if (coupon is null || !coupon.IsUsable(now))
                return Error.Validation("Mã giảm giá không hợp lệ hoặc đã hết hạn.");

            var usedByUser = await db.Orders.CountAsync(o => o.UserId == userId && o.CouponId == coupon.Id && o.Status == OrderStatus.Paid, ct);
            if (usedByUser >= coupon.PerUserLimit)
                return Error.Validation("Bạn đã dùng hết lượt cho mã giảm giá này.");

            if (coupon.CourseId is { } scopedCourseId)
            {
                eligible = courses.Where(c => c.Id == scopedCourseId).ToList();
                if (eligible.Count == 0) return Error.Validation("Mã giảm giá không áp dụng cho các khóa học đã chọn.");
            }

            var eligibleAmount = eligible.Sum(c => c.EffectivePrice);
            if (eligibleAmount < coupon.MinOrderAmount)
                return Error.Validation($"Đơn hàng tối thiểu {coupon.MinOrderAmount.ToString("N0", Vi)}đ để dùng mã này.");

            discount = coupon.CalculateDiscount(eligibleAmount);
        }

        var lines = Allocate(courses, eligible, discount);
        return new PricingResult(lines, subTotal, discount, subTotal - discount, coupon);
    }

    public async Task<Result<PriceQuoteDto>> QuoteAsync(Guid userId, PriceQuoteRequest request, CancellationToken ct = default)
    {
        var result = await CalculateAsync(userId, request.CourseIds, request.CouponCode, ct);
        if (result.IsFailure) return result.Error!;
        var p = result.Value!;
        return new PriceQuoteDto(
            p.Lines.Select(l => new PriceLineDto(l.Course.Id, l.Course.Title, l.OriginalPrice, l.Price, l.DiscountAllocated, l.FinalPrice)).ToList(),
            p.SubTotal, p.DiscountAmount, p.TotalAmount, p.Coupon?.Code,
            p.Coupon is null ? null : $"Đã áp dụng mã {p.Coupon.Code}: giảm {p.DiscountAmount.ToString("N0", Vi)}đ");
    }

    /// <summary>Phân bổ tiền giảm theo tỷ lệ giá cho từng khóa học đủ điều kiện; phần dư làm tròn dồn vào dòng cuối.</summary>
    private static List<PricingLine> Allocate(List<Course> courses, List<Course> eligible, decimal discount)
    {
        var eligibleTotal = eligible.Sum(c => c.EffectivePrice);
        var remaining = discount;
        var lastEligible = eligible.LastOrDefault();
        var lines = new List<PricingLine>();

        foreach (var c in courses)
        {
            decimal allocated = 0;
            if (discount > 0 && eligible.Contains(c) && eligibleTotal > 0)
            {
                allocated = c == lastEligible
                    ? remaining
                    : Math.Round(discount * c.EffectivePrice / eligibleTotal, 0, MidpointRounding.ToZero);
                allocated = Math.Min(allocated, c.EffectivePrice);
                remaining -= allocated;
            }
            lines.Add(new PricingLine(c, c.Price, c.EffectivePrice, allocated, c.EffectivePrice - allocated));
        }
        return lines;
    }
}
