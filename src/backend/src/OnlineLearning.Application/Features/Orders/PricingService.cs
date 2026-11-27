using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Orders;

public sealed record PricingLine(Course Course, decimal OriginalPrice, decimal Price, decimal DiscountAllocated, decimal FinalPrice);
public sealed record PricingResult(IReadOnlyList<PricingLine> Lines, decimal SubTotal, decimal DiscountAmount, decimal TotalAmount, Coupon? Coupon,
    int PointsUsed, decimal PointsDiscountAmount, CheckoutPointsDto Points, Bundle? Bundle = null);

public interface IPricingService
{
    /// <summary>
    /// Tính giá hoàn toàn từ DB (không tin giá từ Frontend), áp BR-01, BR-02, BR-05.
    /// Điểm thưởng (nếu usePoints) trừ sau mã giảm giá, theo quy tắc checkout_points và số dư thật trong DB.
    /// </summary>
    /// <param name="bundleId">Mua combo: lấy danh sách khóa và giá từ combo (bỏ qua courseIds).</param>
    /// <param name="isGift">Mua tặng: không chặn khóa học người mua đã sở hữu.</param>
    Task<Result<PricingResult>> CalculateAsync(Guid userId, IReadOnlyCollection<Guid> courseIds, string? couponCode,
        bool usePoints, int? points, CancellationToken ct = default, Guid? bundleId = null, bool isGift = false);
    Task<Result<PriceQuoteDto>> QuoteAsync(Guid userId, PriceQuoteRequest request, CancellationToken ct = default);
}

public sealed class PricingService(IAppDbContext db, IDateTimeProvider clock) : IPricingService
{
    private static readonly System.Globalization.CultureInfo Vi = new("vi-VN");

    public async Task<Result<PricingResult>> CalculateAsync(Guid userId, IReadOnlyCollection<Guid> courseIds, string? couponCode,
        bool usePoints, int? points, CancellationToken ct = default, Guid? bundleId = null, bool isGift = false)
    {
        var ids = courseIds.Distinct().ToList();
        Bundle? bundle = null;
        if (bundleId is { } bid)
        {
            bundle = await db.Bundles.Include(b => b.Courses).FirstOrDefaultAsync(b => b.Id == bid && b.IsActive, ct);
            if (bundle is null) return Error.Validation("Combo không tồn tại hoặc đã ngừng bán.");
            ids = bundle.Courses.OrderBy(x => x.SortOrder).Select(x => x.CourseId).ToList();
        }
        if (ids.Count == 0) return Error.Validation("Chưa chọn khóa học.");

        var courses = await db.Courses.Where(c => ids.Contains(c.Id)).ToListAsync(ct);
        if (courses.Count != ids.Count || courses.Any(c => c.IsDeleted || c.Status != CourseStatus.Published))
            return Error.Validation("Có khóa học không tồn tại hoặc đã ngừng bán.");
        if (courses.Any(c => c.InstructorId == userId))
            return Error.Conflict("Bạn không thể mua khóa học của chính mình.");

        if (!isGift)
        {
            var owned = await db.Enrollments.Where(e => e.UserId == userId && ids.Contains(e.CourseId))
                .Select(e => e.Course.Title).ToListAsync(ct);
            if (owned.Count > 0)
                return Error.Conflict(bundle is null
                    ? $"Bạn đã sở hữu: {string.Join(", ", owned)}."
                    : $"Bạn đã sở hữu: {string.Join(", ", owned)}. Combo chỉ bán trọn bộ, bạn có thể mua lẻ các khóa còn lại.");
        }

        // Giữ thứ tự theo yêu cầu để phân bổ giảm giá ổn định
        courses = ids.Select(id => courses.First(c => c.Id == id)).ToList();
        // Giá từng khóa: giá khuyến mãi còn hạn tại thời điểm tính, hoặc phần giá combo phân bổ theo giá gốc
        var priceOf = bundle is null
            ? courses.ToDictionary(c => c.Id, c => c.EffectivePriceAt(clock.UtcNow))
            : SplitBundlePrice(courses, bundle.Price);
        var subTotal = courses.Sum(c => priceOf[c.Id]);

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
            // Mã cá nhân (đổi từ điểm thưởng) chỉ chủ sở hữu dùng được
            if (coupon.RestrictedToUserId is { } owner && owner != userId)
                return Error.Validation("Mã giảm giá không hợp lệ hoặc đã hết hạn.");

            var usedByUser = await db.Orders.CountAsync(o => o.UserId == userId && o.CouponId == coupon.Id && o.Status == OrderStatus.Paid, ct);
            if (usedByUser >= coupon.PerUserLimit)
                return Error.Validation("Bạn đã dùng hết lượt cho mã giảm giá này.");

            if (coupon.CourseId is { } scopedCourseId)
            {
                eligible = courses.Where(c => c.Id == scopedCourseId).ToList();
                if (eligible.Count == 0) return Error.Validation("Mã giảm giá không áp dụng cho các khóa học đã chọn.");
            }

            var eligibleAmount = eligible.Sum(c => priceOf[c.Id]);
            if (eligibleAmount < coupon.MinOrderAmount)
                return Error.Validation($"Đơn hàng tối thiểu {coupon.MinOrderAmount.ToString("N0", Vi)}đ để dùng mã này.");

            discount = coupon.CalculateDiscount(eligibleAmount);
        }

        var lines = Allocate(courses, eligible, discount, priceOf);

