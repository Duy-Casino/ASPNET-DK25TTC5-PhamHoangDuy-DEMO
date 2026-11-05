using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Orders;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Payments;

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";
    /// <summary>Trang kết quả phía Frontend mà cổng chuyển người dùng về.</summary>
    public string ReturnUrl { get; set; } = "http://localhost:5173/thanh-toan/ket-qua";
}

public sealed record PaymentMethodDto(PaymentProvider Provider, string DisplayName, bool IsTestMode);
public sealed record CheckoutRequest(PaymentProvider Provider);
public sealed record CheckoutResponse(Guid OrderId, PaymentProvider Provider, string RedirectUrl, bool IsTestMode);

/// <summary>Kết quả xử lý callback, controller ánh xạ sang mã phản hồi riêng của từng cổng.</summary>
public enum CallbackOutcome { Success, AlreadyProcessed, PaymentFailed, InvalidSignature, NotFound, InvalidAmount, Error }

public sealed record PaymentResultDto(
    Guid? OrderId, string? OrderCode, OrderStatus? OrderStatus, PaymentProvider? Provider, PaymentStatus? PaymentStatus,
    bool SignatureValid, bool GatewayReportedSuccess, bool IsTestMode, decimal Amount, string? Message);

public sealed record AdminPaymentDto(Guid Id, Guid OrderId, string OrderCode, string UserEmail, PaymentProvider Provider, decimal Amount,
    PaymentStatus Status, string TransactionRef, string? ProviderTransactionId, string? BankCode, string? ResponseCode, DateTime CreatedAt, DateTime? PaidAt);

public sealed class PaymentQuery : PagedQuery
{
    public PaymentStatus? Status { get; set; }
    public PaymentProvider? Provider { get; set; }
    public string? Keyword { get; set; }
}

public sealed class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    public CheckoutRequestValidator() => RuleFor(x => x.Provider).IsInEnum().NotEqual(PaymentProvider.Free);
}

public interface IPaymentService
{
    IReadOnlyList<PaymentMethodDto> GetMethods();
    Task<Result<CheckoutResponse>> CheckoutAsync(Guid userId, Guid orderId, PaymentProvider provider, string? clientIp, CancellationToken ct = default);
    /// <summary>Xử lý IPN (server-to-server). Đây là nơi DUY NHẤT dẫn tới cấp quyền học cho đơn có phí.</summary>
    Task<CallbackOutcome> HandleCallbackAsync(PaymentProvider provider, IReadOnlyDictionary<string, string> data, CancellationToken ct = default);
    /// <summary>Trang kết quả: chỉ xác thực chữ ký để hiển thị, KHÔNG cấp quyền (BR: không tin redirect).</summary>
    Task<Result<PaymentResultDto>> GetReturnResultAsync(Guid userId, PaymentProvider provider, IReadOnlyDictionary<string, string> data, CancellationToken ct = default);
    Task<PagedResult<AdminPaymentDto>> ListAsync(PaymentQuery query, CancellationToken ct = default);
}

