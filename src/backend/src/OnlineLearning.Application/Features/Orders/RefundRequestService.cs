using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Notifications;
using OnlineLearning.Application.Features.Revenue;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Orders;

public sealed record RefundRequestDto(Guid Id, Guid OrderId, string Reason, RefundRequestStatus Status, string? AdminNote,
    DateTime CreatedAt, DateTime? ProcessedAt);

/// <summary>Tình trạng hoàn tiền của một đơn: có được gửi yêu cầu không, lý do nếu không, và các yêu cầu đã gửi.</summary>
public sealed record OrderRefundStatusDto(bool CanRequest, string? BlockedReason, DateTime? Deadline, decimal MaxProgressPercent,
    decimal CurrentProgressPercent, IReadOnlyList<RefundRequestDto> Requests);

public sealed record AdminRefundRequestDto(Guid Id, Guid OrderId, string OrderCode, OrderStatus OrderStatus, decimal OrderTotal,
    DateTime? PaidAt, string UserFullName, string UserEmail, string Reason, RefundRequestStatus Status, string? AdminNote,
    DateTime CreatedAt, DateTime? ProcessedAt, decimal MaxProgressPercent, bool HasCertificate);

public sealed record CreateRefundRequest(string Reason);
public sealed record ProcessRefundRequest(string? AdminNote);

public sealed class AdminRefundQuery : PagedQuery
{
    public RefundRequestStatus? Status { get; set; }
    public string? Keyword { get; set; }
}

public sealed class CreateRefundRequestValidator : AbstractValidator<CreateRefundRequest>
{
    public CreateRefundRequestValidator() =>
        RuleFor(x => x.Reason).Must(r => !string.IsNullOrWhiteSpace(r) && r.Trim().Length >= 10)
            .WithMessage("Vui lòng nêu lý do (ít nhất 10 ký tự).").MaximumLength(1000);
}

public interface IRefundRequestService
{
    Task<Result<OrderRefundStatusDto>> GetStatusAsync(Guid userId, Guid orderId, CancellationToken ct = default);
    Task<Result<RefundRequestDto>> CreateAsync(Guid userId, Guid orderId, CreateRefundRequest request, CancellationToken ct = default);
    Task<PagedResult<AdminRefundRequestDto>> ListAsync(AdminRefundQuery query, CancellationToken ct = default);
    Task<Result> ApproveAsync(Guid adminId, Guid requestId, ProcessRefundRequest request, CancellationToken ct = default);
    Task<Result> RejectAsync(Guid adminId, Guid requestId, ProcessRefundRequest request, CancellationToken ct = default);
}

