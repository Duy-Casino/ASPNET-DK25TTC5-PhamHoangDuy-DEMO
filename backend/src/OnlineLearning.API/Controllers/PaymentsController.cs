using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using OnlineLearning.API.Common;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Payments;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Enums;
using OnlineLearning.Infrastructure.Payments;

namespace OnlineLearning.API.Controllers;

/// <summary>
/// Thanh toán. Quyền học CHỈ được cấp trong các endpoint IPN (server-to-server) sau khi xác thực chữ ký + số tiền.
/// Endpoint "return" chỉ đọc trạng thái để hiển thị.
/// </summary>
[Route("api/payments")]
public sealed class PaymentsController(IPaymentService payments, ILogger<PaymentsController> logger) : ApiControllerBase
{
    /// <summary>Các phương thức thanh toán đang bật (VNPay/MoMo khi có khóa sandbox, kênh test ở môi trường phát triển).</summary>
    [HttpGet("methods")]
    public ActionResult<IReadOnlyList<PaymentMethodDto>> Methods() => Ok(payments.GetMethods());

    /// <summary>Tạo giao dịch cho đơn hàng và trả URL chuyển hướng tới cổng.</summary>
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Payment)]
    [HttpPost("orders/{orderId:guid}/checkout")]
    public async Task<IActionResult> Checkout(Guid orderId, CheckoutRequest request, CancellationToken ct) =>
        FromResult(await payments.CheckoutAsync(User.GetUserId(), orderId, request.Provider, HttpContext.Connection.RemoteIpAddress?.ToString(), ct));

    /// <summary>Trang kết quả gọi endpoint này với nguyên query cổng trả về; chỉ để hiển thị.</summary>
    [Authorize]
    [HttpGet("return/{provider}")]
    public async Task<IActionResult> Return(PaymentProvider provider, CancellationToken ct) =>
        FromResult(await payments.GetReturnResultAsync(User.GetUserId(), provider, Query(), ct));

    // ---------------- IPN ----------------

    /// <summary>VNPay IPN (GET). Phản hồi theo đặc tả VNPay: RspCode 00/01/02/04/97/99.</summary>
    [HttpGet("vnpay/ipn")]
    [EnableRateLimiting(RateLimitPolicies.Payment)]
    public async Task<IActionResult> VnPayIpn(CancellationToken ct)
    {
        var outcome = await payments.HandleCallbackAsync(PaymentProvider.VNPay, Query(), ct);
        var (code, message) = outcome switch
        {
            CallbackOutcome.Success or CallbackOutcome.PaymentFailed => ("00", "Confirm Success"),
            CallbackOutcome.AlreadyProcessed => ("02", "Order already confirmed"),
            CallbackOutcome.NotFound => ("01", "Order not found"),
            CallbackOutcome.InvalidAmount => ("04", "Invalid amount"),
            CallbackOutcome.InvalidSignature => ("97", "Invalid signature"),
            _ => ("99", "Unknown error"),
        };
        // VNPay yêu cầu đúng tên trường "RspCode"/"Message" (không camelCase)
        return new JsonResult(new { RspCode = code, Message = message }, new System.Text.Json.JsonSerializerOptions());
    }

    /// <summary>MoMo IPN (POST JSON). MoMo chỉ cần HTTP 204 khi đã nhận.</summary>
    [HttpPost("momo/ipn")]
    [EnableRateLimiting(RateLimitPolicies.Payment)]
    public async Task<IActionResult> MoMoIpn([FromBody] Dictionary<string, System.Text.Json.JsonElement> body, CancellationToken ct)
    {
        var data = body.ToDictionary(kv => kv.Key, kv => kv.Value.ValueKind == System.Text.Json.JsonValueKind.String ? kv.Value.GetString() ?? "" : kv.Value.ToString());
        var outcome = await payments.HandleCallbackAsync(PaymentProvider.MoMo, data, ct);
        if (outcome is CallbackOutcome.InvalidSignature or CallbackOutcome.InvalidAmount)
            logger.LogWarning("MoMo IPN bị từ chối: {Outcome}", outcome);
        return outcome is CallbackOutcome.InvalidSignature ? BadRequest() : NoContent();
    }

    /// <summary>
    /// "Cổng" của kênh thanh toán TEST: người dùng được chuyển tới đây, cổng giả lập gửi IPN có chữ ký cho hệ thống
    /// (đi qua đúng bước xác thực như VNPay/MoMo) rồi chuyển người dùng về trang kết quả. Không thu tiền thật.
    /// </summary>
    [HttpGet("test/gateway")]
    public async Task<IActionResult> TestGateway(
        [FromQuery(Name = "ref")] string txnRef, [FromQuery] string amount, [FromQuery] string returnUrl, [FromQuery] string sig,
        [FromServices] TestPaymentGateway gateway, [FromServices] IOptions<PaymentOptions> options, CancellationToken ct)
    {
        if (!gateway.IsEnabled) return NotFound();
        if (!gateway.VerifyGatewayRequest(txnRef, amount, returnUrl, sig) || !returnUrl.StartsWith(options.Value.ReturnUrl, StringComparison.Ordinal))
            return BadRequest("Yêu cầu thanh toán thử nghiệm không hợp lệ.");

        var callback = gateway.BuildCallback(txnRef, amount);
        var outcome = await payments.HandleCallbackAsync(PaymentProvider.Test, callback, ct);
        logger.LogInformation("Kênh test xử lý {Ref}: {Outcome}", txnRef, outcome);

        var query = string.Join('&', callback.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        return Redirect($"{returnUrl}?{query}");
    }

    private Dictionary<string, string> Query() => Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
}

[Route("api/admin/payments")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminPaymentsController(IPaymentService payments) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminPaymentDto>>> List([FromQuery] PaymentQuery query, CancellationToken ct) =>
        Ok(await payments.ListAsync(query, ct));
}