public sealed class PaymentService(
    IAppDbContext db,
    IEnumerable<IPaymentGateway> gateways,
    IOrderFulfillmentService fulfillment,
    IDateTimeProvider clock,
    IOptions<PaymentOptions> options,
    ILogger<PaymentService> logger) : IPaymentService
{
    private IPaymentGateway? Gateway(PaymentProvider p) => gateways.FirstOrDefault(g => g.Provider == p && g.IsEnabled);

    public IReadOnlyList<PaymentMethodDto> GetMethods() =>
        gateways.Where(g => g.IsEnabled).Select(g => new PaymentMethodDto(g.Provider, g.DisplayName, g.IsTestMode)).ToList();

    public async Task<Result<CheckoutResponse>> CheckoutAsync(Guid userId, Guid orderId, PaymentProvider provider, string? clientIp, CancellationToken ct = default)
    {
        var gateway = Gateway(provider);
        if (gateway is null) return Error.Validation("Phương thức thanh toán chưa được bật.");

        var order = await db.Orders.Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct);
        if (order is null) return Error.NotFound("Không tìm thấy đơn hàng.");
        if (order.Status == OrderStatus.Paid) return Error.Conflict("Đơn hàng đã được thanh toán.");
        if (order.Status != OrderStatus.Pending) return Error.Conflict("Đơn hàng không còn ở trạng thái chờ thanh toán.");
        var now = clock.UtcNow;
        if (order.ExpiresAt <= now) return Error.Conflict("Đơn hàng đã hết hạn thanh toán. Vui lòng đặt lại.");
        if (order.TotalAmount <= 0) return Error.Validation("Đơn 0đ không cần thanh toán.");

        // Mỗi lần bấm thanh toán là một mã giao dịch mới (VNPay không cho dùng lại TxnRef)
        var payment = new Payment
        {
            OrderId = order.Id,
            Provider = provider,
            Amount = order.TotalAmount,
            Status = PaymentStatus.Pending,
            TransactionRef = $"{order.OrderCode}{order.Payments.Count + 1:D2}{now:HHmmss}",
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);

        // Không gắn query vào ReturnUrl: cổng tự nối tham số của nó; Frontend nhận biết cổng qua tên tham số
        var returnUrl = options.Value.ReturnUrl;
        var session = await gateway.CreatePaymentAsync(
            new GatewayPaymentRequest(payment.TransactionRef, order.TotalAmount, $"Thanh toan don hang {order.OrderCode}", returnUrl, clientIp, order.ExpiresAt), ct);

        logger.LogInformation("Tạo giao dịch {Ref} ({Provider}) cho đơn {OrderCode}", payment.TransactionRef, provider, order.OrderCode);
        return new CheckoutResponse(order.Id, provider, session.RedirectUrl, gateway.IsTestMode);
    }

    public async Task<CallbackOutcome> HandleCallbackAsync(PaymentProvider provider, IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
    {
        var gateway = gateways.FirstOrDefault(g => g.Provider == provider);
        if (gateway is null) return CallbackOutcome.Error;

        var cb = gateway.ParseCallback(data);
        if (!cb.SignatureValid)
        {
            logger.LogWarning("IPN {Provider} sai chữ ký: {Raw}", provider, cb.RawData);
            return CallbackOutcome.InvalidSignature;
        }

        var payment = await db.Payments.Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.TransactionRef == cb.TransactionRef && p.Provider == provider, ct);
        if (payment is null) return CallbackOutcome.NotFound;

        // BR-07: số tiền cổng báo phải khớp số tiền giao dịch và tổng đơn
        if (cb.Amount != payment.Amount || cb.Amount != payment.Order.TotalAmount)
        {
            logger.LogWarning("IPN {Ref} sai số tiền: cổng {GatewayAmount}, hệ thống {Amount}", cb.TransactionRef, cb.Amount, payment.Amount);
            return CallbackOutcome.InvalidAmount;
        }

        // BR-06: idempotency — đã xử lý giao dịch này rồi thì không xử lý lại
        if (payment.Status != PaymentStatus.Pending)
            return CallbackOutcome.AlreadyProcessed;

        if (!cb.IsSuccess)
        {
            payment.Status = PaymentStatus.Failed;
            payment.ResponseCode = cb.ResponseCode;
            payment.ProviderTransactionId = cb.ProviderTransactionId;
            payment.RawResponse = cb.RawData;
            await db.SaveChangesAsync(ct);
            return CallbackOutcome.PaymentFailed;
        }

        if (payment.Order.Status == OrderStatus.Paid)
        {
            // Đơn đã được thanh toán bằng giao dịch khác (người dùng thanh toán 2 lần) → ghi nhận để đối soát hoàn tiền
            payment.Status = PaymentStatus.Success;
            payment.PaidAt = clock.UtcNow;
            payment.ProviderTransactionId = cb.ProviderTransactionId;
            payment.ResponseCode = cb.ResponseCode;
            payment.RawResponse = cb.RawData;
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Đơn {OrderCode} nhận thêm giao dịch thành công {Ref} — cần đối soát hoàn tiền", payment.Order.OrderCode, payment.TransactionRef);
            return CallbackOutcome.AlreadyProcessed;
        }

        try
        {
            var result = await fulfillment.MarkPaidAsync(payment.OrderId,
                new PaymentConfirmation(provider, payment.TransactionRef, cb.ProviderTransactionId, cb.Amount, cb.BankCode, cb.ResponseCode, cb.RawData), ct);
            return result.IsSuccess ? CallbackOutcome.Success : CallbackOutcome.Error;
        }
        catch (DbUpdateException ex)
        {
            // Hai IPN đến cùng lúc: unique index / rowversion chặn bản thứ hai
            logger.LogWarning(ex, "IPN trùng lặp cho {Ref}", payment.TransactionRef);
            return CallbackOutcome.AlreadyProcessed;
        }
    }

    public async Task<Result<PaymentResultDto>> GetReturnResultAsync(Guid userId, PaymentProvider provider, IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
    {
        var gateway = gateways.FirstOrDefault(g => g.Provider == provider);
        if (gateway is null) return Error.Validation("Phương thức thanh toán không hợp lệ.");

        var cb = gateway.ParseCallback(data);
        if (cb.TransactionRef is null) return Error.Validation("Thiếu mã giao dịch.");

        var payment = await db.Payments.AsNoTracking().Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.TransactionRef == cb.TransactionRef && p.Provider == provider, ct);
        if (payment is null || payment.Order.UserId != userId) return Error.NotFound("Không tìm thấy giao dịch.");

        var message = !cb.SignatureValid ? "Dữ liệu trả về không hợp lệ (sai chữ ký)."
            : payment.Order.Status == OrderStatus.Paid ? "Thanh toán thành công."
            : cb.IsSuccess ? "Cổng thanh toán báo thành công, hệ thống đang chờ xác nhận."
            : "Giao dịch không thành công hoặc đã bị hủy.";

        return new PaymentResultDto(payment.OrderId, payment.Order.OrderCode, payment.Order.Status, provider, payment.Status,
            cb.SignatureValid, cb.SignatureValid && cb.IsSuccess, gateway.IsTestMode, payment.Amount, message);
    }

    public Task<PagedResult<AdminPaymentDto>> ListAsync(PaymentQuery q, CancellationToken ct = default)
    {
        var query = db.Payments.AsNoTracking().AsQueryable();
        if (q.Status is { } s) query = query.Where(p => p.Status == s);
        if (q.Provider is { } pr) query = query.Where(p => p.Provider == pr);
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(p => p.TransactionRef.Contains(kw) || p.Order.OrderCode.Contains(kw) || p.Order.User.Email!.Contains(kw)
                                     || (p.ProviderTransactionId != null && p.ProviderTransactionId.Contains(kw)));
        }
        return query.OrderByDescending(p => p.CreatedAt)
            .Select(p => new AdminPaymentDto(p.Id, p.OrderId, p.Order.OrderCode, p.Order.User.Email!, p.Provider, p.Amount, p.Status,
                p.TransactionRef, p.ProviderTransactionId, p.BankCode, p.ResponseCode, p.CreatedAt, p.PaidAt))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);
    }
}
