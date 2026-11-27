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
    /// <summary>Xuất danh sách đơn (theo bộ lọc) ra CSV cho Admin đối soát doanh thu.</summary>
    Task<byte[]> ExportCsvAsync(OrderQuery query, CancellationToken ct = default);
}

public sealed class OrderService(
    IAppDbContext db,
    IPricingService pricing,
    IOrderFulfillmentService fulfillment,
    Rewards.IRewardService rewards,
    IDateTimeProvider clock,
    IOptions<AppOptions> appOptions,
    ILogger<OrderService> logger) : IOrderService
{
    public async Task<Result<OrderDto>> CreateAsync(Guid userId, CreateOrderRequest request, CancellationToken ct = default)
    {
        var courseIds = request.BundleId is not null ? []
            : request.FromCart
                ? await db.CartItems.Where(i => i.Cart.UserId == userId).Select(i => i.CourseId).ToListAsync(ct)
                : request.CourseIds?.ToList() ?? [];
        if (courseIds.Count == 0 && request.BundleId is null) return Error.Validation("Giỏ hàng trống.");

        var gift = request.Gift;
        if (gift is not null)
        {
            var buyerEmail = await db.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstAsync(ct);
            if (string.Equals(buyerEmail, gift.RecipientEmail.Trim(), StringComparison.OrdinalIgnoreCase))
                return Error.Validation("Không thể tự tặng khóa học cho chính mình.");
        }

        // Tính giá, tạo đơn và trừ điểm (nếu dùng) trong cùng một transaction:
        // số dư thay đổi giữa lúc báo giá và lúc đặt hàng thì đơn không được tạo
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var priced = await pricing.CalculateAsync(userId, courseIds, request.CouponCode, request.UsePoints, request.Points, ct,
            request.BundleId, isGift: gift is not null);
        if (priced.IsFailure) return priced.Error!;
        var p = priced.Value!;
        if (request.UsePoints && request.Points is { } wanted && wanted != p.PointsUsed)
            return Error.Validation("Số điểm sử dụng không hợp lệ.");

        var now = clock.UtcNow;
        var order = new Order
        {
            OrderCode = await UniqueOrderCodeAsync(now, ct),
            UserId = userId,
            CouponId = p.Coupon?.Id,
            CouponCode = p.Coupon?.Code,
            SubTotal = p.SubTotal,
            DiscountAmount = p.DiscountAmount,
            PointsUsed = p.PointsUsed,
            PointsDiscountAmount = p.PointsDiscountAmount,
            TotalAmount = p.TotalAmount,
            Status = OrderStatus.Pending,
            Note = request.Note?.Trim(),
            ExpiresAt = now.AddMinutes(appOptions.Value.OrderExpiryMinutes),
            BundleId = p.Bundle?.Id,
            IsGift = gift is not null,
            Gift = gift is null ? null : new CourseGift
            {
                SenderId = userId,
                RecipientEmail = gift.RecipientEmail.Trim().ToLowerInvariant(),
                RecipientName = gift.RecipientName.Trim(),
                Message = string.IsNullOrWhiteSpace(gift.Message) ? null : gift.Message.Trim(),
                Code = await UniqueGiftCodeAsync(ct),
                Status = GiftStatus.Pending,
            },
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
        try
        {
            await db.SaveChangesAsync(ct);
            if (!await rewards.StageCheckoutDebitAsync(order, ct))
            {
                await tx.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                return Error.Conflict("Số dư điểm thưởng vừa thay đổi. Vui lòng kiểm tra lại số điểm và đặt hàng lại.");
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogInformation(ex, "Không tạo được đơn hàng có dùng điểm (yêu cầu đồng thời)");
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return Error.Conflict("Đơn hàng đang được xử lý, vui lòng thử lại.");
        }

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
            .Include(x => x.Gift)
            .Include(x => x.Bundle)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (o is null || (!isAdmin && o.UserId != userId)) return Error.NotFound("Không tìm thấy đơn hàng.");
        var giftDto = o.Gift is not { } g ? null : new OrderGiftDto(g.RecipientEmail, g.RecipientName, g.Message, g.Status, g.SentAt, g.RedeemedAt,
            // Mã nhận quà chỉ hiện cho người mua sau khi đã thanh toán (để gửi lại nếu người nhận không thấy email)
            g.Status == GiftStatus.Sent ? $"{appOptions.Value.ClientUrl.TrimEnd('/')}/qua-tang/{g.Code}" : null);

        return new OrderDto(o.Id, o.OrderCode, o.Status, o.SubTotal, o.DiscountAmount, o.PointsUsed, o.PointsDiscountAmount, o.TotalAmount, o.CouponCode, o.Note,
            o.CreatedAt, o.ExpiresAt, o.PaidAt, o.CancelledAt,
            o.Items.Select(i => new OrderItemDto(i.CourseId, i.CourseTitle, i.Course?.Slug, i.Course?.ThumbnailUrl,
                i.OriginalPrice, i.Price, i.DiscountAllocated, i.FinalPrice)).ToList(),
            o.Payments.OrderByDescending(x => x.CreatedAt).Select(x => new PaymentDto(x.Id, x.Provider, x.Amount, x.Status,
                x.TransactionRef, x.ProviderTransactionId, x.BankCode, x.ResponseCode, x.CreatedAt, x.PaidAt)).ToList(),
            giftDto, o.Bundle?.Title);
    }

    public async Task<Result> CancelAsync(Guid orderId, Guid userId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct);
        if (order is null) return Result.Failure(Error.NotFound("Không tìm thấy đơn hàng."));
        if (order.Status != OrderStatus.Pending) return Result.Failure(Error.Conflict("Chỉ hủy được đơn đang chờ thanh toán."));

        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = clock.UtcNow;
        await rewards.StageCheckoutRestoreAsync(order, refund: false, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Result.Success();
    }

    private IQueryable<Order> FilterAll(OrderQuery q)
    {
        var query = db.Orders.AsNoTracking().AsQueryable();
        if (q.Status is { } s) query = query.Where(o => o.Status == s);
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(o => o.OrderCode.Contains(kw) || o.User.Email!.Contains(kw) || o.User.FullName.Contains(kw));
        }
        // Lọc theo ngày tạo đơn (giờ Việt Nam, bao gồm cả ngày cuối)
        if (q.From is { } from) { var f = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7); query = query.Where(o => o.CreatedAt >= f); }
        if (q.To is { } to) { var t = to.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1).AddHours(-7); query = query.Where(o => o.CreatedAt < t); }
        return query;
    }

    public async Task<byte[]> ExportCsvAsync(OrderQuery q, CancellationToken ct = default)
    {
        var rows = await FilterAll(q).OrderByDescending(o => o.CreatedAt).Take(50_000)
            .Select(o => new
            {
                o.OrderCode, o.CreatedAt, o.PaidAt, o.RefundedAt, o.Status, o.User.FullName, o.User.Email,
                Courses = o.Items.Select(i => i.CourseTitle).ToList(),
                o.SubTotal, o.DiscountAmount, o.CouponCode, o.PointsUsed, o.PointsDiscountAmount, o.TotalAmount,
                Payment = o.Payments.Where(p => p.Status == PaymentStatus.Success).OrderBy(p => p.PaidAt)
                    .Select(p => new { p.Provider, p.TransactionRef, p.ProviderTransactionId }).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Mã đơn,Ngày tạo (UTC+7),Ngày thanh toán,Ngày hoàn tiền,Trạng thái,Học viên,Email,Khóa học,Số khóa,Tạm tính,Mã giảm giá,Giảm theo mã,Điểm dùng,Giảm bằng điểm,Thực thu,Cổng thanh toán,Mã giao dịch,Mã giao dịch cổng");
        foreach (var o in rows)
        {
            sb.Append(CsvExport.Field(o.OrderCode)).Append(',').Append(CsvExport.Time(o.CreatedAt)).Append(',')
              .Append(CsvExport.Time(o.PaidAt)).Append(',').Append(CsvExport.Time(o.RefundedAt)).Append(',')
              .Append(StatusText(o.Status)).Append(',').Append(CsvExport.Field(o.FullName)).Append(',').Append(CsvExport.Field(o.Email)).Append(',')
              .Append(CsvExport.Field(string.Join("; ", o.Courses))).Append(',').Append(o.Courses.Count).Append(',')
              .Append(CsvExport.Money(o.SubTotal)).Append(',').Append(CsvExport.Field(o.CouponCode)).Append(',').Append(CsvExport.Money(o.DiscountAmount)).Append(',')
              .Append(o.PointsUsed).Append(',').Append(CsvExport.Money(o.PointsDiscountAmount)).Append(',').Append(CsvExport.Money(o.TotalAmount)).Append(',')
              .Append(o.Payment is null ? "" : CsvExport.Field(PaymentText.Provider(o.Payment.Provider))).Append(',')
              .Append(CsvExport.Field(o.Payment?.TransactionRef)).Append(',').Append(CsvExport.Field(o.Payment?.ProviderTransactionId)).AppendLine();
        }
        return CsvExport.ToBytes(sb);
    }

    private static string StatusText(OrderStatus s) => s switch
    {
        OrderStatus.Pending => "Chờ thanh toán",
        OrderStatus.Paid => "Đã thanh toán",
        OrderStatus.Cancelled => "Đã hủy",
        OrderStatus.Expired => "Hết hạn",
        _ => "Đã hoàn tiền",
    };

    public Task<PagedResult<AdminOrderListItemDto>> ListAllAsync(OrderQuery q, CancellationToken ct = default)
    {
        return FilterAll(q).OrderByDescending(o => o.CreatedAt)
            .Select(o => new AdminOrderListItemDto(o.Id, o.OrderCode, o.Status, o.User.FullName, o.User.Email!,
                o.SubTotal, o.DiscountAmount, o.TotalAmount, o.CouponCode, o.Items.Count, o.CreatedAt, o.PaidAt))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);
    }

    public async Task<int> ExpireStaleOrdersAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var staleIds = await db.Orders.Where(o => o.Status == OrderStatus.Pending && o.ExpiresAt < now).Select(o => o.Id).ToListAsync(ct);
        var count = 0;
        // Mỗi đơn một transaction: đơn hết hạn + hoàn điểm đã dùng đi cùng nhau
        foreach (var id in staleIds)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var o = await db.Orders.FirstOrDefaultAsync(x => x.Id == id && x.Status == OrderStatus.Pending, ct);
            if (o is null) continue;
            o.Status = OrderStatus.Expired;
            await rewards.StageCheckoutRestoreAsync(o, refund: false, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            count++;
        }
        return count;
    }

    private async Task<string> UniqueGiftCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var code = CodeGenerator.Random(12);
            if (!await db.CourseGifts.AnyAsync(g => g.Code == code, ct)) return code;
        }
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

    /// <summary>Như <see cref="RefundAsync"/> nhưng không mở transaction và không SaveChanges (người gọi quản lý).</summary>
    Task<Result> StageRefundAsync(Guid orderId, Guid adminId, string reason, CancellationToken ct = default);
}

