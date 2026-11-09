using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearning.API.Common;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Notifications;
using OnlineLearning.Application.Features.Orders;
using OnlineLearning.Domain.Constants;

namespace OnlineLearning.API.Controllers;

[Route("api/cart")]
[Authorize]
public sealed class CartController(ICartService cart) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CartDto>> Get(CancellationToken ct) => Ok(await cart.GetAsync(User.GetUserId(), ct));

    [HttpPost("items")]
    public async Task<IActionResult> Add(AddToCartRequest request, CancellationToken ct) =>
        FromResult(await cart.AddAsync(User.GetUserId(), request.CourseId, ct));

    [HttpDelete("items/{courseId:guid}")]
    public async Task<ActionResult<CartDto>> Remove(Guid courseId, CancellationToken ct) =>
        Ok(await cart.RemoveAsync(User.GetUserId(), courseId, ct));

    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        await cart.ClearAsync(User.GetUserId(), ct);
        return NoContent();
    }
}

[Route("api/orders")]
[Authorize]
public sealed class OrdersController(IOrderService orders, IPricingService pricing) : ApiControllerBase
{
    /// <summary>Xem trước giá + kiểm tra mã giảm giá (giá tính hoàn toàn ở Backend).</summary>
    [HttpPost("quote")]
    public async Task<IActionResult> Quote(PriceQuoteRequest request, CancellationToken ct) =>
        FromResult(await pricing.QuoteAsync(User.GetUserId(), request, ct));

    /// <summary>Tạo đơn hàng. Đơn 0đ được hoàn tất và cấp quyền học ngay.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken ct) =>
        Created(await orders.CreateAsync(User.GetUserId(), request, ct), nameof(Get), o => new { id = o.Id });

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderListItemDto>>> Mine([FromQuery] OrderQuery query, CancellationToken ct) =>
        Ok(await orders.GetMineAsync(User.GetUserId(), query, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        FromResult(await orders.GetAsync(id, User.GetUserId(), User.IsInRole(Roles.Admin), ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        FromResult(await orders.CancelAsync(id, User.GetUserId(), ct));
}

[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(INotificationService notifications) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<NotificationDto>>> Mine([FromQuery] PagedQuery query, CancellationToken ct) =>
        Ok(await notifications.GetMineAsync(User.GetUserId(), query, ct));

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct) =>
        Ok(new { count = await notifications.CountUnreadAsync(User.GetUserId(), ct) });

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct) =>
        FromResult(await notifications.MarkReadAsync(User.GetUserId(), id, ct));

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(User.GetUserId(), ct);
        return NoContent();
    }
}
