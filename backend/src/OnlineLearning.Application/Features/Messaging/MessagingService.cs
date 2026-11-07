using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Notifications;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Messaging;

// ---------------------------------------------------------------- DTOs

public sealed record ParticipantDto(Guid Id, string FullName, string? AvatarUrl, string? Headline, bool IsVerified);
public sealed record ConversationCourseDto(Guid Id, string Title, string Slug);

public sealed record ConversationListItemDto(
    Guid Id, ParticipantDto Other, bool IAmInstructor, ConversationCourseDto? Course,
    string? LastMessagePreview, bool LastMessageIsMine, DateTime LastMessageAt, bool HasUnread);

public sealed record MessageDto(Guid Id, Guid SenderId, string Body, DateTime CreatedAt);

public sealed record ConversationDetailDto(
    Guid Id, ParticipantDto Other, bool IAmInstructor, ConversationCourseDto? Course,
    IReadOnlyList<MessageDto> Messages, bool HasMore, DateTime? OtherLastReadAt);

public sealed record StartConversationRequest(Guid InstructorId, Guid? CourseId, string Message);
public sealed record SendMessageRequest(string Body);

public sealed class StartConversationRequestValidator : AbstractValidator<StartConversationRequest>
{
    public StartConversationRequestValidator()
    {
        RuleFor(x => x.InstructorId).NotEmpty();
        RuleFor(x => x.Message).Must(m => !string.IsNullOrWhiteSpace(m)).WithMessage("Nhập nội dung tin nhắn.")
            .MaximumLength(MessagingService.MaxBodyLength).WithMessage($"Tin nhắn tối đa {MessagingService.MaxBodyLength} ký tự.");
    }
}

public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Body).Must(m => !string.IsNullOrWhiteSpace(m)).WithMessage("Nhập nội dung tin nhắn.")
            .MaximumLength(MessagingService.MaxBodyLength).WithMessage($"Tin nhắn tối đa {MessagingService.MaxBodyLength} ký tự.");
    }
}

// ---------------------------------------------------------------- Service

public interface IMessagingService
{
    Task<PagedResult<ConversationListItemDto>> ListMineAsync(Guid userId, PagedQuery query, CancellationToken ct = default);
    /// <summary>Người học mở (hoặc tiếp tục) trao đổi với một giảng viên và gửi tin nhắn đầu tiên.</summary>
    Task<Result<ConversationDetailDto>> StartAsync(Guid userId, StartConversationRequest request, CancellationToken ct = default);
    /// <summary>Tin nhắn mới nhất (hoặc cũ hơn mốc `before`); đánh dấu đã đọc cho người xem.</summary>
    Task<Result<ConversationDetailDto>> GetAsync(Guid userId, Guid conversationId, DateTime? before, CancellationToken ct = default);
    Task<Result<MessageDto>> SendAsync(Guid userId, Guid conversationId, SendMessageRequest request, CancellationToken ct = default);
    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);
}

public sealed class MessagingService(IAppDbContext db, IDateTimeProvider clock, INotificationService notifications) : IMessagingService
{
    public const int MaxBodyLength = 2000;
    public const int PageSize = 50;
    /// <summary>Giới hạn chống spam: số tin nhắn tối đa một người gửi trong 1 giờ.</summary>
    public const int MaxMessagesPerHour = 40;