public sealed class OrderFulfillmentService(
    IAppDbContext db, IDateTimeProvider clock, INotificationService notifications, Rewards.IRewardService rewards,
    Revenue.IRevenueService revenue, IOrderEmailService emails, ILogger<OrderFulfillmentService> logger)
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

        // IPN đến muộn cho đơn đã hết hạn: điểm của đơn đã được hoàn lúc hết hạn, nên trừ lại
        if (order.Status == OrderStatus.Expired)
            await rewards.StageCheckoutRedebitAsync(order, ct);

        order.Status = OrderStatus.Paid;
        order.PaidAt = now;

        var courseIds = order.Items.Select(i => i.CourseId).ToList();
        var alreadyEnrolled = order.IsGift ? courseIds : await db.Enrollments
            .Where(e => e.UserId == order.UserId && courseIds.Contains(e.CourseId))
            .Select(e => e.CourseId).ToListAsync(ct);
        var courses = await db.Courses.Where(c => courseIds.Contains(c.Id)).ToListAsync(ct);

        // Đơn mua tặng: người mua không được ghi danh; quà chuyển sang "Đã gửi", người nhận nhận bằng mã
        CourseGift? gift = null;
        if (order.IsGift)
        {
            gift = await db.CourseGifts.FirstAsync(g => g.OrderId == order.Id, ct);
            gift.Status = GiftStatus.Sent;
            gift.SentAt = now;
            var recipientId = await db.Users.Where(u => u.NormalizedEmail == gift.RecipientEmail.ToUpper()).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
            if (recipientId is { } rid)
                notifications.Add(rid, "Bạn nhận được quà tặng khóa học", "Mở email hoặc liên kết để nhận khóa học được tặng.",
                    NotificationType.Order, $"/qua-tang/{gift.Code}");
        }

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

        // Mua tặng không xóa giỏ: người mua chưa có các khóa này
        if (!order.IsGift)
        {
            var cartItems = await db.CartItems
                .Where(i => i.Cart.UserId == order.UserId && courseIds.Contains(i.CourseId))
                .ToListAsync(ct);
            foreach (var ci in cartItems) db.CartItems.Remove(ci);
        }

        notifications.Add(order.UserId, "Thanh toán thành công",
            gift is null
                ? $"Đơn hàng {order.OrderCode} đã được thanh toán. Bạn có thể bắt đầu học ngay."
                : $"Đơn hàng {order.OrderCode} đã được thanh toán. Quà tặng đã được gửi tới {gift.RecipientEmail}.",
            NotificationType.Order, gift is null ? "/hoc-vien/khoa-hoc" : $"/don-hang/{order.Id}");

        // Điểm thưởng mua hàng + giới thiệu: cùng transaction với việc xác nhận thanh toán,
        // nên IPN gửi lặp (đơn đã Paid ở trên) không bao giờ cộng điểm lần hai
        await rewards.StagePurchaseRewardsAsync(order, ct);

        // Thu nhập giảng viên: ghi sổ cùng transaction, unique (OrderItemId, Type) chặn ghi lặp
        await revenue.StageSaleEarningsAsync(order, now, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        logger.LogInformation("Đơn {OrderCode} thanh toán thành công qua {Provider}", order.OrderCode, confirmation.Provider);
        await emails.SendReceiptAsync(order.Id, ct);
        if (gift is not null) await emails.SendGiftAsync(order.Id, ct);
        return Result.Success();
    }

    public async Task<Result> RefundAsync(Guid orderId, Guid adminId, string reason, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var result = await StageRefundAsync(orderId, adminId, reason, ct);
        if (result.IsFailure) return result;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogInformation("Admin {AdminId} hoàn tiền đơn {OrderId}", adminId, orderId);
        await emails.SendRefundedAsync(orderId, ct);
        return Result.Success();
    }

    public async Task<Result> StageRefundAsync(Guid orderId, Guid adminId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
            return Result.Failure(Error.Validation("Bắt buộc ghi lý do hoàn tiền (ít nhất 5 ký tự)."));

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return Result.Failure(Error.NotFound("Không tìm thấy đơn hàng."));
        if (order.Status == OrderStatus.Refunded) return Result.Failure(Error.Conflict("Đơn hàng đã được hoàn tiền trước đó."));
        if (order.Status != OrderStatus.Paid) return Result.Failure(Error.Conflict("Chỉ hoàn tiền được đơn đã thanh toán."));

        // Quyền học của đơn (người mua, hoặc người nhận nếu là quà đã nhận)
        var enrollments = await db.Enrollments.Where(e => e.OrderId == order.Id).ToListAsync(ct);
        var enrolledCourseIds = enrollments.Select(e => e.CourseId).ToList();
        var learnerIds = enrollments.Select(e => e.UserId).Append(order.UserId).Distinct().ToList();
        if (await db.Certificates.AnyAsync(c => learnerIds.Contains(c.UserId) && enrolledCourseIds.Contains(c.CourseId) && !c.IsRevoked, ct))
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
        await rewards.StageCheckoutRestoreAsync(order, refund: true, ct);
        await revenue.StageRefundReversalAsync(order, now, ct);

        if (order.IsGift && await db.CourseGifts.FirstOrDefaultAsync(g => g.OrderId == order.Id, ct) is { } gift)
        {
            if (gift.Status != GiftStatus.Redeemed) gift.Status = GiftStatus.Cancelled;
            if (gift.RedeemedById is { } rid && rid != order.UserId)
                notifications.Add(rid, "Quà tặng đã bị thu hồi", $"Đơn quà tặng {order.OrderCode} đã được hoàn tiền cho người tặng; quyền học bị thu hồi.",
                    NotificationType.Order, "/hoc-vien");
        }

        // Yêu cầu hoàn tiền đang chờ của đơn (nếu có) được đóng là "Đã duyệt"
        var pending = await db.RefundRequests.Where(r => r.OrderId == order.Id && r.Status == RefundRequestStatus.Pending).ToListAsync(ct);
        foreach (var r in pending)
        {
            r.Status = RefundRequestStatus.Approved;
            r.ProcessedAt = now;
            r.ProcessedById = adminId;
        }

        notifications.Add(order.UserId, "Đơn hàng đã được hoàn tiền",
            $"Đơn {order.OrderCode} đã được hoàn tiền. Quyền học các khóa trong đơn đã được thu hồi.", NotificationType.Order,
            $"/don-hang/{order.Id}");
        return Result.Success();
    }
}
