using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearning.API.Common;
using OnlineLearning.Application.Features.Growth;
using OnlineLearning.Domain.Constants;

namespace OnlineLearning.API.Controllers;

/// <summary>Danh sách khóa học yêu thích của người dùng.</summary>
[Route("api/wishlist")]
[Authorize]
public sealed class WishlistController(IWishlistService wishlist) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WishlistItemDto>>> List(CancellationToken ct) => Ok(await wishlist.ListAsync(User.GetUserId(), ct));

    /// <summary>Chỉ mã khóa học (để tô trái tim trên thẻ khóa học).</summary>
    [HttpGet("ids")]
    public async Task<ActionResult<IReadOnlyList<Guid>>> Ids(CancellationToken ct) => Ok(await wishlist.GetIdsAsync(User.GetUserId(), ct));

    [HttpPost("{courseId:guid}")]
    public async Task<IActionResult> Add(Guid courseId, CancellationToken ct) => FromResult(await wishlist.AddAsync(User.GetUserId(), courseId, ct));

    [HttpDelete("{courseId:guid}")]
    public async Task<IActionResult> Remove(Guid courseId, CancellationToken ct)
    {
        await wishlist.RemoveAsync(User.GetUserId(), courseId, ct);
        return NoContent();
    }
}

/// <summary>Gợi ý khóa học: trang chi tiết khóa học và giỏ hàng.</summary>
[Route("api")]
public sealed class RecommendationsController(IRecommendationService recommendations) : ApiControllerBase
{
    private Guid? CurrentUserId => User.Identity?.IsAuthenticated == true ? User.GetUserId() : null;

    [HttpGet("courses/{slug}/related")]
    public async Task<ActionResult<IReadOnlyList<RecommendedCourseDto>>> Related(string slug, [FromQuery] int take = 4, CancellationToken ct = default) =>
        Ok(await recommendations.GetForSlugAsync(slug, CurrentUserId, take, ct));

    [Authorize]
    [HttpGet("cart/suggestions")]
    public async Task<ActionResult<IReadOnlyList<RecommendedCourseDto>>> CartSuggestions([FromQuery] int take = 4, CancellationToken ct = default) =>
        Ok(await recommendations.GetForCartAsync(User.GetUserId(), take, ct));
}

/// <summary>Combo khóa học đang bán.</summary>
[Route("api/bundles")]
public sealed class BundlesController(IBundleService bundles) : ApiControllerBase
{
    /// <summary>Combo đang bán; truyền courseId để lấy các combo có chứa khóa học đó.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BundleSummaryDto>>> List([FromQuery] Guid? courseId, CancellationToken ct) =>
        Ok(await bundles.ListActiveAsync(courseId, ct));

    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug, CancellationToken ct) =>
        FromResult(await bundles.GetBySlugAsync(slug, User.Identity?.IsAuthenticated == true ? User.GetUserId() : null, ct));
}

[Route("api/admin/bundles")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminBundlesController(IBundleService bundles) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminBundleDto>>> List(CancellationToken ct) => Ok(await bundles.ListAllAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create(SaveBundleRequest request, CancellationToken ct) => FromResult(await bundles.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveBundleRequest request, CancellationToken ct) => FromResult(await bundles.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => FromResult(await bundles.DeleteAsync(id, ct));
}

/// <summary>Nhận quà tặng khóa học bằng mã trong email.</summary>
[Route("api/gifts")]
public sealed class GiftsController(IGiftService gifts) : ApiControllerBase
{
    [HttpGet("{code}")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimitPolicies.Payment)]
    public async Task<IActionResult> Get(string code, CancellationToken ct) =>
        FromResult(await gifts.GetAsync(code, User.Identity?.IsAuthenticated == true ? User.GetUserId() : null, ct));

    [Authorize]
    [HttpPost("{code}/redeem")]
    public async Task<IActionResult> Redeem(string code, CancellationToken ct) => FromResult(await gifts.RedeemAsync(User.GetUserId(), code, ct));
}