        // ---- Điểm thưởng: trừ sau mã giảm giá ----
        var afterCoupon = subTotal - discount;
        var rule = await db.PointRules.AsNoTracking().FirstOrDefaultAsync(r => r.Key == PointRuleKeys.CheckoutPoints, ct);
        var enabled = rule is { IsActive: true, Points: > 0, AmountUnit: > 0 };
        var maxPercent = Math.Clamp(rule?.Threshold ?? 100, 0, 100);
        var balance = await db.UserPoints.Where(x => x.UserId == userId).Select(x => x.Balance).FirstOrDefaultAsync(ct);
        var maxUsable = 0;
        if (enabled)
        {
            var cap = Math.Floor(afterCoupon * maxPercent / 100m);
            var byCap = (int)Math.Floor(cap * rule!.Points / rule.AmountUnit!.Value);
            maxUsable = Math.Max(0, Math.Min(balance, byCap));
        }
        var info = new CheckoutPointsDto(enabled, balance, maxUsable, rule?.Points ?? 0, rule?.AmountUnit ?? 0, maxPercent);

        var used = 0;
        if (usePoints)
        {
            if (!enabled) return Error.Validation("Tính năng dùng điểm khi thanh toán đang tạm dừng.");
            if (points is { } wanted && wanted > maxUsable)
                return Error.Validation(maxUsable == 0
                    ? "Bạn chưa có điểm dùng được cho đơn hàng này."
                    : $"Bạn chỉ dùng được tối đa {maxUsable.ToString("N0", Vi)} điểm cho đơn hàng này.");
            used = points ?? maxUsable;
        }
        var pointsDiscount = used > 0 ? Math.Floor(used * rule!.AmountUnit!.Value / rule.Points) : 0;
        if (pointsDiscount > afterCoupon) pointsDiscount = afterCoupon;
        lines = AllocateExtra(lines, pointsDiscount);

        return new PricingResult(lines, subTotal, discount, afterCoupon - pointsDiscount, coupon, used, pointsDiscount, info, bundle);
    }

    public async Task<Result<PriceQuoteDto>> QuoteAsync(Guid userId, PriceQuoteRequest request, CancellationToken ct = default)
    {
        var result = await CalculateAsync(userId, request.CourseIds ?? [], request.CouponCode, request.UsePoints, request.Points, ct,
            request.BundleId, request.IsGift);
        if (result.IsFailure) return result.Error!;
        var p = result.Value!;
        return new PriceQuoteDto(
            p.Lines.Select(l => new PriceLineDto(l.Course.Id, l.Course.Title, l.OriginalPrice, l.Price, l.DiscountAllocated, l.FinalPrice)).ToList(),
            p.SubTotal, p.DiscountAmount, p.TotalAmount, p.Coupon?.Code,
            p.Coupon is null ? null : $"Đã áp dụng mã {p.Coupon.Code}: giảm {p.DiscountAmount.ToString("N0", Vi)}đ",
            p.PointsUsed, p.PointsDiscountAmount, p.Points);
    }

    /// <summary>
    /// Phân bổ thêm một khoản giảm (điểm thưởng) lên các dòng theo tỷ lệ số tiền còn phải trả của từng dòng,
    /// để tổng FinalPrice luôn bằng TotalAmount. Phần dư do làm tròn dồn vào các dòng còn chỗ.
    /// </summary>
    private static List<PricingLine> AllocateExtra(List<PricingLine> lines, decimal amount)
    {
        if (amount <= 0) return lines;
        var total = lines.Sum(l => l.FinalPrice);
        var shares = new decimal[lines.Count];
        var remaining = amount;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].FinalPrice <= 0 || total <= 0) continue;
            shares[i] = Math.Min(lines[i].FinalPrice, Math.Round(amount * lines[i].FinalPrice / total, 0, MidpointRounding.ToZero));
            remaining -= shares[i];
        }
        for (var i = lines.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var room = lines[i].FinalPrice - shares[i];
            var add = Math.Min(room, remaining);
            shares[i] += add;
            remaining -= add;
        }
        return lines.Select((l, i) => l with { DiscountAllocated = l.DiscountAllocated + shares[i], FinalPrice = l.FinalPrice - shares[i] }).ToList();
    }

    /// <summary>Chia giá combo cho từng khóa theo tỷ lệ giá gốc; phần dư làm tròn dồn vào khóa cuối để tổng đúng bằng giá combo.</summary>
    internal static Dictionary<Guid, decimal> SplitBundlePrice(List<Course> courses, decimal bundlePrice)
    {
        var total = courses.Sum(c => c.Price);
        var result = new Dictionary<Guid, decimal>();
        var remaining = bundlePrice;
        for (var i = 0; i < courses.Count; i++)
        {
            var c = courses[i];
            var share = i == courses.Count - 1 ? remaining
                : total <= 0 ? 0 : Math.Min(remaining, Math.Round(bundlePrice * c.Price / total, 0, MidpointRounding.ToZero));
            result[c.Id] = share;
            remaining -= share;
        }
        return result;
    }

    /// <summary>Phân bổ tiền giảm theo tỷ lệ giá cho từng khóa học đủ điều kiện; phần dư làm tròn dồn vào dòng cuối.</summary>
    private static List<PricingLine> Allocate(List<Course> courses, List<Course> eligible, decimal discount, Dictionary<Guid, decimal> priceOf)
    {
        var eligibleTotal = eligible.Sum(c => priceOf[c.Id]);
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
                    : Math.Round(discount * priceOf[c.Id] / eligibleTotal, 0, MidpointRounding.ToZero);
                allocated = Math.Min(allocated, priceOf[c.Id]);
                remaining -= allocated;
            }
            lines.Add(new PricingLine(c, c.Price, priceOf[c.Id], allocated, priceOf[c.Id] - allocated));
        }
        return lines;
    }
}
