using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearning.API.Common;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Courses;
using OnlineLearning.Application.Features.Orders;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.API.Controllers;

/// <summary>Khu vực giảng viên: quản lý khóa học, chương, bài học, tài liệu, đánh giá.</summary>
[Route("api/instructor")]
[Authorize(Roles = $"{Roles.Instructor},{Roles.Admin}")]
public sealed class InstructorController(IInstructorCourseService courses, IReviewService reviews,
    Application.Features.Instructors.IInstructorProfileService profiles) : ApiControllerBase
{
    // ---------- Hồ sơ giảng viên ----------
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken ct) => FromResult(await profiles.GetMineAsync(User.GetUserId(), ct));

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(Application.Features.Instructors.UpdateInstructorProfileRequest request, CancellationToken ct) =>
        FromResult(await profiles.UpdateMineAsync(User.GetUserId(), request, ct));

    // ---------- Courses ----------
    [HttpGet("courses")]
    public async Task<ActionResult<PagedResult<InstructorCourseListItemDto>>> MyCourses(
        [FromQuery] CourseStatus? status, [FromQuery] PagedQuery query, CancellationToken ct) =>
        Ok(await courses.ListMineAsync(User.GetUserId(), status, query, ct));

    [HttpGet("courses/{id:guid}")]
    public async Task<IActionResult> GetCourse(Guid id, CancellationToken ct) => FromResult(await courses.GetForEditAsync(id, ct));

    [HttpPost("courses")]
    public async Task<IActionResult> CreateCourse(SaveCourseRequest request, CancellationToken ct) =>
        Created(await courses.CreateAsync(request, ct), nameof(GetCourse), c => new { id = c.Id });

    [HttpPut("courses/{id:guid}")]
    public async Task<IActionResult> UpdateCourse(Guid id, SaveCourseRequest request, CancellationToken ct) =>
        FromResult(await courses.UpdateAsync(id, request, ct));

    [HttpDelete("courses/{id:guid}")]
    public async Task<IActionResult> DeleteCourse(Guid id, CancellationToken ct) => FromResult(await courses.DeleteAsync(id, ct));

    /// <summary>Gửi khóa học cho Admin duyệt (Draft/Rejected → Pending).</summary>
    [HttpPost("courses/{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct) => FromResult(await courses.SubmitForReviewAsync(id, ct));

    /// <summary>Chuyển khóa học về bản nháp để chỉnh sửa (học viên đã mua vẫn học được).</summary>
    [HttpPost("courses/{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct) => FromResult(await courses.UnpublishAsync(id, ct));

    // ---------- Sections ----------
    [HttpPost("courses/{courseId:guid}/sections")]
    public async Task<IActionResult> AddSection(Guid courseId, SaveSectionRequest request, CancellationToken ct) =>
        FromResult(await courses.AddSectionAsync(courseId, request, ct));

    [HttpPut("courses/{courseId:guid}/sections/order")]
    public async Task<IActionResult> ReorderSections(Guid courseId, ReorderRequest request, CancellationToken ct) =>
        FromResult(await courses.ReorderSectionsAsync(courseId, request, ct));

    [HttpPut("sections/{sectionId:guid}")]
    public async Task<IActionResult> UpdateSection(Guid sectionId, SaveSectionRequest request, CancellationToken ct) =>
        FromResult(await courses.UpdateSectionAsync(sectionId, request, ct));

    [HttpDelete("sections/{sectionId:guid}")]
    public async Task<IActionResult> DeleteSection(Guid sectionId, CancellationToken ct) =>
        FromResult(await courses.DeleteSectionAsync(sectionId, ct));

    // ---------- Lessons ----------
    [HttpPost("sections/{sectionId:guid}/lessons")]
    public async Task<IActionResult> AddLesson(Guid sectionId, SaveLessonRequest request, CancellationToken ct) =>
        FromResult(await courses.AddLessonAsync(sectionId, request, ct));

    [HttpPut("sections/{sectionId:guid}/lessons/order")]
    public async Task<IActionResult> ReorderLessons(Guid sectionId, ReorderRequest request, CancellationToken ct) =>
        FromResult(await courses.ReorderLessonsAsync(sectionId, request, ct));

    [HttpPut("lessons/{lessonId:guid}")]
    public async Task<IActionResult> UpdateLesson(Guid lessonId, SaveLessonRequest request, CancellationToken ct) =>
        FromResult(await courses.UpdateLessonAsync(lessonId, request, ct));

    [HttpDelete("lessons/{lessonId:guid}")]
    public async Task<IActionResult> DeleteLesson(Guid lessonId, CancellationToken ct) =>
        FromResult(await courses.DeleteLessonAsync(lessonId, ct));

    // ---------- Resources ----------
    [HttpPost("lessons/{lessonId:guid}/resources")]
    public async Task<IActionResult> AddResource(Guid lessonId, AddResourceRequest request, CancellationToken ct) =>
        FromResult(await courses.AddResourceAsync(lessonId, request, ct));

    [HttpDelete("resources/{resourceId:guid}")]
    public async Task<IActionResult> DeleteResource(Guid resourceId, CancellationToken ct) =>
        FromResult(await courses.DeleteResourceAsync(resourceId, ct));

    // ---------- Reviews ----------
    [HttpGet("reviews")]
    public async Task<ActionResult<PagedResult<InstructorReviewDto>>> Reviews([FromQuery] Guid? courseId, [FromQuery] PagedQuery query, CancellationToken ct) =>
        Ok(await reviews.ListForInstructorAsync(User.GetUserId(), courseId, query, ct));

    [HttpPost("reviews/{reviewId:guid}/reply")]
    public async Task<IActionResult> Reply(Guid reviewId, ReplyReviewRequest request, CancellationToken ct) =>
        FromResult(await reviews.ReplyAsync(User.GetUserId(), reviewId, request, User.IsInRole(Roles.Admin), ct));
}
