using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearning.API.Common;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Orders;
using OnlineLearning.Application.Features.Rewards;
using OnlineLearning.Domain.Constants;

namespace OnlineLearning.API.Controllers;

/// <summary>Điểm thưởng của học viên. Mọi phép cộng/trừ điểm do backend quyết định; client không gửi số điểm.</summary>
[Route("api/rewards")]
[Authorize]
public sealed class RewardsController(IRewardService rewards) : ApiControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<PointSummaryDto>> Summary(CancellationToken ct) => Ok(await rewards.GetSummaryAsync(User.GetUserId(), ct));

    [HttpGet("transactions")]
    public async Task<ActionResult<PagedResult<PointTransactionDto>>> Transactions([FromQuery] PagedQuery query, CancellationToken ct) =>
        Ok(await rewards.GetTransactionsAsync(User.GetUserId(), query, ct));

    [HttpPost("check-in")]
    public async Task<IActionResult> CheckIn(CancellationToken ct) => FromResult(await rewards.CheckInAsync(User.GetUserId(), ct));

    /// <summary>Tạo (hoặc dùng lại trong ngày) liên kết chia sẻ riêng cho khóa học.</summary>
    [HttpPost("shares")]
    public async Task<IActionResult> CreateShare(CreateShareRequest request, CancellationToken ct) =>
        FromResult(await rewards.CreateShareAsync(User.GetUserId(), request, ClientKeys.Ip(HttpContext), ct));

    /// <summary>
    /// Trang khóa học gọi khi được mở từ liên kết chia sẻ (?s=token). Luôn trả 204 — không tiết lộ có thưởng hay không.
    /// Lượt mở của chính người chia sẻ (cùng tài khoản/IP) và lượt mở lặp của cùng người xem không được tính.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("shares/{token}/open")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimitPolicies.Payment)]
    public async Task<IActionResult> OpenShare(string token, CancellationToken ct)
    {
        Guid? visitor = User.Identity?.IsAuthenticated == true ? User.GetUserId() : null;
        await rewards.RecordShareOpenAsync(token, visitor, ClientKeys.Ip(HttpContext), ClientKeys.Visitor(HttpContext), ct);
        return NoContent();
    }

    [HttpPost("redeem")]
    public async Task<IActionResult> Redeem(RedeemRequest request, CancellationToken ct) =>
        FromResult(await rewards.RedeemAsync(User.GetUserId(), request, ct));

    [HttpGet("redemptions")]
    public async Task<ActionResult<IReadOnlyList<RedemptionDto>>> Redemptions(CancellationToken ct) =>
        Ok(await rewards.GetRedemptionsAsync(User.GetUserId(), ct));
}

/// <summary>Quản lý điểm thưởng (Admin).</summary>
[Route("api/admin/rewards")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminRewardsController(IRewardAdminService admin, IOrderFulfillmentService fulfillment) : ApiControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<RewardOverviewDto>> Overview(CancellationToken ct) => Ok(await admin.GetOverviewAsync(ct));

    [HttpGet("balances")]
    public async Task<ActionResult<PagedResult<UserBalanceDto>>> Balances([FromQuery] BalanceQuery query, CancellationToken ct) =>
        Ok(await admin.ListBalancesAsync(query, ct));

    [HttpGet("transactions")]
    public async Task<ActionResult<PagedResult<AdminPointTransactionDto>>> Transactions([FromQuery] PointTransactionQuery query, CancellationToken ct) =>
        Ok(await admin.ListTransactionsAsync(query, ct));

    [HttpGet("transactions/export")]
    public async Task<IActionResult> Export([FromQuery] PointTransactionQuery query, CancellationToken ct) =>
        File(await admin.ExportTransactionsCsvAsync(query, ct), "text/csv; charset=utf-8", $"diem-thuong-{DateTime.UtcNow.AddHours(7):yyyyMMdd-HHmm}.csv");

    [HttpGet("rules")]
    public async Task<ActionResult<IReadOnlyList<PointRuleDto>>> Rules(CancellationToken ct) => Ok(await admin.ListRulesAsync(ct));

    [HttpPost("rules")]
    public async Task<IActionResult> CreateRule(SavePointRuleRequest request, CancellationToken ct) => FromResult(await admin.CreateRuleAsync(request, ct));

    [HttpPut("rules/{id:guid}")]
    public async Task<IActionResult> UpdateRule(Guid id, SavePointRuleRequest request, CancellationToken ct) =>
        FromResult(await admin.UpdateRuleAsync(id, request, ct));

    [HttpDelete("rules/{id:guid}")]
    public async Task<IActionResult> DeleteRule(Guid id, CancellationToken ct) => FromResult(await admin.DeleteRuleAsync(id, ct));

    /// <summary>Cộng/trừ điểm thủ công, bắt buộc có lý do; không trừ quá số dư.</summary>
    [HttpPost("adjustments")]
    public async Task<IActionResult> Adjust(AdjustPointsRequest request, CancellationToken ct) =>
        FromResult(await admin.AdjustAsync(User.GetUserId(), request, ct));

    /// <summary>Xác nhận đã hoàn tiền đơn hàng: thu hồi quyền học và điểm thưởng của đơn.</summary>
    [HttpPost("/api/admin/orders/{orderId:guid}/refund")]
    public async Task<IActionResult> Refund(Guid orderId, RefundOrderRequest request, CancellationToken ct) =>
        FromResult(await fulfillment.RefundAsync(orderId, User.GetUserId(), request.Reason, ct));
}

public sealed record RefundOrderRequest(string Reason);

/// <summary>Khóa ẩn danh của client (băm SHA-256), không lưu IP thô.</summary>
internal static class ClientKeys
{
    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("eduviet-share:" + value)));

    public static string Ip(HttpContext http) => Hash(http.Connection.RemoteIpAddress?.ToString() ?? "unknown");

    public static string Visitor(HttpContext http) =>
        Hash((http.Connection.RemoteIpAddress?.ToString() ?? "unknown") + "|" + http.Request.Headers.UserAgent);
}
