using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OnlineLearning.API.Common;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Certificates;
using OnlineLearning.Domain.Constants;

namespace OnlineLearning.API.Controllers;

[Route("api/certificates")]
public sealed class CertificatesController(ICertificateService certificates) : ApiControllerBase
{
    /// <summary>Trang xác minh công khai: tra cứu theo mã chứng chỉ (FR-CERT-04).</summary>
    [HttpGet("verify/{code}")]
    [EnableRateLimiting(RateLimitPolicies.Payment)]
    public async Task<IActionResult> Verify(string code, CancellationToken ct) =>
        FromResult(await certificates.VerifyAsync(code, ct));

    /// <summary>Tải PDF từ trang xác minh (chứng chỉ còn hiệu lực) — để người nhận hồ sơ lưu bản gốc.</summary>
    [HttpGet("verify/{code}/pdf")]
    [EnableRateLimiting(RateLimitPolicies.Payment)]
    public async Task<IActionResult> VerifyPdf(string code, CancellationToken ct) =>
        Pdf(await certificates.GetPdfByCodeAsync(code, ct));

    /// <summary>Học viên tải chứng chỉ của mình (Admin tải được mọi chứng chỉ, kể cả đã thu hồi).</summary>
    [Authorize]
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> MyPdf(Guid id, CancellationToken ct) =>
        Pdf(await certificates.GetPdfAsync(id, User.GetUserId(), User.IsInRole(Roles.Admin), ct));

    private IActionResult Pdf(Result<(byte[] Pdf, string FileName)> result)
    {
        if (result.IsFailure) return Problem(result.Error!);
        var (bytes, fileName) = result.Value;
        return File(bytes, "application/pdf", fileName);
    }
}

[Route("api/admin/certificates")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminCertificatesController(ICertificateService certificates) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminCertificateDto>>> List([FromQuery] AdminCertificateQuery query, CancellationToken ct) =>
        Ok(await certificates.ListAsync(query, ct));

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, RevokeCertificateRequest request, CancellationToken ct) =>
        FromResult(await certificates.RevokeAsync(id, request.Reason, ct));

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct) => FromResult(await certificates.RestoreAsync(id, ct));

    /// <summary>Cấp chứng chỉ cho các lượt ghi danh đã hoàn thành nhưng chưa có chứng chỉ.</summary>
    [HttpPost("backfill")]
    public async Task<IActionResult> Backfill(CancellationToken ct) => Ok(new { issued = await certificates.BackfillAsync(ct) });
}
