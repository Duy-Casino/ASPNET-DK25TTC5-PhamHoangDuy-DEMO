using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnlineLearning.Application.Common.Helpers;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Common.Options;
using OnlineLearning.Application.Features.Notifications;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Admin;

// =====================================================================
// DTO
// =====================================================================
public sealed record AdminReviewDto(Guid Id, Guid CourseId, string CourseTitle, string CourseSlug, string UserName, string UserEmail,
    byte Rating, string? Comment, bool IsHidden, string? InstructorReply, DateTime CreatedAt);
public sealed class AdminReviewQuery : PagedQuery
{
    public int? Rating { get; set; }
    public bool? IsHidden { get; set; }
    public string? Keyword { get; set; }
}

public sealed record InstructorApplicationDto(Guid Id, Guid UserId, string UserName, string UserEmail, string Headline, string Bio, string Expertise,
    string ProposedTopic, string? PortfolioUrl, int? YearsOfExperience, InstructorApplicationStatus Status, string? AdminNote,
    DateTime CreatedAt, DateTime? ProcessedAt);
public sealed record SubmitInstructorApplicationRequest(string Headline, string Bio, string Expertise, string ProposedTopic, string? PortfolioUrl, int? YearsOfExperience);
public sealed record ProcessApplicationRequest(string? AdminNote);
public sealed record MyApplicationStatusDto(bool IsInstructor, InstructorApplicationDto? Latest);

public sealed record AuditLogDto(Guid Id, Guid ActorId, string ActorName, string Action, string Summary, string HttpMethod, string Path,
    string? Payload, int StatusCode, bool Succeeded, string? IpAddress, DateTime CreatedAt);
