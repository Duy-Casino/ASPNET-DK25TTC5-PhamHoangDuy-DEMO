using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnlineLearning.Application.Common.Helpers;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Common.Options;
using OnlineLearning.Application.Features.Notifications;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Orders;

public interface IOrderService
{
    Task<Result<OrderDto>> CreateAsync(Guid userId, CreateOrderRequest request, CancellationToken ct = default);
    Task<PagedResult<OrderListItemDto>> GetMineAsync(Guid userId, OrderQuery query, CancellationToken ct = default);
    Task<Result<OrderDto>> GetAsync(Guid orderId, Guid userId, bool isAdmin, CancellationToken ct = default);
    Task<Result> CancelAsync(Guid orderId, Guid userId, CancellationToken ct = default);
    Task<PagedResult<AdminOrderListItemDto>> ListAllAsync(OrderQuery query, CancellationToken ct = default);
    Task<int> ExpireStaleOrdersAsync(CancellationToken ct = default);
}

public sealed class OrderService(
    IAppDbContext db,
    IPricingService pricing,
    IOrderFulfillmentService fulfillment,
    IDateTimeProvider clock,
    IOptions<AppOptions> appOptions) : IOrderService
{
    public async Task<Result<OrderDto>> CreateAsync(Guid userId, CreateOrderRequest request, CancellationToken ct = default)
    {
        var courseIds = request.FromCart
            ? await db.CartItems.Where(i => i.Cart.UserId == userId).Select(i => i.CourseId).ToListAsync(ct)
            : request.CourseIds?.ToList() ?? [];
        if (courseIds.Count == 0) return Error.Validation("Giỏ hàng trống.");

        var priced = await pricing.CalculateAsync(userId, courseIds, request.CouponCode, ct);
        if (priced.IsFailure) return priced.Error!;
        var p = priced.Value!;

        var now = clock.UtcNow;
        var order = new Order
        {
            OrderCode = await UniqueOrderCodeAsync(now, ct),
            UserId = userId,
            CouponId = p.Coupon?.Id,
            CouponCode = p.Coupon?.Code,
            SubTotal = p.SubTotal,
            DiscountAmount = p.DiscountAmount,
            TotalAmount = p.TotalAmount,
            Status = OrderStatus.Pending,
            Note = request.Note?.Trim(),
            ExpiresAt = now.AddMinutes(appOptions.Value.OrderExpiryMinutes),
            Items = p.Lines.Select(l => new OrderItem
            {
                CourseId = l.Course.Id,
                CourseTitle = l.Course.Title,
                InstructorId = l.Course.InstructorId,
                OriginalPrice = l.OriginalPrice,
                Price = l.Price,
                DiscountAllocated = l.DiscountAllocated,
                FinalPrice = l.FinalPrice,
            }).ToList(),
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        // FR-ORDER-08: đơn 0đ hoàn tất ngay, không qua cổng thanh toán
        if (order.TotalAmount == 0)
        {
            var paid = await fulfillment.CompleteFreeOrderAsync(order.Id, ct);
            if (paid.IsFailure) return paid.Error!;
        }

        return await GetAsync(order.Id, userId, false, ct);
    }

    public Task<PagedResult<OrderListItemDto>> GetMineAsync(Guid userId, OrderQuery query, CancellationToken ct = default) =>
        db.Orders.AsNoTracking()
            .Where(o => o.UserId == userId && (query.Status == null || o.Status == query.Status))
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderListItemDto(o.Id, o.OrderCode, o.Status, o.TotalAmount, o.Items.Count,
                o.Items.OrderBy(i => i.CourseTitle).Select(i => i.CourseTitle).FirstOrDefault() ?? "", o.CreatedAt, o.PaidAt))
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

    public async Task<Result<OrderDto>> GetAsync(Guid orderId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var o = await db.Orders.AsNoTracking()
            .Include(x => x.Items).ThenInclude(i => i.Course)
            .Include(x => x.Payments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (o is null || (!isAdmin && o.UserId != userId)) return Error.NotFound("Không tìm thấy đơn hàng.");

        return new OrderDto(o.Id, o.OrderCode, o.Status, o.SubTotal, o.DiscountAmount, o.TotalAmount, o.CouponCode, o.Note,
            o.CreatedAt, o.ExpiresAt, o.PaidAt, o.CancelledAt,
            o.Items.Select(i => new OrderItemDto(i.CourseId, i.CourseTitle, i.Course?.Slug, i.Course?.ThumbnailUrl,
                i.OriginalPrice, i.Price, i.DiscountAllocated, i.FinalPrice)).ToList(),
            o.Payments.OrderByDescending(x => x.CreatedAt).Select(x => new PaymentDto(x.Id, x.Provider, x.Amount, x.Status,
                x.TransactionRef, x.ProviderTransactionId, x.BankCode, x.ResponseCode, x.CreatedAt, x.PaidAt)).ToList());
    }

    public async Task<Result> CancelAsync(Guid orderId, Guid userId, CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct);
        if (order is null) return Result.Failure(Error.NotFound("Không tìm thấy đơn hàng."));
        if (order.Status != OrderStatus.Pending) return Result.Failure(Error.Conflict("Chỉ hủy được đơn đang chờ thanh toán."));

        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public Task<PagedResult<AdminOrderListItemDto>> ListAllAsync(OrderQuery q, CancellationToken ct = default)
    {
        var query = db.Orders.AsNoTracking().AsQueryable();
        if (q.Status is { } s) query = query.Where(o => o.Status == s);
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(o => o.OrderCode.Contains(kw) || o.User.Email!.Contains(kw) || o.User.FullName.Contains(kw));
        }

        return query.OrderByDescending(o => o.CreatedAt)
            .Select(o => new AdminOrderListItemDto(o.Id, o.OrderCode, o.Status, o.User.FullName, o.User.Email!,
                o.SubTotal, o.DiscountAmount, o.TotalAmount, o.CouponCode, o.Items.Count, o.CreatedAt, o.PaidAt))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);
    }

    public async Task<int> ExpireStaleOrdersAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var stale = await db.Orders.Where(o => o.Status == OrderStatus.Pending && o.ExpiresAt < now).ToListAsync(ct);
        foreach (var o in stale) o.Status = OrderStatus.Expired;
        if (stale.Count > 0) await db.SaveChangesAsync(ct);
        return stale.Count;
    }

    private async Task<string> UniqueOrderCodeAsync(DateTime now, CancellationToken ct)
    {
        while (true)
        {
            var code = CodeGenerator.OrderCode(now);
            if (!await db.Orders.AnyAsync(o => o.OrderCode == code, ct)) return code;
        }
    }
}