public sealed class RefundRequestService(
    IAppDbContext db,
    IOrderFulfillmentService fulfillment,
    IOrderEmailService emails,
    INotificationService notifications,
    IDateTimeProvider clock,
    IOptions<RevenueOptions> options,
    ILogger<RefundRequestService> logger) : IRefundRequestService
{
    private RevenueOptions O => options.Value;

    private static RefundRequestDto ToDto(RefundRequest r) =>
        new(r.Id, r.OrderId, r.Reason, r.Status, r.AdminNote, r.CreatedAt, r.ProcessedAt);

    /// <summary>Kiểm tra điều kiện theo Chính sách hoàn tiền; trả về lý do nếu không đủ điều kiện.</summary>
    private async Task<(string? Blocked, DateTime? Deadline, decimal Progress)> CheckAsync(Order order, List<RefundRequest> requests, CancellationToken ct)
    {
        DateTime? deadline = order.PaidAt?.AddDays(O.RefundRequestDays);
        var progress = await db.Enrollments.Where(e => e.OrderId == order.Id)
            .Select(e => (decimal?)e.ProgressPercent).MaxAsync(ct) ?? 0;

        if (requests.Any(r => r.Status == RefundRequestStatus.Pending)) return ("Yêu cầu hoàn tiền của đơn này đang chờ xử lý.", deadline, progress);
        if (order.Status == OrderStatus.Refunded) return ("Đơn hàng đã được hoàn tiền.", deadline, progress);
        if (order.Status != OrderStatus.Paid) return ("Chỉ yêu cầu hoàn tiền được đơn đã thanh toán.", deadline, progress);
        if (order.TotalAmount <= 0) return ("Đơn 0đ (khóa học miễn phí hoặc mã giảm 100%) không áp dụng hoàn tiền.", deadline, progress);
        if (requests.Any(r => r.Status == RefundRequestStatus.Rejected)) return ("Yêu cầu hoàn tiền của đơn này đã bị từ chối trước đó.", deadline, progress);
        if (deadline is null || clock.UtcNow > deadline) return ($"Đã quá {O.RefundRequestDays} ngày kể từ ngày thanh toán.", deadline, progress);
        if (progress > O.RefundMaxProgressPercent)
            return ($"Bạn đã học {progress:0.#}% nội dung, vượt mức {O.RefundMaxProgressPercent:0.#}% được hoàn tiền.", deadline, progress);

        var courseIds = order.Items.Select(i => i.CourseId).ToList();
        if (await db.Certificates.AnyAsync(c => c.UserId == order.UserId && courseIds.Contains(c.CourseId) && !c.IsRevoked, ct))
            return ("Khóa học trong đơn đã được cấp chứng chỉ.", deadline, progress);

        return (null, deadline, progress);
    }

    public async Task<Result<OrderRefundStatusDto>> GetStatusAsync(Guid userId, Guid orderId, CancellationToken ct = default)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct);
        if (order is null) return Error.NotFound("Không tìm thấy đơn hàng.");
        var requests = await db.RefundRequests.AsNoTracking().Where(r => r.OrderId == orderId).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        var (blocked, deadline, progress) = await CheckAsync(order, requests, ct);
        return new OrderRefundStatusDto(blocked is null, blocked, deadline, O.RefundMaxProgressPercent, progress, requests.Select(ToDto).ToList());
    }

    public async Task<Result<RefundRequestDto>> CreateAsync(Guid userId, Guid orderId, CreateRefundRequest request, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct);
        if (order is null) return Error.NotFound("Không tìm thấy đơn hàng.");
        var requests = await db.RefundRequests.Where(r => r.OrderId == orderId).ToListAsync(ct);
        var (blocked, _, _) = await CheckAsync(order, requests, ct);
        if (blocked is not null) return Error.Conflict(blocked);

        var entity = new RefundRequest { OrderId = order.Id, UserId = userId, Reason = request.Reason.Trim(), Status = RefundRequestStatus.Pending };
        db.RefundRequests.Add(entity);
        await notifications.AddToRoleAsync(Roles.Admin, "Yêu cầu hoàn tiền mới",
            $"Học viên yêu cầu hoàn tiền đơn {order.OrderCode} ({order.TotalAmount:N0}đ).", NotificationType.Order, "/quan-tri/hoan-tien", ct);
        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogInformation(ex, "Yêu cầu hoàn tiền trùng cho đơn {OrderId}", orderId);
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return Error.Conflict("Yêu cầu hoàn tiền của đơn này đang chờ xử lý.");
        }
        return ToDto(entity);
    }

    public Task<PagedResult<AdminRefundRequestDto>> ListAsync(AdminRefundQuery q, CancellationToken ct = default)
    {
        var query = db.RefundRequests.AsNoTracking().AsQueryable();
        if (q.Status is { } s) query = query.Where(r => r.Status == s);
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(r => r.Order.OrderCode.Contains(kw) || r.User.Email!.Contains(kw) || r.User.FullName.Contains(kw));
        }
        return query
            .OrderBy(r => r.Status == RefundRequestStatus.Pending ? 0 : 1).ThenByDescending(r => r.CreatedAt)
            .Select(r => new AdminRefundRequestDto(r.Id, r.OrderId, r.Order.OrderCode, r.Order.Status, r.Order.TotalAmount, r.Order.PaidAt,
                r.User.FullName, r.User.Email!, r.Reason, r.Status, r.AdminNote, r.CreatedAt, r.ProcessedAt,
                db.Enrollments.Where(e => e.OrderId == r.OrderId).Max(e => (decimal?)e.ProgressPercent) ?? 0,
                db.Certificates.Any(c => c.UserId == r.UserId && !c.IsRevoked && r.Order.Items.Select(i => i.CourseId).Contains(c.CourseId))))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);
    }

    public async Task<Result> ApproveAsync(Guid adminId, Guid requestId, ProcessRefundRequest request, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var r = await db.RefundRequests.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (r is null) return Result.Failure(Error.NotFound("Không tìm thấy yêu cầu hoàn tiền."));
        if (r.Status != RefundRequestStatus.Pending) return Result.Failure(Error.Conflict("Yêu cầu này đã được xử lý."));

        var note = string.IsNullOrWhiteSpace(request.AdminNote) ? null : request.AdminNote.Trim();
        var reason = note ?? $"Học viên yêu cầu: {r.Reason}";
        if (reason.Length > 500) reason = reason[..500];

        // Hoàn tiền đơn (thu hồi quyền học, điểm, thu nhập giảng viên) và đóng yêu cầu trong cùng transaction
        var refunded = await fulfillment.StageRefundAsync(r.OrderId, adminId, reason, ct);
        if (refunded.IsFailure) return refunded;
        r.AdminNote = note;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogInformation("Admin {AdminId} duyệt yêu cầu hoàn tiền {RequestId} (đơn {OrderCode})", adminId, r.Id, r.Order.OrderCode);
        await emails.SendRefundedAsync(r.OrderId, ct);
        return Result.Success();
    }

    public async Task<Result> RejectAsync(Guid adminId, Guid requestId, ProcessRefundRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.AdminNote) || request.AdminNote.Trim().Length < 5)
            return Result.Failure(Error.Validation("Bắt buộc ghi lý do từ chối (ít nhất 5 ký tự)."));

        var r = await db.RefundRequests.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (r is null) return Result.Failure(Error.NotFound("Không tìm thấy yêu cầu hoàn tiền."));
        if (r.Status != RefundRequestStatus.Pending) return Result.Failure(Error.Conflict("Yêu cầu này đã được xử lý."));

        r.Status = RefundRequestStatus.Rejected;
        r.AdminNote = request.AdminNote.Trim();
        r.ProcessedAt = clock.UtcNow;
        r.ProcessedById = adminId;
        notifications.Add(r.UserId, "Yêu cầu hoàn tiền bị từ chối",
            $"Yêu cầu hoàn tiền đơn {r.Order.OrderCode} bị từ chối: {r.AdminNote}", NotificationType.Order, $"/don-hang/{r.OrderId}");
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