    public async Task<PagedResult<ConversationListItemDto>> ListMineAsync(Guid userId, PagedQuery query, CancellationToken ct = default)
    {
        var page = await db.Conversations.AsNoTracking()
            .Where(c => c.StudentId == userId || c.InstructorId == userId)
            .OrderByDescending(c => c.LastMessageAt)
            .Select(c => new
            {
                c.Id, c.StudentId, c.InstructorId, c.LastMessageAt, c.StudentLastReadAt, c.InstructorLastReadAt,
                Course = c.Course == null ? null : new ConversationCourseDto(c.Course.Id, c.Course.Title, c.Course.Slug),
                Student = new ParticipantDto(c.Student.Id, c.Student.FullName, c.Student.AvatarUrl, c.Student.Headline, false),
                Instructor = new ParticipantDto(c.Instructor.Id, c.Instructor.FullName, c.Instructor.AvatarUrl, c.Instructor.Headline, c.Instructor.IsVerifiedInstructor),
                Last = c.Messages.OrderByDescending(m => m.CreatedAt).Select(m => new { m.Body, m.SenderId, m.CreatedAt }).FirstOrDefault(),
            })
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

        var items = page.Items.Select(c =>
        {
            var iAmInstructor = c.InstructorId == userId;
            var myRead = iAmInstructor ? c.InstructorLastReadAt : c.StudentLastReadAt;
            var hasUnread = c.Last is not null && c.Last.SenderId != userId && (myRead is null || c.Last.CreatedAt > myRead);
            var preview = c.Last?.Body is { } b ? (b.Length > 120 ? b[..120] + "…" : b) : null;
            return new ConversationListItemDto(c.Id, iAmInstructor ? c.Student : c.Instructor, iAmInstructor, c.Course,
                preview, c.Last?.SenderId == userId, c.LastMessageAt, hasUnread);
        }).ToList();
        return new PagedResult<ConversationListItemDto>(items, page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<Result<ConversationDetailDto>> StartAsync(Guid userId, StartConversationRequest r, CancellationToken ct = default)
    {
        if (r.InstructorId == userId) return Error.Validation("Bạn không thể tự nhắn tin cho chính mình.");

        var instructorRole = Roles.Instructor.ToUpperInvariant();
        var isInstructor = await (from ur in db.Set<IdentityUserRole<Guid>>()
                                  join role in db.Set<AppRole>() on ur.RoleId equals role.Id
                                  where ur.UserId == r.InstructorId && role.NormalizedName == instructorRole
                                  select ur).AnyAsync(ct)
                           && await db.Users.AnyAsync(u => u.Id == r.InstructorId && u.IsActive, ct);
        if (!isInstructor) return Error.NotFound("Không tìm thấy giảng viên.");

        if (r.CourseId is { } courseId &&
            !await db.Courses.AnyAsync(c => c.Id == courseId && c.InstructorId == r.InstructorId && !c.IsDeleted, ct))
            return Error.Validation("Khóa học không thuộc giảng viên này.");

        if (await IsRateLimitedAsync(userId, ct)) return TooMany();

        var now = clock.UtcNow;
        var conversation = await db.Conversations
            .FirstOrDefaultAsync(c => c.StudentId == userId && c.InstructorId == r.InstructorId, ct);
        if (conversation is null)
        {
            conversation = new Conversation { StudentId = userId, InstructorId = r.InstructorId, CreatedAt = now, LastMessageAt = now };
            db.Conversations.Add(conversation);
        }
        if (r.CourseId is not null) conversation.CourseId = r.CourseId;

        await AppendAsync(conversation, userId, r.Message, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(userId, conversation.Id, null, ct);
    }

    public async Task<Result<ConversationDetailDto>> GetAsync(Guid userId, Guid conversationId, DateTime? before, CancellationToken ct = default)
    {
        var c = await db.Conversations
            .Include(x => x.Student).Include(x => x.Instructor).Include(x => x.Course)
            .FirstOrDefaultAsync(x => x.Id == conversationId, ct);
        if (c is null || (c.StudentId != userId && c.InstructorId != userId))
            return Error.NotFound("Không tìm thấy cuộc trao đổi.");

        var query = db.Messages.AsNoTracking().Where(m => m.ConversationId == conversationId);
        if (before is { } b) query = query.Where(m => m.CreatedAt < b);
        var latest = await query.OrderByDescending(m => m.CreatedAt).Take(PageSize + 1)
            .Select(m => new MessageDto(m.Id, m.SenderId, m.Body, m.CreatedAt)).ToListAsync(ct);
        var hasMore = latest.Count > PageSize;
        var messages = latest.Take(PageSize).Reverse().ToList();

        var iAmInstructor = c.InstructorId == userId;
        // Xem trang mới nhất = đã đọc hết
        if (before is null)
        {
            if (iAmInstructor) c.InstructorLastReadAt = clock.UtcNow; else c.StudentLastReadAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var other = iAmInstructor
            ? new ParticipantDto(c.Student.Id, c.Student.FullName, c.Student.AvatarUrl, c.Student.Headline, false)
            : new ParticipantDto(c.Instructor.Id, c.Instructor.FullName, c.Instructor.AvatarUrl, c.Instructor.Headline, c.Instructor.IsVerifiedInstructor);
        var course = c.Course is null ? null : new ConversationCourseDto(c.Course.Id, c.Course.Title, c.Course.Slug);
        return new ConversationDetailDto(c.Id, other, iAmInstructor, course, messages, hasMore,
            iAmInstructor ? c.StudentLastReadAt : c.InstructorLastReadAt);
    }

    public async Task<Result<MessageDto>> SendAsync(Guid userId, Guid conversationId, SendMessageRequest r, CancellationToken ct = default)
    {
        var c = await db.Conversations.FirstOrDefaultAsync(x => x.Id == conversationId, ct);
        if (c is null || (c.StudentId != userId && c.InstructorId != userId))
            return Error.NotFound("Không tìm thấy cuộc trao đổi.");
        if (await IsRateLimitedAsync(userId, ct)) return TooMany();

        var message = await AppendAsync(c, userId, r.Body, ct);
        await db.SaveChangesAsync(ct);
        return new MessageDto(message.Id, message.SenderId, message.Body, message.CreatedAt);
    }

    public async Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default) =>
        await db.Conversations.AsNoTracking()
            .Where(c => (c.StudentId == userId &&
                         c.Messages.Any(m => m.SenderId != userId && (c.StudentLastReadAt == null || m.CreatedAt > c.StudentLastReadAt)))
                     || (c.InstructorId == userId &&
                         c.Messages.Any(m => m.SenderId != userId && (c.InstructorLastReadAt == null || m.CreatedAt > c.InstructorLastReadAt))))
            .CountAsync(ct);

    // ---------------------------------------------------------------- helpers

    private async Task<Message> AppendAsync(Conversation c, Guid senderId, string body, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var senderIsInstructor = c.InstructorId == senderId;
        var recipientId = senderIsInstructor ? c.StudentId : c.InstructorId;
        var recipientRead = senderIsInstructor ? c.StudentLastReadAt : c.InstructorLastReadAt;

        // Chỉ báo khi người nhận đang "đã đọc hết" — tránh một chuỗi tin nhắn sinh ra nhiều thông báo.
        // (EF sinh Guid ngay khi Add nên cuộc trao đổi mới cũng đã có Id ở đây.)
        var recipientHasUnreadAlready = await db.Messages.AnyAsync(m =>
            m.ConversationId == c.Id && m.SenderId == senderId && (recipientRead == null || m.CreatedAt > recipientRead), ct);

        var message = new Message { Conversation = c, SenderId = senderId, Body = body.Trim(), CreatedAt = now };
        db.Messages.Add(message);
        c.LastMessageAt = now;
        if (senderIsInstructor) c.InstructorLastReadAt = now; else c.StudentLastReadAt = now;

        if (!recipientHasUnreadAlready)
        {
            var senderName = await db.Users.Where(u => u.Id == senderId).Select(u => u.FullName).FirstAsync(ct);
            var preview = message.Body.Length > 120 ? message.Body[..120] + "…" : message.Body;
            var link = senderIsInstructor ? $"/hoc-vien/tin-nhan/{c.Id}" : $"/giang-day/tin-nhan/{c.Id}";
            notifications.Add(recipientId, $"Tin nhắn mới từ {senderName}", preview, NotificationType.Message, link);
        }
        return message;
    }

    private async Task<bool> IsRateLimitedAsync(Guid userId, CancellationToken ct)
    {
        var since = clock.UtcNow.AddHours(-1);
        return await db.Messages.CountAsync(m => m.SenderId == userId && m.CreatedAt >= since, ct) >= MaxMessagesPerHour;
    }

    private static Error TooMany() =>
        new("too_many_messages", "Bạn đã gửi quá nhiều tin nhắn trong một giờ. Vui lòng thử lại sau.", ErrorType.Conflict);
}