// =====================================================================

public sealed record PaymentConfirmation(
    PaymentProvider Provider, string TransactionRef, string? ProviderTransactionId, decimal Amount,
    string? BankCode, string? ResponseCode, string? RawResponse);

public interface IOrderFulfillmentService
{
    Task<Result> CompleteFreeOrderAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// Đánh dấu đơn đã thanh toán và cấp quyền học. Chỉ được gọi sau khi Backend đã xác thực
    /// chữ ký + số tiền từ cổng thanh toán (IPN, Giai đoạn 5). Idempotent.
    /// </summary>
    Task<Result> MarkPaidAsync(Guid orderId, PaymentConfirmation confirmation, CancellationToken ct = default);

    /// <summary>
    /// Admin xác nhận đã hoàn tiền (tiền được hoàn ngoài hệ thống qua VNPay/MoMo): đơn → Refunded,
    /// thu hồi quyền học của đơn và thu hồi điểm thưởng tương ứng. Không hoàn đơn đã cấp chứng chỉ.
    /// </summary>
    Task<Result> RefundAsync(Guid orderId, Guid adminId, string reason, CancellationToken ct = default);
}

public sealed class OrderFulfillmentService(
    IAppDbContext db, IDateTimeProvider clock, INotificationService notifications, Rewards.IRewardService rewards,
    ILogger<OrderFulfillmentService> logger)
    : IOrderFulfillmentService
{
    public Task<Result> CompleteFreeOrderAsync(Guid orderId, CancellationToken ct = default) =>
        MarkPaidAsync(orderId, new PaymentConfirmation(PaymentProvider.Free, $"FREE-{orderId:N}", null, 0, null, "00", null), ct);

    public async Task<Result> MarkPaidAsync(Guid orderId, PaymentConfirmation confirmation, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var order = await db.Orders.Include(o => o.Items).Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return Result.Failure(Error.NotFound("Không tìm thấy đơn hàng."));

        if (order.Status == OrderStatus.Paid)
        {
            logger.LogInformation("Đơn {OrderCode} đã thanh toán trước đó — bỏ qua (idempotent)", order.OrderCode);
            return Result.Success();
        }
        if (order.Status is not (OrderStatus.Pending or OrderStatus.Expired))
            return Result.Failure(Error.Conflict("Đơn hàng không ở trạng thái chờ thanh toán."));
        if (confirmation.Amount != order.TotalAmount)
            return Result.Failure(Error.Validation("Số tiền thanh toán không khớp với đơn hàng."));

        var now = clock.UtcNow;

        var payment = order.Payments.FirstOrDefault(p => p.TransactionRef == confirmation.TransactionRef);
        if (payment is null)
        {
            payment = new Payment
            {
                OrderId = order.Id,
                Provider = confirmation.Provider,
                Amount = confirmation.Amount,
                TransactionRef = confirmation.TransactionRef,
            };
            db.Payments.Add(payment);
        }
        payment.Status = PaymentStatus.Success;
        payment.ProviderTransactionId = confirmation.ProviderTransactionId;
        payment.BankCode = confirmation.BankCode;
        payment.ResponseCode = confirmation.ResponseCode;
        payment.RawResponse = confirmation.RawResponse;
        payment.PaidAt = now;

        order.Status = OrderStatus.Paid;
        order.PaidAt = now;

        var courseIds = order.Items.Select(i => i.CourseId).ToList();
        var alreadyEnrolled = await db.Enrollments
            .Where(e => e.UserId == order.UserId && courseIds.Contains(e.CourseId))
            .Select(e => e.CourseId).ToListAsync(ct);
        var courses = await db.Courses.Where(c => courseIds.Contains(c.Id)).ToListAsync(ct);

        foreach (var item in order.Items.Where(i => !alreadyEnrolled.Contains(i.CourseId)))
        {
            db.Enrollments.Add(new Enrollment
            {
                UserId = order.UserId,
                CourseId = item.CourseId,
                OrderId = order.Id,
                EnrolledAt = now,
                Status = EnrollmentStatus.Active,
            });
            var course = courses.First(c => c.Id == item.CourseId);
            course.EnrollmentCount += 1;
            notifications.Add(course.InstructorId, "Học viên mới",
                $"Có học viên mới đăng ký khóa học \"{course.Title}\".", NotificationType.Order, "/giang-vien/hoc-vien");
        }

        if (order.CouponId is { } couponId)
        {
            var coupon = await db.Coupons.FirstAsync(c => c.Id == couponId, ct);
            coupon.UsedCount += 1;
        }

        var cartItems = await db.CartItems
            .Where(i => i.Cart.UserId == order.UserId && courseIds.Contains(i.CourseId))
            .ToListAsync(ct);
        foreach (var ci in cartItems) db.CartItems.Remove(ci);

        notifications.Add(order.UserId, "Thanh toán thành công",
            $"Đơn hàng {order.OrderCode} đã được thanh toán. Bạn có thể bắt đầu học ngay.", NotificationType.Order,
            "/hoc-vien/khoa-hoc");

        // Điểm thưởng mua hàng + giới thiệu: cùng transaction với việc xác nhận thanh toán,
        // nên IPN gửi lặp (đơn đã Paid ở trên) không bao giờ cộng điểm lần hai
        await rewards.StagePurchaseRewardsAsync(order, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        logger.LogInformation("Đơn {OrderCode} thanh toán thành công qua {Provider}", order.OrderCode, confirmation.Provider);
        return Result.Success();
    }

    public async Task<Result> RefundAsync(Guid orderId, Guid adminId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
            return Result.Failure(Error.Validation("Bắt buộc ghi lý do hoàn tiền (ít nhất 5 ký tự)."));

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return Result.Failure(Error.NotFound("Không tìm thấy đơn hàng."));
        if (order.Status == OrderStatus.Refunded) return Result.Failure(Error.Conflict("Đơn hàng đã được hoàn tiền trước đó."));
        if (order.Status != OrderStatus.Paid) return Result.Failure(Error.Conflict("Chỉ hoàn tiền được đơn đã thanh toán."));

        var enrollments = await db.Enrollments.Where(e => e.OrderId == order.Id).ToListAsync(ct);
        var enrolledCourseIds = enrollments.Select(e => e.CourseId).ToList();
        if (await db.Certificates.AnyAsync(c => c.UserId == order.UserId && enrolledCourseIds.Contains(c.CourseId) && !c.IsRevoked, ct))
            return Result.Failure(Error.Conflict("Không hoàn tiền được: học viên đã được cấp chứng chỉ cho khóa học trong đơn."));

        var now = clock.UtcNow;
        order.Status = OrderStatus.Refunded;
        order.RefundedAt = now;
        order.RefundReason = reason.Trim();

        var courses = await db.Courses.Where(c => enrolledCourseIds.Contains(c.Id)).ToListAsync(ct);
        foreach (var e in enrollments.Where(e => e.Status != EnrollmentStatus.Revoked))
        {
            e.Status = EnrollmentStatus.Revoked;
            var course = courses.First(c => c.Id == e.CourseId);
            course.EnrollmentCount = Math.Max(0, course.EnrollmentCount - 1);
        }

        await rewards.StageRefundRevocationAsync(order, adminId, ct);
        notifications.Add(order.UserId, "Đơn hàng đã được hoàn tiền",
            $"Đơn {order.OrderCode} đã được hoàn tiền. Quyền học các khóa trong đơn đã được thu hồi.", NotificationType.Order,
            $"/don-hang/{order.Id}");

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogInformation("Admin {AdminId} hoàn tiền đơn {OrderCode}", adminId, order.OrderCode);
        return Result.Success();
    }
}