public sealed class AuditLogQuery : PagedQuery
{
    public string? Keyword { get; set; }
    public bool? Succeeded { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed record SendAnnouncementRequest(AnnouncementAudience Audience, Guid? CourseId, string Title, string Message, string? Link, bool SendEmail);
public sealed record AnnouncementDto(Guid Id, string Title, string Message, string? Link, AnnouncementAudience Audience, Guid? CourseId, string? CourseTitle,
    bool SendEmail, int RecipientCount, int EmailCount, string CreatedByName, DateTime CreatedAt);
public sealed record AudiencePreviewDto(int Recipients, int EmailRecipients);

public sealed class SubmitInstructorApplicationValidator : AbstractValidator<SubmitInstructorApplicationRequest>
{
    public SubmitInstructorApplicationValidator()
    {
        RuleFor(x => x.Headline).NotEmpty().WithMessage("Nhập chức danh / giới thiệu ngắn.").MaximumLength(200);
        RuleFor(x => x.Bio).Must(b => b is not null && b.Trim().Length >= 50).WithMessage("Giới thiệu bản thân ít nhất 50 ký tự.").MaximumLength(2000);
        RuleFor(x => x.Expertise).NotEmpty().WithMessage("Nhập lĩnh vực chuyên môn.").MaximumLength(500);
        RuleFor(x => x.ProposedTopic).Must(t => t is not null && t.Trim().Length >= 10).WithMessage("Mô tả khóa học dự định dạy ít nhất 10 ký tự.").MaximumLength(500);
        RuleFor(x => x.PortfolioUrl).Must(u => string.IsNullOrWhiteSpace(u) || (Uri.TryCreate(u, UriKind.Absolute, out var x) && (x.Scheme == "https" || x.Scheme == "http")))
            .WithMessage("Liên kết hồ sơ phải là địa chỉ web hợp lệ.").MaximumLength(500);
        RuleFor(x => x.YearsOfExperience).InclusiveBetween(0, 60).When(x => x.YearsOfExperience.HasValue);
    }
}

public sealed class SendAnnouncementValidator : AbstractValidator<SendAnnouncementRequest>
{
    public SendAnnouncementValidator()
    {
        RuleFor(x => x.Audience).IsInEnum();
        RuleFor(x => x.CourseId).NotEmpty().When(x => x.Audience == AnnouncementAudience.CourseStudents).WithMessage("Chọn khóa học.");
        RuleFor(x => x.Title).NotEmpty().WithMessage("Nhập tiêu đề.").MaximumLength(150);
        RuleFor(x => x.Message).Must(m => m is not null && m.Trim().Length >= 10).WithMessage("Nội dung ít nhất 10 ký tự.").MaximumLength(1000);
        RuleFor(x => x.Link).Must(l => string.IsNullOrWhiteSpace(l) || (l.StartsWith('/') && !l.StartsWith("//")))
            .WithMessage("Liên kết là đường dẫn trong website, bắt đầu bằng / (ví dụ /combo).").MaximumLength(300);
    }
}

// =====================================================================
// Công cụ quản trị: đánh giá, hồ sơ giảng viên, nhật ký, thông báo, xuất dữ liệu
// =====================================================================
public interface IAdminToolsService
{
    Task<PagedResult<AdminReviewDto>> ListReviewsAsync(AdminReviewQuery query, CancellationToken ct = default);

    Task<MyApplicationStatusDto> GetMyApplicationAsync(Guid userId, CancellationToken ct = default);
    Task<Result<InstructorApplicationDto>> SubmitApplicationAsync(Guid userId, SubmitInstructorApplicationRequest request, CancellationToken ct = default);
    Task<PagedResult<InstructorApplicationDto>> ListApplicationsAsync(InstructorApplicationStatus? status, PagedQuery query, CancellationToken ct = default);
    Task<Result> ApproveApplicationAsync(Guid adminId, Guid id, string? note, CancellationToken ct = default);
    Task<Result> RejectApplicationAsync(Guid adminId, Guid id, string? note, CancellationToken ct = default);

    Task<PagedResult<AuditLogDto>> ListAuditAsync(AuditLogQuery query, CancellationToken ct = default);
    Task<byte[]> ExportAuditCsvAsync(AuditLogQuery query, CancellationToken ct = default);

    Task<AudiencePreviewDto> PreviewAudienceAsync(AnnouncementAudience audience, Guid? courseId, CancellationToken ct = default);
    Task<Result<AnnouncementDto>> SendAnnouncementAsync(Guid adminId, SendAnnouncementRequest request, CancellationToken ct = default);
    Task<PagedResult<AnnouncementDto>> ListAnnouncementsAsync(PagedQuery query, CancellationToken ct = default);

    Task<byte[]> ExportUsersCsvAsync(AdminUserQuery query, CancellationToken ct = default);
}

public sealed class AdminToolsService(
    IAppDbContext db, UserManager<AppUser> userManager, INotificationService notifications, IEmailSender email,
    IDateTimeProvider clock, IOptions<AppOptions> app, ILogger<AdminToolsService> logger) : IAdminToolsService
{
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);

    // ---------------------------------------------------------------- đánh giá
    public Task<PagedResult<AdminReviewDto>> ListReviewsAsync(AdminReviewQuery q, CancellationToken ct = default)
    {
        var query = db.Reviews.AsNoTracking().AsQueryable();
        if (q.Rating is { } r) query = query.Where(x => x.Rating == r);
        if (q.IsHidden is { } h) query = query.Where(x => x.IsHidden == h);
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(x => (x.Comment != null && x.Comment.Contains(kw)) || x.User.FullName.Contains(kw) || x.Course.Title.Contains(kw));
        }
        return query.OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminReviewDto(x.Id, x.CourseId, x.Course.Title, x.Course.Slug, x.User.FullName, x.User.Email ?? "",
                x.Rating, x.Comment, x.IsHidden, x.InstructorReply, x.CreatedAt))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);
    }

    // ---------------------------------------------------------------- đăng ký làm giảng viên
    private IQueryable<InstructorApplicationDto> ProjectApps(IQueryable<InstructorApplication> q) =>
        q.Select(a => new InstructorApplicationDto(a.Id, a.UserId, a.User.FullName, a.User.Email ?? "", a.Headline, a.Bio, a.Expertise,
            a.ProposedTopic, a.PortfolioUrl, a.YearsOfExperience, a.Status, a.AdminNote, a.CreatedAt, a.ProcessedAt));

    private async Task<bool> IsInstructorAsync(AppUser user) => await userManager.IsInRoleAsync(user, Roles.Instructor);

    public async Task<MyApplicationStatusDto> GetMyApplicationAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        var latest = await ProjectApps(db.InstructorApplications.AsNoTracking().Where(a => a.UserId == userId).OrderByDescending(a => a.CreatedAt)).FirstOrDefaultAsync(ct);
        return new MyApplicationStatusDto(user is not null && await IsInstructorAsync(user), latest);
    }

    public async Task<Result<InstructorApplicationDto>> SubmitApplicationAsync(Guid userId, SubmitInstructorApplicationRequest r, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Error.NotFound("Không tìm thấy tài khoản.");
        if (await IsInstructorAsync(user)) return Error.Conflict("Bạn đã là giảng viên.");
        if (await db.InstructorApplications.AnyAsync(a => a.UserId == userId && a.Status == InstructorApplicationStatus.Pending, ct))
            return Error.Conflict("Hồ sơ của bạn đang chờ duyệt.");

        var entity = new InstructorApplication
        {
            UserId = userId, Headline = r.Headline.Trim(), Bio = r.Bio.Trim(), Expertise = r.Expertise.Trim(), ProposedTopic = r.ProposedTopic.Trim(),
            PortfolioUrl = string.IsNullOrWhiteSpace(r.PortfolioUrl) ? null : r.PortfolioUrl.Trim(), YearsOfExperience = r.YearsOfExperience,
        };
        db.InstructorApplications.Add(entity);
        await notifications.AddToRoleAsync(Roles.Admin, "Hồ sơ đăng ký giảng viên mới", $"{user.FullName} đăng ký làm giảng viên: {entity.ProposedTopic}",
            NotificationType.System, "/quan-tri/dang-ky-giang-vien", ct);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return Error.Conflict("Hồ sơ của bạn đang chờ duyệt."); }
        return await ProjectApps(db.InstructorApplications.AsNoTracking().Where(a => a.Id == entity.Id)).FirstAsync(ct);
    }

    public Task<PagedResult<InstructorApplicationDto>> ListApplicationsAsync(InstructorApplicationStatus? status, PagedQuery q, CancellationToken ct = default) =>
        ProjectApps(db.InstructorApplications.AsNoTracking().Where(a => status == null || a.Status == status)
                .OrderBy(a => a.Status == InstructorApplicationStatus.Pending ? 0 : 1).ThenByDescending(a => a.CreatedAt))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);

    public async Task<Result> ApproveApplicationAsync(Guid adminId, Guid id, string? note, CancellationToken ct = default)
    {
        var a = await db.InstructorApplications.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return Result.Failure(Error.NotFound("Không tìm thấy hồ sơ."));
        if (a.Status != InstructorApplicationStatus.Pending) return Result.Failure(Error.Conflict("Hồ sơ đã được xử lý."));
        var user = await userManager.FindByIdAsync(a.UserId.ToString());
        if (user is null) return Result.Failure(Error.NotFound("Không tìm thấy tài khoản."));

        if (!await IsInstructorAsync(user)) await userManager.AddToRoleAsync(user, Roles.Instructor);
        // Đưa thông tin hồ sơ vào trang giảng viên nếu người dùng chưa tự điền
        user.Headline ??= a.Headline;
        user.Bio ??= a.Bio;
        user.Expertise ??= string.Join(';', a.Expertise.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        user.YearsOfExperience ??= a.YearsOfExperience;
        await userManager.UpdateAsync(user);
        await userManager.UpdateSecurityStampAsync(user);

        a.Status = InstructorApplicationStatus.Approved;
        a.AdminNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        a.ProcessedAt = clock.UtcNow;
        a.ProcessedById = adminId;
        notifications.Add(a.UserId, "Bạn đã trở thành giảng viên",
            "Hồ sơ đăng ký giảng viên đã được duyệt. Đăng nhập lại rồi vào khu vực Giảng dạy để tạo khóa học đầu tiên.", NotificationType.System, "/giang-day/khoa-hoc");
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RejectApplicationAsync(Guid adminId, Guid id, string? note, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(note) || note.Trim().Length < 5) return Result.Failure(Error.Validation("Bắt buộc ghi lý do từ chối (ít nhất 5 ký tự)."));
        var a = await db.InstructorApplications.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return Result.Failure(Error.NotFound("Không tìm thấy hồ sơ."));
        if (a.Status != InstructorApplicationStatus.Pending) return Result.Failure(Error.Conflict("Hồ sơ đã được xử lý."));
        a.Status = InstructorApplicationStatus.Rejected;
        a.AdminNote = note.Trim();
        a.ProcessedAt = clock.UtcNow;
        a.ProcessedById = adminId;
        notifications.Add(a.UserId, "Hồ sơ giảng viên chưa được duyệt", $"Lý do: {a.AdminNote}. Bạn có thể bổ sung và gửi lại.", NotificationType.System, "/dang-ky-giang-day");
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ---------------------------------------------------------------- nhật ký Admin
    private IQueryable<AdminAuditLog> FilterAudit(AuditLogQuery q)
    {
        var query = db.AdminAuditLogs.AsNoTracking().AsQueryable();
        if (q.Succeeded is { } s) query = query.Where(x => x.Succeeded == s);
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(x => x.ActorName.Contains(kw) || x.Summary.Contains(kw) || x.Path.Contains(kw) || (x.Payload != null && x.Payload.Contains(kw)));
        }
        if (q.From is { } f) { var t = f.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7); query = query.Where(x => x.CreatedAt >= t); }
        if (q.To is { } to) { var t = to.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1).AddHours(-7); query = query.Where(x => x.CreatedAt < t); }
        return query.OrderByDescending(x => x.CreatedAt);
    }

    private static AuditLogDto ToDto(AdminAuditLog x) => new(x.Id, x.ActorId, x.ActorName, x.Action, x.Summary, x.HttpMethod, x.Path, x.Payload,
        x.StatusCode, x.Succeeded, x.IpAddress, x.CreatedAt);

    public async Task<PagedResult<AuditLogDto>> ListAuditAsync(AuditLogQuery q, CancellationToken ct = default)
    {
        var page = await FilterAudit(q).ToPagedResultAsync(q.Page, q.PageSize, ct);
        return new PagedResult<AuditLogDto>(page.Items.Select(ToDto).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<byte[]> ExportAuditCsvAsync(AuditLogQuery q, CancellationToken ct = default)
    {
        var rows = await FilterAudit(q).Take(50_000).ToListAsync(ct);
        var sb = new StringBuilder("Thời gian (UTC+7),Người thực hiện,Thao tác,Kết quả,Mã HTTP,Phương thức,Đường dẫn,Dữ liệu,IP\n");
        foreach (var x in rows)
            sb.Append(CsvExport.Time(x.CreatedAt)).Append(',').Append(CsvExport.Field(x.ActorName)).Append(',').Append(CsvExport.Field(x.Summary)).Append(',')
              .Append(x.Succeeded ? "Thành công" : "Thất bại").Append(',').Append(x.StatusCode).Append(',').Append(x.HttpMethod).Append(',')
              .Append(CsvExport.Field(x.Path)).Append(',').Append(CsvExport.Field(x.Payload)).Append(',').Append(CsvExport.Field(x.IpAddress)).AppendLine();
        return CsvExport.ToBytes(sb);
    }

    // ---------------------------------------------------------------- thông báo hàng loạt
    private IQueryable<AppUser> Audience(AnnouncementAudience audience, Guid? courseId)
    {
        var users = db.Users.Where(u => u.IsActive);
        IQueryable<Guid> InRole(string role) =>
            from ur in db.Set<IdentityUserRole<Guid>>() join r in db.Set<AppRole>() on ur.RoleId equals r.Id where r.Name == role select ur.UserId;
        return audience switch
        {
            AnnouncementAudience.Students => FilterIn(users, InRole(Roles.Student)),
            AnnouncementAudience.Instructors => FilterIn(users, InRole(Roles.Instructor)),
            AnnouncementAudience.CourseStudents => users.Where(u => db.Enrollments.Any(e => e.UserId == u.Id && e.CourseId == courseId && e.Status != EnrollmentStatus.Revoked)),
            _ => users,
        };
    }

    private static IQueryable<AppUser> FilterIn(IQueryable<AppUser> users, IQueryable<Guid> ids) => users.Where(u => ids.Contains(u.Id));

    public async Task<AudiencePreviewDto> PreviewAudienceAsync(AnnouncementAudience audience, Guid? courseId, CancellationToken ct = default)
    {
        var q = Audience(audience, courseId);
        return new AudiencePreviewDto(await q.CountAsync(ct), await q.CountAsync(u => u.Email != null && !u.ReminderEmailsDisabled, ct));
    }

    public async Task<Result<AnnouncementDto>> SendAnnouncementAsync(Guid adminId, SendAnnouncementRequest r, CancellationToken ct = default)
    {
        string? courseTitle = null;
        if (r.Audience == AnnouncementAudience.CourseStudents)
        {
            courseTitle = await db.Courses.Where(c => c.Id == r.CourseId).Select(c => c.Title).FirstOrDefaultAsync(ct);
            if (courseTitle is null) return Error.NotFound("Không tìm thấy khóa học.");
        }
        var recipients = await Audience(r.Audience, r.CourseId)
            .Select(u => new { u.Id, u.Email, u.FullName, u.ReminderEmailsDisabled }).Take(20_000).ToListAsync(ct);
        if (recipients.Count == 0) return Error.Validation("Không có người nhận phù hợp.");

        var title = r.Title.Trim();
        var message = r.Message.Trim();
        var link = string.IsNullOrWhiteSpace(r.Link) ? null : r.Link.Trim();
        foreach (var u in recipients) notifications.Add(u.Id, title, message, NotificationType.System, link);

        var entity = new Announcement
        {
            Title = title, Message = message, Link = link, Audience = r.Audience, CourseId = r.CourseId, SendEmail = r.SendEmail,
            RecipientCount = recipients.Count, CreatedById = adminId,
        };
        db.Announcements.Add(entity);
        await db.SaveChangesAsync(ct);

        if (r.SendEmail)
        {
            var client = app.Value.ClientUrl.TrimEnd('/');
            var body = $"<p>{Html.Encode(message).Replace("\n", "<br>")}</p>" + (link is null ? "" : $"<p><a href=\"{client}{link}\">Xem chi tiết</a></p>") +
                       $"<p style=\"color:#64748b;font-size:12px\">Tắt email thông báo tại <a href=\"{client}/tai-khoan\">Hồ sơ cá nhân</a>.</p>";
            var sent = 0;
            foreach (var u in recipients.Where(x => x.Email != null && !x.ReminderEmailsDisabled).Take(5_000))
            {
                try { await email.SendAsync(u.Email!, title, $"<p>Chào {Html.Encode(u.FullName)},</p>" + body, ct); sent++; }
                catch (Exception ex) { logger.LogWarning(ex, "Không gửi được email thông báo tới {UserId}", u.Id); }
            }
            entity.EmailCount = sent;
            await db.SaveChangesAsync(ct);
        }
        var name = await db.Users.Where(u => u.Id == adminId).Select(u => u.FullName).FirstAsync(ct);
        return new AnnouncementDto(entity.Id, entity.Title, entity.Message, entity.Link, entity.Audience, entity.CourseId, courseTitle,
            entity.SendEmail, entity.RecipientCount, entity.EmailCount, name, entity.CreatedAt);
    }

    public async Task<PagedResult<AnnouncementDto>> ListAnnouncementsAsync(PagedQuery q, CancellationToken ct = default)
    {
        var page = await db.Announcements.AsNoTracking().OrderByDescending(a => a.CreatedAt).ToPagedResultAsync(q.Page, q.PageSize, ct);
        var adminIds = page.Items.Select(a => a.CreatedById).Distinct().ToList();
        var courseIds = page.Items.Where(a => a.CourseId != null).Select(a => a.CourseId!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => adminIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var courses = await db.Courses.AsNoTracking().Where(c => courseIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Title, ct);
        return new PagedResult<AnnouncementDto>(page.Items.Select(a => new AnnouncementDto(a.Id, a.Title, a.Message, a.Link, a.Audience, a.CourseId,
            a.CourseId is { } c ? courses.GetValueOrDefault(c) : null, a.SendEmail, a.RecipientCount, a.EmailCount,
            names.GetValueOrDefault(a.CreatedById, "Quản trị viên"), a.CreatedAt)).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    // ---------------------------------------------------------------- xuất người dùng
    public async Task<byte[]> ExportUsersCsvAsync(AdminUserQuery q, CancellationToken ct = default)
    {
        var query = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(u => u.FullName.Contains(kw) || u.Email!.Contains(kw));
        }
        if (q.IsActive is { } active) query = query.Where(u => u.IsActive == active);
        if (!string.IsNullOrWhiteSpace(q.Role))
        {
            var role = q.Role.Trim();
            var ids = from ur in db.Set<IdentityUserRole<Guid>>() join r in db.Set<AppRole>() on ur.RoleId equals r.Id where r.Name == role select ur.UserId;
            query = query.Where(u => ids.Contains(u.Id));
        }
        var rows = await query.OrderByDescending(u => u.CreatedAt).Take(50_000)
            .Select(u => new { u.Id, u.FullName, u.Email, u.PhoneNumber, u.IsActive, u.EmailConfirmed, u.CreatedAt, u.ReminderEmailsDisabled,
                Enrollments = u.Enrollments.Count(e => e.Status != EnrollmentStatus.Revoked), Completed = u.Enrollments.Count(e => e.Status == EnrollmentStatus.Completed),
                Spent = u.Orders.Where(o => o.Status == OrderStatus.Paid).Sum(o => (decimal?)o.TotalAmount) ?? 0 })
            .ToListAsync(ct);
        var ids2 = rows.Select(r => r.Id).ToList();
        var roles = await (from ur in db.Set<IdentityUserRole<Guid>>() join r in db.Set<AppRole>() on ur.RoleId equals r.Id
                           where ids2.Contains(ur.UserId) select new { ur.UserId, r.Name }).ToListAsync(ct);
        var sb = new StringBuilder("Họ tên,Email,Điện thoại,Vai trò,Trạng thái,Đã xác nhận email,Khóa đã ghi danh,Khóa hoàn thành,Tổng chi tiêu,Nhận email nhắc nhở,Ngày tạo (UTC+7)\n");
        foreach (var u in rows)
            sb.Append(CsvExport.Field(u.FullName)).Append(',').Append(CsvExport.Field(u.Email)).Append(',').Append(CsvExport.Field(u.PhoneNumber)).Append(',')
              .Append(CsvExport.Field(string.Join(" ", roles.Where(r => r.UserId == u.Id).Select(r => r.Name)))).Append(',')
              .Append(u.IsActive ? "Hoạt động" : "Đã khóa").Append(',').Append(u.EmailConfirmed ? "Có" : "Chưa").Append(',')
              .Append(u.Enrollments).Append(',').Append(u.Completed).Append(',').Append(CsvExport.Money(u.Spent)).Append(',')
              .Append(u.ReminderEmailsDisabled ? "Không" : "Có").Append(',').Append(CsvExport.Time(u.CreatedAt)).AppendLine();
        return CsvExport.ToBytes(sb);
    }
}
