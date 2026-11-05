using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearning.API.Common;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Categories;
using OnlineLearning.Application.Features.Courses;
using OnlineLearning.Application.Features.Orders;
using OnlineLearning.Domain.Constants;

namespace OnlineLearning.API.Controllers;

[Route("api/home")]
public sealed class HomeController(ICatalogService catalog) : ApiControllerBase
{
    /// <summary>Dữ liệu trang chủ: nổi bật, mới nhất, bán chạy, danh mục, giảng viên, đánh giá, thống kê.</summary>
    [HttpGet]
    [ResponseCache(Duration = 60)]
    public async Task<ActionResult<HomeDto>> Get(CancellationToken ct) => Ok(await catalog.GetHomeAsync(ct));
}

[Route("api/categories")]
public sealed class CategoriesController(ICategoryService categories) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetTree(CancellationToken ct) =>
        Ok(await categories.GetTreeAsync(includeInactive: false, ct));

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct) =>
        FromResult(await categories.GetBySlugAsync(slug, ct));
}

[Route("api/courses")]
public sealed class CoursesController(ICatalogService catalog, IReviewService reviews) : ApiControllerBase
{
    /// <summary>Tìm kiếm, lọc, sắp xếp, phân trang khóa học đã xuất bản.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<CourseSummaryDto>>> Search([FromQuery] CourseSearchQuery query, CancellationToken ct) =>
        Ok(await catalog.SearchAsync(query, ct));

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetDetail(string slug, CancellationToken ct)
    {
        Guid? userId = User.Identity?.IsAuthenticated == true ? User.GetUserId() : null;
        return FromResult(await catalog.GetDetailAsync(slug, userId, User.IsInRole(Roles.Admin), ct));
    }

    [HttpGet("{courseId:guid}/reviews")]
    public async Task<IActionResult> GetReviews(Guid courseId, [FromQuery] PagedQuery query, CancellationToken ct) =>
        FromResult(await catalog.GetReviewsAsync(courseId, query, ct));

    [Authorize]
    [HttpGet("{courseId:guid}/reviews/me")]
    public async Task<IActionResult> GetMyReview(Guid courseId, CancellationToken ct) =>
        FromResult(await reviews.GetMineAsync(User.GetUserId(), courseId, ct));

    [Authorize]
    [HttpPost("{courseId:guid}/reviews")]
    public async Task<IActionResult> CreateReview(Guid courseId, SaveReviewRequest request, CancellationToken ct) =>
        FromResult(await reviews.CreateAsync(User.GetUserId(), courseId, request, ct));

    [Authorize]
    [HttpPut("{courseId:guid}/reviews/me")]
    public async Task<IActionResult> UpdateReview(Guid courseId, SaveReviewRequest request, CancellationToken ct) =>
        FromResult(await reviews.UpdateAsync(User.GetUserId(), courseId, request, ct));

    [Authorize]
    [HttpDelete("{courseId:guid}/reviews/me")]
    public async Task<IActionResult> DeleteReview(Guid courseId, CancellationToken ct) =>
        FromResult(await reviews.DeleteAsync(User.GetUserId(), courseId, ct));
}

[Route("api/instructors")]
public sealed class InstructorsController(ICatalogService catalog) : ApiControllerBase
{
    [HttpGet("featured")]
    public async Task<ActionResult<IReadOnlyList<InstructorProfileDto>>> Featured([FromQuery] int take = 8, CancellationToken ct = default) =>
        Ok(await catalog.GetFeaturedInstructorsAsync(Math.Clamp(take, 1, 24), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        FromResult(await catalog.GetInstructorAsync(id, ct));
}
