using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Courses;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Orders;

public interface ICartService
{
    Task<CartDto> GetAsync(Guid userId, CancellationToken ct = default);
    Task<Result<CartDto>> AddAsync(Guid userId, Guid courseId, CancellationToken ct = default);
    Task<CartDto> RemoveAsync(Guid userId, Guid courseId, CancellationToken ct = default);
    Task ClearAsync(Guid userId, CancellationToken ct = default);
}

public sealed class CartService(IAppDbContext db, IDateTimeProvider clock) : ICartService
{
    public async Task<CartDto> GetAsync(Guid userId, CancellationToken ct = default)
    {
        // Tự loại khóa học không còn bán hoặc đã sở hữu khỏi giỏ
        var stale = await db.CartItems
            .Where(i => i.Cart.UserId == userId &&
                        (i.Course.IsDeleted || i.Course.Status != CourseStatus.Published ||
                         db.Enrollments.Any(e => e.UserId == userId && e.CourseId == i.CourseId)))
            .ToListAsync(ct);
        if (stale.Count > 0)
        {
            foreach (var s in stale) db.CartItems.Remove(s);
            await db.SaveChangesAsync(ct);
        }

        var items = await db.CartItems.AsNoTracking()
            .Where(i => i.Cart.UserId == userId)
            .OrderByDescending(i => i.AddedAt)
            .Select(i => new { i.CourseId, i.AddedAt, i.Course })
            .ToListAsync(ct);

        var courseIds = items.Select(i => i.CourseId).ToList();
        var summaries = await db.Courses.AsNoTracking().Where(c => courseIds.Contains(c.Id))
            .Select(CatalogService.ToSummary).ToListAsync(ct);

        var dtos = items
            .Select(i => new CartItemDto(i.CourseId, summaries.First(s => s.Id == i.CourseId), i.AddedAt))
            .ToList();

        return new CartDto(dtos, dtos.Count, dtos.Sum(d => d.Course.EffectivePrice), dtos.Sum(d => d.Course.Price));
    }

    public async Task<Result<CartDto>> AddAsync(Guid userId, Guid courseId, CancellationToken ct = default)
    {
        var course = await db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId, ct);
        if (course is null || course.IsDeleted || course.Status != CourseStatus.Published)
            return Error.NotFound("Khóa học không tồn tại hoặc chưa mở bán.");
        if (course.InstructorId == userId)
            return Error.Conflict("Bạn không thể mua khóa học của chính mình.");
        if (await db.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseId == courseId, ct))
            return Error.Conflict("Bạn đã sở hữu khóa học này.");

        var cart = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (cart is null)
        {
            cart = new Cart { UserId = userId };
            db.Carts.Add(cart);
        }

        if (cart.Items.Any(i => i.CourseId == courseId))
            return Error.Conflict("Khóa học đã có trong giỏ hàng.");

        cart.Items.Add(new CartItem { CourseId = courseId, AddedAt = clock.UtcNow });
        cart.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(userId, ct);
    }

    public async Task<CartDto> RemoveAsync(Guid userId, Guid courseId, CancellationToken ct = default)
    {
        var item = await db.CartItems.FirstOrDefaultAsync(i => i.Cart.UserId == userId && i.CourseId == courseId, ct);
        if (item is not null)
        {
            db.CartItems.Remove(item);
            await db.SaveChangesAsync(ct);
        }
        return await GetAsync(userId, ct);
    }

    public async Task ClearAsync(Guid userId, CancellationToken ct = default)
    {
        var items = await db.CartItems.Where(i => i.Cart.UserId == userId).ToListAsync(ct);
        foreach (var i in items) db.CartItems.Remove(i);
        await db.SaveChangesAsync(ct);
    }
}
