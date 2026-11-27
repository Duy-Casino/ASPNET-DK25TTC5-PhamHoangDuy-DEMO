using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearning.API.Common;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Learning;
using OnlineLearning.Domain.Constants;

namespace OnlineLearning.API.Controllers;

/// <summary>Học viên gửi và theo dõi yêu cầu hỗ trợ học tập (xin thêm lượt kiểm tra, video lỗi, tiến độ, chứng chỉ).</summary>
[Route("api/learning/support-requests")]
[Authorize]
public sealed class SupportRequestsController(ILearningAdminService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupportRequestDto>>> Mine([FromQuery] Guid? courseId, CancellationToken ct) =>
        Ok(await service.ListMyRequestsAsync(User.GetUserId(), courseId, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateSupportRequest request, CancellationToken ct) =>
        FromResult(await service.CreateRequestAsync(User.GetUserId(), request, ct));
}

/// <summary>Admin quản lý tiến độ học và xử lý sự cố học tập. Mọi thao tác thay đổi đều bắt buộc lý do và được ghi nhật ký.</summary>
[Route("api/admin/learning")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminLearningController(ILearningAdminService service) : ApiControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<LearningOverviewDto>> Overview(CancellationToken ct) => Ok(await service.GetOverviewAsync(ct));

    [HttpGet("enrollments")]
    public async Task<ActionResult<PagedResult<AdminEnrollmentDto>>> Enrollments([FromQuery] AdminEnrollmentQuery query, CancellationToken ct) =>
        Ok(await service.ListAsync(query, ct));

    [HttpGet("enrollments/{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct) => FromResult(await service.GetDetailAsync(id, ct));

    /// <summary>Cấp thêm lượt làm bài kiểm tra (học viên hết lượt mà chưa đạt).</summary>
    [HttpPost("enrollments/{id:guid}/grant-attempts")]
    public async Task<IActionResult> GrantAttempts(Guid id, GrantAttemptsRequest request, CancellationToken ct) =>
        FromResult(await service.GrantAttemptsAsync(User.GetUserId(), id, request, ct));

    /// <summary>Đánh dấu/bỏ hoàn thành một bài học (sửa lỗi tiến độ không được ghi nhận).</summary>
    [HttpPost("enrollments/{id:guid}/lesson")]
    public async Task<IActionResult> SetLesson(Guid id, SetLessonRequest request, CancellationToken ct) =>
        FromResult(await service.SetLessonAsync(User.GetUserId(), id, request, ct));

    [HttpPost("enrollments/{id:guid}/reset-progress")]
    public async Task<IActionResult> Reset(Guid id, ReasonRequest request, CancellationToken ct) =>
        FromResult(await service.ResetProgressAsync(User.GetUserId(), id, request.Reason, ct));

    /// <summary>Tính lại tiến độ; đủ điều kiện thì cấp chứng chỉ.</summary>
    [HttpPost("enrollments/{id:guid}/recalculate")]
    public async Task<IActionResult> Recalculate(Guid id, ReasonRequest request, CancellationToken ct) =>
        FromResult(await service.RecalculateAsync(User.GetUserId(), id, request.Reason, ct));

    [HttpPost("enrollments/{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, ReasonRequest request, CancellationToken ct) =>
        FromResult(await service.SetAccessAsync(User.GetUserId(), id, false, request.Reason, ct));

    [HttpPost("enrollments/{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, ReasonRequest request, CancellationToken ct) =>
        FromResult(await service.SetAccessAsync(User.GetUserId(), id, true, request.Reason, ct));

    /// <summary>Cấp quyền học thủ công (ví dụ thanh toán ngoài hệ thống, lỗi cổng thanh toán).</summary>
    [HttpPost("enrollments")]
    public async Task<IActionResult> ManualEnroll(ManualEnrollRequest request, CancellationToken ct)
    {
        var result = await service.ManualEnrollAsync(User.GetUserId(), request, ct);
        return result.IsSuccess ? Ok(new { enrollmentId = result.Value }) : Problem(result.Error!);
    }

    [HttpGet("support-requests")]
    public async Task<ActionResult<PagedResult<SupportRequestDto>>> Requests([FromQuery] SupportRequestQuery query, CancellationToken ct) =>
        Ok(await service.ListRequestsAsync(query, ct));

    [HttpPost("support-requests/{id:guid}/respond")]
    public async Task<IActionResult> Respond(Guid id, RespondSupportRequest request, CancellationToken ct) =>
        FromResult(await service.RespondAsync(User.GetUserId(), id, request, ct));
}
