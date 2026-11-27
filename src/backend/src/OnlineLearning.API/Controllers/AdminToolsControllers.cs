using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearning.API.Common;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Admin;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.API.Controllers;

/// <summary>Công cụ quản trị: đánh giá, hồ sơ giảng viên, nhật ký, thông báo, cài đặt, báo cáo, trạng thái hệ thống, xuất dữ liệu.</summary>
[Route("api/admin")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminToolsController(IAdminToolsService tools, ISettingsService settings, IReportService reports, ISystemStatusService system) : ApiControllerBase
{
    private static string Stamp => DateTime.UtcNow.AddHours(7).ToString("yyyyMMdd-HHmm");

    [HttpGet("reviews")]
    public async Task<ActionResult<PagedResult<AdminReviewDto>>> Reviews([FromQuery] AdminReviewQuery query, CancellationToken ct) => Ok(await tools.ListReviewsAsync(query, ct));

    [HttpGet("instructor-applications")]
    public async Task<ActionResult<PagedResult<InstructorApplicationDto>>> Applications([FromQuery] InstructorApplicationStatus? status, [FromQuery] PagedQuery query, CancellationToken ct) =>
        Ok(await tools.ListApplicationsAsync(status, query, ct));

    [HttpPost("instructor-applications/{id:guid}/approve")]
    public async Task<IActionResult> ApproveApplication(Guid id, ProcessApplicationRequest request, CancellationToken ct) =>
        FromResult(await tools.ApproveApplicationAsync(User.GetUserId(), id, request.AdminNote, ct));

    [HttpPost("instructor-applications/{id:guid}/reject")]
    public async Task<IActionResult> RejectApplication(Guid id, ProcessApplicationRequest request, CancellationToken ct) =>
        FromResult(await tools.RejectApplicationAsync(User.GetUserId(), id, request.AdminNote, ct));

    [HttpGet("audit-logs")]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> Audit([FromQuery] AuditLogQuery query, CancellationToken ct) => Ok(await tools.ListAuditAsync(query, ct));

    [HttpGet("audit-logs/export")]
    public async Task<IActionResult> ExportAudit([FromQuery] AuditLogQuery query, CancellationToken ct) =>
        File(await tools.ExportAuditCsvAsync(query, ct), "text/csv; charset=utf-8", $"nhat-ky-admin-{Stamp}.csv");

    [HttpGet("announcements")]
    public async Task<ActionResult<PagedResult<AnnouncementDto>>> Announcements([FromQuery] PagedQuery query, CancellationToken ct) => Ok(await tools.ListAnnouncementsAsync(query, ct));

    [HttpGet("announcements/preview")]
    public async Task<ActionResult<AudiencePreviewDto>> Preview([FromQuery] AnnouncementAudience audience, [FromQuery] Guid? courseId, CancellationToken ct) =>
        Ok(await tools.PreviewAudienceAsync(audience, courseId, ct));

    [HttpPost("announcements")]
    public async Task<IActionResult> SendAnnouncement(SendAnnouncementRequest request, CancellationToken ct) =>
        FromResult(await tools.SendAnnouncementAsync(User.GetUserId(), request, ct));

    [HttpGet("settings")]
    public async Task<ActionResult<IReadOnlyList<SettingDto>>> Settings(CancellationToken ct) => Ok(await settings.GetAllAsync(ct));

    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings(SaveSettingsRequest request, CancellationToken ct) =>
        FromResult(await settings.SaveAsync(User.GetUserId(), request, ct));

    [HttpGet("reports")]
    public async Task<ActionResult<AnalyticsReportDto>> Report([FromQuery] ReportQuery query, CancellationToken ct) => Ok(await reports.GetAsync(query, ct));

    [HttpGet("reports/export")]
    public async Task<IActionResult> ExportReport([FromQuery] ReportQuery query, CancellationToken ct) =>
        File(await reports.ExportCoursesCsvAsync(query, ct), "text/csv; charset=utf-8", $"bao-cao-khoa-hoc-{Stamp}.csv");

    [HttpGet("reports/courses/{courseId:guid}/lessons")]
    public async Task<IActionResult> LessonStats(Guid courseId, CancellationToken ct) => FromResult(await reports.GetLessonStatsAsync(courseId, null, ct));

    [HttpGet("system")]
    public async Task<ActionResult<SystemStatusDto>> System(CancellationToken ct) => Ok(await system.GetAsync(ct));

    [HttpPost("system/jobs/{key}/run")]
    public async Task<IActionResult> RunJob(string key, CancellationToken ct)
    {
        var result = await system.RunJobAsync(key, ct);
        return result.IsSuccess ? Ok(new { message = result.Value }) : Problem(result.Error!);
    }

    [HttpGet("users/export")]
    public async Task<IActionResult> ExportUsers([FromQuery] AdminUserQuery query, CancellationToken ct) =>
        File(await tools.ExportUsersCsvAsync(query, ct), "text/csv; charset=utf-8", $"nguoi-dung-{Stamp}.csv");
}

/// <summary>Người dùng đăng ký làm giảng viên.</summary>
[Route("api/instructor-applications")]
[Authorize]
public sealed class InstructorApplicationsController(IAdminToolsService tools) : ApiControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<MyApplicationStatusDto>> Mine(CancellationToken ct) => Ok(await tools.GetMyApplicationAsync(User.GetUserId(), ct));

    [HttpPost]
    public async Task<IActionResult> Submit(SubmitInstructorApplicationRequest request, CancellationToken ct) =>
        FromResult(await tools.SubmitApplicationAsync(User.GetUserId(), request, ct));
}

/// <summary>Cài đặt công khai cho Frontend (chính sách hoàn tiền, thông tin liên hệ bên bán).</summary>
[Route("api/settings")]
public sealed class PublicSettingsController(ISettingsService settings) : ApiControllerBase
{
    [HttpGet("public")]
    public ActionResult<PublicSettingsDto> Public() => Ok(settings.GetPublic());
}

/// <summary>Giảng viên xem tỷ lệ hoàn thành từng bài của khóa học mình.</summary>
[Route("api/instructor/courses")]
[Authorize(Roles = $"{Roles.Instructor},{Roles.Admin}")]
public sealed class InstructorLessonStatsController(IReportService reports) : ApiControllerBase
{
    [HttpGet("{courseId:guid}/lesson-stats")]
    public async Task<IActionResult> Get(Guid courseId, CancellationToken ct) =>
        FromResult(await reports.GetLessonStatsAsync(courseId, User.IsInRole(Roles.Admin) ? null : User.GetUserId(), ct));
}
