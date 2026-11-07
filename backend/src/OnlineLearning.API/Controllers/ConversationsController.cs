using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineLearning.API.Common;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Messaging;

namespace OnlineLearning.API.Controllers;

/// <summary>Trao đổi 1-1 giữa người học và giảng viên. Chỉ hai người trong cuộc trao đổi xem được nội dung.</summary>
[Route("api/conversations")]
[Authorize]
public sealed class ConversationsController(IMessagingService messaging) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ConversationListItemDto>>> Mine([FromQuery] PagedQuery query, CancellationToken ct) =>
        Ok(await messaging.ListMineAsync(User.GetUserId(), query, ct));

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct) =>
        Ok(new { count = await messaging.CountUnreadAsync(User.GetUserId(), ct) });

    /// <summary>Mở (hoặc tiếp tục) trao đổi với giảng viên và gửi tin nhắn đầu tiên.</summary>
    [HttpPost]
    public async Task<IActionResult> Start(StartConversationRequest request, CancellationToken ct) =>
        FromResult(await messaging.StartAsync(User.GetUserId(), request, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] DateTime? before, CancellationToken ct) =>
        FromResult(await messaging.GetAsync(User.GetUserId(), id, before, ct));

    [HttpPost("{id:guid}/messages")]
    public async Task<IActionResult> Send(Guid id, SendMessageRequest request, CancellationToken ct) =>
        FromResult(await messaging.SendAsync(User.GetUserId(), id, request, ct));
}
