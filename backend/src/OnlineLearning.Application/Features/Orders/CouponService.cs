using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Domain.Entities;

namespace OnlineLearning.Application.Features.Orders;

public interface ICouponService
{
    Task<PagedResult<CouponDto>> ListAsync(CouponQuery query, CancellationToken ct = default);
    Task<Result<CouponDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<CouponDto>> CreateAsync(SaveCouponRequest request, CancellationToken ct = default);
    Task<Result<CouponDto>> UpdateAsync(Guid id, SaveCouponRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

public sealed class CouponService(IAppDbContext db) : ICouponService
{
    public Task<PagedResult<CouponDto>> ListAsync(CouponQuery q, CancellationToken ct = default)
    {
        var query = db.Coupons.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim().ToUpperInvariant();
            query = query.Where(c => c.Code.Contains(kw) || (c.Description != null && c.Description.Contains(q.Keyword)));
        }
        if (q.IsActive is { } active) query = query.Where(c => c.IsActive == active);

        return query.OrderByDescending(c => c.CreatedAt)
            .Select(c => new CouponDto(c.Id, c.Code, c.Description, c.DiscountType, c.DiscountValue, c.MaxDiscountAmount,
                c.MinOrderAmount, c.UsageLimit, c.UsedCount, c.PerUserLimit, c.CourseId, c.Course != null ? c.Course.Title : null,
                c.StartsAt, c.ExpiresAt, c.IsActive, c.CreatedAt))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);
    }

    public async Task<Result<CouponDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var c = await db.Coupons.AsNoTracking().Include(x => x.Course).FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? Error.NotFound("Không tìm thấy mã giảm giá.") : ToDto(c);
    }

    public async Task<Result<CouponDto>> CreateAsync(SaveCouponRequest request, CancellationToken ct = default)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Coupons.AnyAsync(c => c.Code == code, ct)) return Error.Conflict("Mã giảm giá đã tồn tại.");
        if (request.CourseId is { } cid && !await db.Courses.AnyAsync(c => c.Id == cid && !c.IsDeleted, ct))
            return Error.Validation("Khóa học áp dụng không tồn tại.");

        var coupon = new Coupon { Code = code };
        Apply(coupon, request);
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync(ct);
        return await GetAsync(coupon.Id, ct);
    }

    public async Task<Result<CouponDto>> UpdateAsync(Guid id, SaveCouponRequest request, CancellationToken ct = default)
    {
        var coupon = await db.Coupons.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (coupon is null) return Error.NotFound("Không tìm thấy mã giảm giá.");

        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Coupons.AnyAsync(c => c.Code == code && c.Id != id, ct)) return Error.Conflict("Mã giảm giá đã tồn tại.");
        if (coupon.UsedCount > 0 && code != coupon.Code) return Error.Conflict("Mã đã được sử dụng nên không thể đổi mã.");
        if (request.CourseId is { } cid && !await db.Courses.AnyAsync(c => c.Id == cid && !c.IsDeleted, ct))
            return Error.Validation("Khóa học áp dụng không tồn tại.");

        coupon.Code = code;
        Apply(coupon, request);
        await db.SaveChangesAsync(ct);
        return await GetAsync(coupon.Id, ct);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var coupon = await db.Coupons.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (coupon is null) return Result.Failure(Error.NotFound("Không tìm thấy mã giảm giá."));

        if (await db.Orders.AnyAsync(o => o.CouponId == id, ct))
            coupon.IsActive = false; // Đã gắn với đơn hàng → chỉ vô hiệu hóa để giữ lịch sử
        else
            db.Coupons.Remove(coupon);

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static void Apply(Coupon c, SaveCouponRequest r)
    {
        c.Description = r.Description?.Trim();
        c.DiscountType = r.DiscountType;
        c.DiscountValue = r.DiscountValue;
        c.MaxDiscountAmount = r.MaxDiscountAmount;
        c.MinOrderAmount = r.MinOrderAmount;
        c.UsageLimit = r.UsageLimit;
        c.PerUserLimit = r.PerUserLimit;
        c.CourseId = r.CourseId;
        c.StartsAt = DateTime.SpecifyKind(r.StartsAt.ToUniversalTime(), DateTimeKind.Utc);
        c.ExpiresAt = DateTime.SpecifyKind(r.ExpiresAt.ToUniversalTime(), DateTimeKind.Utc);
        c.IsActive = r.IsActive;
    }

    private static CouponDto ToDto(Coupon c) => new(c.Id, c.Code, c.Description, c.DiscountType, c.DiscountValue, c.MaxDiscountAmount,
        c.MinOrderAmount, c.UsageLimit, c.UsedCount, c.PerUserLimit, c.CourseId, c.Course?.Title, c.StartsAt, c.ExpiresAt,
        c.IsActive, c.CreatedAt);
}
