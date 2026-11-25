using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearning.API.Common;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Orders;
using OnlineLearning.Application.Features.Revenue;
using OnlineLearning.Domain.Constants;

namespace OnlineLearning.API.Controllers;

/// <summary>Thu nhập và rút tiền của giảng viên.</summary>
[Route("api/instructor")]
[Authorize(Roles = Roles.Instructor)]
public sealed class InstructorEarningsController(IRevenueService revenue) : ApiControllerBase
{
    [HttpGet("earnings/summary")]
    public async Task<ActionResult<EarningsSummaryDto>> Summary(CancellationToken ct) =>
        Ok(await revenue.GetSummaryAsync(User.GetUserId(), ct));

    [HttpGet("earnings")]
    public async Task<ActionResult<PagedResult<EarningDto>>> Earnings([FromQuery] EarningQuery query, CancellationToken ct) =>
        Ok(await revenue.ListEarningsAsync(User.GetUserId(), query, ct));

    [HttpGet("payouts")]
    public async Task<ActionResult<PagedResult<PayoutDto>>> Payouts([FromQuery] PagedQuery query, CancellationToken ct) =>
        Ok(await revenue.ListMyPayoutsAsync(User.GetUserId(), query, ct));

    /// <summary>Gửi yêu cầu rút tiền (tối đa số dư có thể rút; mỗi lúc chỉ một yêu cầu chờ xử lý).</summary>
    [HttpPost("payouts")]
    public async Task<IActionResult> RequestPayout(RequestPayoutRequest request, CancellationToken ct) =>
        FromResult(await revenue.RequestPayoutAsync(User.GetUserId(), request, ct));

    [HttpPost("payouts/{id:guid}/cancel")]
    public async Task<IActionResult> CancelPayout(Guid id, CancellationToken ct) =>
        FromResult(await revenue.CancelPayoutAsync(User.GetUserId(), id, ct));
}

/// <summary>Admin: doanh thu nền tảng, chi trả giảng viên, yêu cầu hoàn tiền.</summary>
[Route("api/admin")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminRevenueController(IRevenueService revenue, IRefundRequestService refunds) : ApiControllerBase
{
    [HttpGet("revenue")]
    public async Task<ActionResult<RevenueOverviewDto>> Overview(CancellationToken ct) => Ok(await revenue.GetOverviewAsync(ct));

    [HttpGet("payouts")]
    public async Task<ActionResult<PagedResult<AdminPayoutDto>>> Payouts([FromQuery] AdminPayoutQuery query, CancellationToken ct) =>
        Ok(await revenue.ListPayoutsAsync(query, ct));

    [HttpGet("payouts/export")]
    public async Task<IActionResult> ExportPayouts([FromQuery] AdminPayoutQuery query, CancellationToken ct) =>
        File(await revenue.ExportPayoutsCsvAsync(query, ct), "text/csv; charset=utf-8", $"chi-tra-giang-vien-{DateTime.UtcNow.AddHours(7):yyyyMMdd-HHmm}.csv");

    /// <summary>Xác nhận đã chuyển khoản cho giảng viên (chuyển ngoài hệ thống, ghi mã giao dịch).</summary>
    [HttpPost("payouts/{id:guid}/paid")]
    public async Task<IActionResult> MarkPaid(Guid id, MarkPayoutPaidRequest request, CancellationToken ct) =>
        FromResult(await revenue.MarkPayoutPaidAsync(User.GetUserId(), id, request, ct));

    [HttpPost("payouts/{id:guid}/reject")]
    public async Task<IActionResult> RejectPayout(Guid id, RejectPayoutRequest request, CancellationToken ct) =>
        FromResult(await revenue.RejectPayoutAsync(User.GetUserId(), id, request, ct));

    [HttpGet("refund-requests")]
    public async Task<ActionResult<PagedResult<AdminRefundRequestDto>>> RefundRequests([FromQuery] AdminRefundQuery query, CancellationToken ct) =>
        Ok(await refunds.ListAsync(query, ct));

    /// <summary>Duyệt: hoàn tiền đơn (thu hồi quyền học, điểm thưởng, thu nhập giảng viên). Admin hoàn tiền cho khách qua cổng thanh toán.</summary>
    [HttpPost("refund-requests/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, ProcessRefundRequest request, CancellationToken ct) =>
        FromResult(await refunds.ApproveAsync(User.GetUserId(), id, request, ct));

    [HttpPost("refund-requests/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, ProcessRefundRequest request, CancellationToken ct) =>
        FromResult(await refunds.RejectAsync(User.GetUserId(), id, request, ct));
}
