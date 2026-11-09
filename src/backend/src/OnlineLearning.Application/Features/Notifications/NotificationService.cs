using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Notifications;

public sealed record NotificationDto(Guid Id, string Title, string Message, NotificationType Type, string? Link, bool IsRead, DateTime CreatedAt);

public interface INotificationService
{
    /// <summary>Thêm thông báo vào DbContext (chưa SaveChanges) — lưu chung transaction với thao tác gốc.</summary>
    void Add(Guid userId, string title, string message, NotificationType type, string? link = null);
    Task AddToRoleAsync(string role, string title, string message, NotificationType type, string? link = null, CancellationToken ct = default);
    Task<PagedResult<NotificationDto>> GetMineAsync(Guid userId, PagedQuery query, CancellationToken ct = default);
    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);
    Task<Result> MarkReadAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}

public sealed class NotificationService(IAppDbContext db, IDateTimeProvider clock) : INotificationService
{
    public void Add(Guid userId, string title, string message, NotificationType type, string? link = null) =>
        db.Notifications.Add(new Notification
        {
            UserId = userId, Title = title, Message = message, Type = type, Link = link, CreatedAt = clock.UtcNow,
        });

    public async Task AddToRoleAsync(string role, string title, string message, NotificationType type, string? link = null, CancellationToken ct = default)
    {
        var normalized = role.ToUpperInvariant();
        var userIds = await (from ur in db.Set<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>()
                             join r in db.Set<AppRole>() on ur.RoleId equals r.Id
                             where r.NormalizedName == normalized
                             select ur.UserId).ToListAsync(ct);
        foreach (var id in userIds) Add(id, title, message, type, link);
    }

    public Task<PagedResult<NotificationDto>> GetMineAsync(Guid userId, PagedQuery query, CancellationToken ct = default) =>
        db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto(n.Id, n.Title, n.Message, n.Type, n.Link, n.IsRead, n.CreatedAt))
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default) =>
        db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public async Task<Result> MarkReadAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (n is null) return Result.Failure(Error.NotFound("Không tìm thấy thông báo."));
        if (!n.IsRead)
        {
            n.IsRead = true;
            n.ReadAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return Result.Success();
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var unread = await db.Notifications.Where(x => x.UserId == userId && !x.IsRead).ToListAsync(ct);
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = now;
        }
        await db.SaveChangesAsync(ct);
    }
}
