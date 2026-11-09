using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearning.API.Common;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Admin;
using OnlineLearning.Application.Features.Categories;
using OnlineLearning.Application.Features.Courses;
using OnlineLearning.Application.Features.Orders;
using OnlineLearning.Domain.Constants;

namespace OnlineLearning.API.Controllers;

/// <summary>Quản trị hệ thống — chỉ Admin.</summary>
[Route("api/admin")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminController(
    IAdminService admin,
    ICourseModerationService moderation,
    ICategoryService categories,
    ICouponService coupons,
    IOrderService orders,
    IReviewService reviews) : ApiControllerBase
{
    // ---------- Tổng quan ----------
    [HttpGet("overview")]
    public async Task<ActionResult<AdminOverviewDto>> Overview(CancellationToken ct) => Ok(await admin.GetOverviewAsync(ct));

    // ---------- Người dùng ----------
    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> Users([FromQuery] AdminUserQuery query, CancellationToken ct) =>
        Ok(await admin.ListUsersAsync(query, ct));

    [HttpPut("users/{id:guid}/roles")]
    public async Task<IActionResult> SetRoles(Guid id, SetRolesRequest request, CancellationToken ct) =>
        FromResult(await admin.SetRolesAsync(User.GetUserId(), id, request, ct));

    [HttpPut("users/{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, SetActiveRequest request, CancellationToken ct) =>
        FromResult(await admin.SetActiveAsync(User.GetUserId(), id, request.IsActive, ct));

    [HttpPut("users/{id:guid}/instructor-verified")]
    public async Task<IActionResult> SetInstructorVerified(Guid id, SetInstructorVerifiedRequest request, CancellationToken ct) =>
        FromResult(await admin.SetInstructorVerifiedAsync(id, request.IsVerified, ct));

    // ---------- Khóa học ----------
    [HttpGet("courses")]
    public async Task<ActionResult<PagedResult<AdminCourseListItemDto>>> Courses([FromQuery] AdminCourseQuery query, CancellationToken ct) =>
        Ok(await moderation.ListAsync(query, ct));

    [HttpPost("courses/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) => FromResult(await moderation.ApproveAsync(id, ct));

    [HttpPost("courses/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, RejectCourseRequest request, CancellationToken ct) =>
        FromResult(await moderation.RejectAsync(id, request, ct));

    [HttpPost("courses/{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct) => FromResult(await moderation.UnpublishAsync(id, ct));

    [HttpPut("courses/{id:guid}/featured")]
    public async Task<IActionResult> SetFeatured(Guid id, SetFeaturedRequest request, CancellationToken ct) =>
        FromResult(await moderation.SetFeaturedAsync(id, request.IsFeatured, ct));

    // ---------- Danh mục ----------
    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> Categories(CancellationToken ct) =>
        Ok(await categories.GetTreeAsync(includeInactive: true, ct));

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory(SaveCategoryRequest request, CancellationToken ct) =>
        FromResult(await categories.CreateAsync(request, ct));

    [HttpPut("categories/{id:guid}")]
    public async Task<IActionResult> UpdateCategory(Guid id, SaveCategoryRequest request, CancellationToken ct) =>
        FromResult(await categories.UpdateAsync(id, request, ct));

    [HttpDelete("categories/{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct) => FromResult(await categories.DeleteAsync(id, ct));

    // ---------- Mã giảm giá ----------
    [HttpGet("coupons")]
    public async Task<ActionResult<PagedResult<CouponDto>>> Coupons([FromQuery] CouponQuery query, CancellationToken ct) =>
        Ok(await coupons.ListAsync(query, ct));

    [HttpGet("coupons/{id:guid}")]
    public async Task<IActionResult> GetCoupon(Guid id, CancellationToken ct) => FromResult(await coupons.GetAsync(id, ct));

    [HttpPost("coupons")]
    public async Task<IActionResult> CreateCoupon(SaveCouponRequest request, CancellationToken ct) =>
        Created(await coupons.CreateAsync(request, ct), nameof(GetCoupon), c => new { id = c.Id });

    [HttpPut("coupons/{id:guid}")]
    public async Task<IActionResult> UpdateCoupon(Guid id, SaveCouponRequest request, CancellationToken ct) =>
        FromResult(await coupons.UpdateAsync(id, request, ct));

    [HttpDelete("coupons/{id:guid}")]
    public async Task<IActionResult> DeleteCoupon(Guid id, CancellationToken ct) => FromResult(await coupons.DeleteAsync(id, ct));

    // ---------- Đơn hàng ----------
    [HttpGet("orders")]
    public async Task<ActionResult<PagedResult<AdminOrderListItemDto>>> Orders([FromQuery] OrderQuery query, CancellationToken ct) =>
        Ok(await orders.ListAllAsync(query, ct));

    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult> GetOrder(Guid id, CancellationToken ct) =>
        FromResult(await orders.GetAsync(id, User.GetUserId(), isAdmin: true, ct));

    // ---------- Đánh giá ----------
    [HttpPut("reviews/{id:guid}/hidden")]
    public async Task<IActionResult> SetReviewHidden(Guid id, [FromBody] SetHiddenRequest request, CancellationToken ct) =>
        FromResult(await reviews.SetHiddenAsync(id, request.IsHidden, ct));
}

public sealed record SetHiddenRequest(bool IsHidden);
