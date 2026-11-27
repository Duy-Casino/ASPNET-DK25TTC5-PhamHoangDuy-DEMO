using System.Globalization;
using System.Net;
using System.Text;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnlineLearning.Application.Common.Helpers;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Common.Options;
using OnlineLearning.Application.Features.Courses;
using OnlineLearning.Application.Features.Notifications;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Growth;

// =====================================================================
// DTO
// =====================================================================
public sealed record WishlistItemDto(CourseSummaryDto Course, DateTime AddedAt);

public sealed record RecommendedCourseDto(CourseSummaryDto Course, string Reason);

public sealed record BundleCourseBriefDto(Guid Id, string Title, string Slug, string? ThumbnailUrl, decimal Price);
public sealed record BundleSummaryDto(Guid Id, string Title, string Slug, string? Description, string? ThumbnailUrl,
    decimal Price, decimal OriginalTotal, int SavingPercent, IReadOnlyList<BundleCourseBriefDto> Courses);
public sealed record BundleDetailDto(Guid Id, string Title, string Slug, string? Description, string? ThumbnailUrl,
    decimal Price, decimal OriginalTotal, int SavingPercent, IReadOnlyList<CourseSummaryDto> Courses,
    IReadOnlyList<Guid> OwnedCourseIds);
public sealed record AdminBundleDto(Guid Id, string Title, string Slug, string? Description, string? ThumbnailUrl, decimal Price,
    decimal OriginalTotal, bool IsActive, IReadOnlyList<BundleCourseBriefDto> Courses, int OrderCount, DateTime CreatedAt);
public sealed record SaveBundleRequest(string Title, string? Description, string? ThumbnailUrl, decimal Price, bool IsActive, IReadOnlyList<Guid> CourseIds);

public sealed record GiftViewDto(string SenderName, string RecipientName, string RecipientEmailMasked, string? Message, GiftStatus Status,
    IReadOnlyList<BundleCourseBriefDto> Courses, bool CanRedeem, string? BlockedReason, DateTime? RedeemedAt);
public sealed record RedeemGiftResultDto(int EnrolledCount, IReadOnlyList<string> AlreadyOwned);

public sealed class SaveBundleRequestValidator : AbstractValidator<SaveBundleRequest>
{
    public SaveBundleRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Nhập tên combo.").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.ThumbnailUrl).MaximumLength(500);
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Giá combo phải lớn hơn 0.").LessThanOrEqualTo(100_000_000);
        RuleFor(x => x.CourseIds).NotNull().Must(l => l is not null && l.Distinct().Count() >= 2 && l.Count <= 10)
            .WithMessage("Combo gồm từ 2 đến 10 khóa học khác nhau.");
    }
}

// =====================================================================
// Yêu thích
// =====================================================================
public interface IWishlistService
{
    Task<IReadOnlyList<Guid>> GetIdsAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<WishlistItemDto>> ListAsync(Guid userId, CancellationToken ct = default);
    Task<Result> AddAsync(Guid userId, Guid courseId, CancellationToken ct = default);
    Task RemoveAsync(Guid userId, Guid courseId, CancellationToken ct = default);
}

public sealed class WishlistService(IAppDbContext db, IDateTimeProvider clock) : IWishlistService
{
    public async Task<IReadOnlyList<Guid>> GetIdsAsync(Guid userId, CancellationToken ct = default) =>
        await db.WishlistItems.Where(w => w.UserId == userId).Select(w => w.CourseId).ToListAsync(ct);

    public async Task<IReadOnlyList<WishlistItemDto>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var items = await db.WishlistItems.AsNoTracking().Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt).Select(w => new { w.CourseId, w.CreatedAt }).ToListAsync(ct);
        var ids = items.Select(i => i.CourseId).ToList();
        var courses = await db.Courses.AsNoTracking()
            .Where(c => ids.Contains(c.Id) && c.Status == CourseStatus.Published && !c.IsDeleted)
            .Select(CatalogService.SummaryAt(clock.UtcNow)).ToListAsync(ct);
        return items.Where(i => courses.Any(c => c.Id == i.CourseId))
            .Select(i => new WishlistItemDto(courses.First(c => c.Id == i.CourseId), i.CreatedAt)).ToList();
    }

    public async Task<Result> AddAsync(Guid userId, Guid courseId, CancellationToken ct = default)
    {
        if (!await db.Courses.AnyAsync(c => c.Id == courseId && c.Status == CourseStatus.Published && !c.IsDeleted, ct))
            return Result.Failure(Error.NotFound("Khóa học không tồn tại hoặc chưa mở bán."));
        if (await db.WishlistItems.AnyAsync(w => w.UserId == userId && w.CourseId == courseId, ct)) return Result.Success();
        db.WishlistItems.Add(new WishlistItem { UserId = userId, CourseId = courseId, CreatedAt = clock.UtcNow });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); }   // bấm hai lần cùng lúc: đã có rồi
        return Result.Success();
    }

    public async Task RemoveAsync(Guid userId, Guid courseId, CancellationToken ct = default) =>
        await db.WishlistItems.Where(w => w.UserId == userId && w.CourseId == courseId).ExecuteDeleteAsync(ct);
}

// =====================================================================
// Gợi ý khóa học
// =====================================================================
public interface IRecommendationService
{
    /// <summary>
    /// Gợi ý cho một nhóm khóa học (trang chi tiết: 1 khóa; giỏ hàng: các khóa trong giỏ): trước hết là khóa học
    /// mà người đã mua các khóa này cũng mua, sau đó bổ sung khóa cùng chủ đề được học nhiều. Bỏ khóa người xem đã sở hữu.
    /// </summary>
    Task<IReadOnlyList<RecommendedCourseDto>> GetRelatedAsync(IReadOnlyCollection<Guid> courseIds, Guid? userId, int take, CancellationToken ct = default);
    Task<IReadOnlyList<RecommendedCourseDto>> GetForSlugAsync(string slug, Guid? userId, int take, CancellationToken ct = default);
    Task<IReadOnlyList<RecommendedCourseDto>> GetForCartAsync(Guid userId, int take, CancellationToken ct = default);
}

public sealed class RecommendationService(IAppDbContext db, IDateTimeProvider clock) : IRecommendationService
{
    public const string AlsoBought = "Học viên cũng mua";
    public const string SameTopic = "Cùng chủ đề";

    public async Task<IReadOnlyList<RecommendedCourseDto>> GetForSlugAsync(string slug, Guid? userId, int take, CancellationToken ct = default)
    {
        var id = await db.Courses.Where(c => c.Slug == slug && !c.IsDeleted).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
        return id is null ? [] : await GetRelatedAsync([id.Value], userId, take, ct);
    }

    public async Task<IReadOnlyList<RecommendedCourseDto>> GetForCartAsync(Guid userId, int take, CancellationToken ct = default)
    {
        var ids = await db.CartItems.Where(i => i.Cart.UserId == userId).Select(i => i.CourseId).ToListAsync(ct);
        return ids.Count == 0 ? [] : await GetRelatedAsync(ids, userId, take, ct);
    }

    public async Task<IReadOnlyList<RecommendedCourseDto>> GetRelatedAsync(IReadOnlyCollection<Guid> courseIds, Guid? userId, int take, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 12);
        var seeds = courseIds.ToList();
        var exclude = new HashSet<Guid>(seeds);
        if (userId is { } uid)
        {
            exclude.UnionWith(await db.Enrollments.Where(e => e.UserId == uid && e.Status != EnrollmentStatus.Revoked).Select(e => e.CourseId).ToListAsync(ct));
            exclude.UnionWith(await db.CartItems.Where(i => i.Cart.UserId == uid).Select(i => i.CourseId).ToListAsync(ct));
            exclude.UnionWith(await db.Courses.Where(c => c.InstructorId == uid).Select(c => c.Id).ToListAsync(ct));
        }
        var published = db.Courses.AsNoTracking().Where(c => c.Status == CourseStatus.Published && !c.IsDeleted);

        // 1) Mua kèm: học viên đã ghi danh các khóa hạt giống còn ghi danh những khóa nào, xếp theo số người
        var buyers = db.Enrollments.Where(e => seeds.Contains(e.CourseId) && e.Status != EnrollmentStatus.Revoked).Select(e => e.UserId);
        var coBought = await db.Enrollments
            .Where(e => buyers.Contains(e.UserId) && !seeds.Contains(e.CourseId) && e.Status != EnrollmentStatus.Revoked
                        && e.Course.Status == CourseStatus.Published && !e.Course.IsDeleted)
            .GroupBy(e => e.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(take + exclude.Count)
            .ToListAsync(ct);
        var picked = coBought.Where(x => !exclude.Contains(x.CourseId)).Take(take)
            .Select(x => (x.CourseId, Reason: AlsoBought)).ToList();

        // 2) Bổ sung khóa cùng chủ đề (cùng danh mục hoặc danh mục cha), học nhiều nhất
        if (picked.Count < take)
        {
            var cats = await db.Courses.Where(c => seeds.Contains(c.Id)).Select(c => new { c.CategoryId, ParentId = c.Category.ParentId }).ToListAsync(ct);
            var catIds = cats.Select(c => c.CategoryId).Concat(cats.Where(c => c.ParentId != null).Select(c => c.ParentId!.Value)).Distinct().ToList();
            var taken = picked.Select(p => p.CourseId).Concat(exclude).ToList();
            var topical = await published
                .Where(c => !taken.Contains(c.Id) && (catIds.Contains(c.CategoryId) || (c.Category.ParentId != null && catIds.Contains(c.Category.ParentId.Value))))
                .OrderByDescending(c => c.EnrollmentCount).ThenByDescending(c => c.AverageRating)
                .Take(take - picked.Count).Select(c => c.Id).ToListAsync(ct);
            picked.AddRange(topical.Select(id => (id, SameTopic)));
        }

        var ids = picked.Select(p => p.CourseId).ToList();
        var summaries = await published.Where(c => ids.Contains(c.Id)).Select(CatalogService.SummaryAt(clock.UtcNow)).ToListAsync(ct);
        return picked.Where(p => summaries.Any(s => s.Id == p.CourseId))
            .Select(p => new RecommendedCourseDto(summaries.First(s => s.Id == p.CourseId), p.Reason)).ToList();
    }
}

// =====================================================================
// Combo
// =====================================================================
public interface IBundleService
{
    Task<IReadOnlyList<BundleSummaryDto>> ListActiveAsync(Guid? courseId, CancellationToken ct = default);
    Task<Result<BundleDetailDto>> GetBySlugAsync(string slug, Guid? userId, CancellationToken ct = default);
    Task<IReadOnlyList<AdminBundleDto>> ListAllAsync(CancellationToken ct = default);
    Task<Result<AdminBundleDto>> CreateAsync(SaveBundleRequest request, CancellationToken ct = default);
    Task<Result<AdminBundleDto>> UpdateAsync(Guid id, SaveBundleRequest request, CancellationToken ct = default);
    /// <summary>Xóa combo chưa có đơn; combo đã có đơn chỉ được ẩn (giữ lịch sử đơn hàng).</summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

public sealed class BundleService(IAppDbContext db, IDateTimeProvider clock) : IBundleService
{
    private static int Saving(decimal price, decimal original) =>
        original <= 0 ? 0 : (int)Math.Floor((original - price) * 100 / original);

    /// <summary>Combo đang bán: bật, và mọi khóa học bên trong còn xuất bản.</summary>
    private IQueryable<Bundle> Sellable => db.Bundles.AsNoTracking()
        .Where(b => b.IsActive && b.Courses.Count >= 2 && b.Courses.All(x => x.Course.Status == CourseStatus.Published && !x.Course.IsDeleted));

    public async Task<IReadOnlyList<BundleSummaryDto>> ListActiveAsync(Guid? courseId, CancellationToken ct = default)
    {
        var query = Sellable;
        if (courseId is { } cid) query = query.Where(b => b.Courses.Any(x => x.CourseId == cid));
        var rows = await query.OrderByDescending(b => b.CreatedAt)
            .Select(b => new
            {
                b.Id, b.Title, b.Slug, b.Description, b.ThumbnailUrl, b.Price,
                Courses = b.Courses.OrderBy(x => x.SortOrder)
                    .Select(x => new BundleCourseBriefDto(x.CourseId, x.Course.Title, x.Course.Slug, x.Course.ThumbnailUrl, x.Course.Price)).ToList(),
            }).ToListAsync(ct);
        return rows.Select(b =>
        {
            var original = b.Courses.Sum(c => c.Price);
            return new BundleSummaryDto(b.Id, b.Title, b.Slug, b.Description, b.ThumbnailUrl ?? b.Courses.FirstOrDefault()?.ThumbnailUrl,
                b.Price, original, Saving(b.Price, original), b.Courses);
        }).ToList();
    }

    public async Task<Result<BundleDetailDto>> GetBySlugAsync(string slug, Guid? userId, CancellationToken ct = default)
    {
        var b = await Sellable.Where(x => x.Slug == slug)
            .Select(x => new { x.Id, x.Title, x.Slug, x.Description, x.ThumbnailUrl, x.Price,
                CourseIds = x.Courses.OrderBy(c => c.SortOrder).Select(c => c.CourseId).ToList() })
            .FirstOrDefaultAsync(ct);
        if (b is null) return Error.NotFound("Không tìm thấy combo.");

        var courses = await db.Courses.AsNoTracking().Where(c => b.CourseIds.Contains(c.Id))
            .Select(CatalogService.SummaryAt(clock.UtcNow)).ToListAsync(ct);
        courses = b.CourseIds.Select(id => courses.First(c => c.Id == id)).ToList();
        var owned = userId is { } uid
            ? await db.Enrollments.Where(e => e.UserId == uid && b.CourseIds.Contains(e.CourseId) && e.Status != EnrollmentStatus.Revoked)
                .Select(e => e.CourseId).ToListAsync(ct)
            : [];
        var original = courses.Sum(c => c.Price);
        return new BundleDetailDto(b.Id, b.Title, b.Slug, b.Description, b.ThumbnailUrl ?? courses.FirstOrDefault()?.ThumbnailUrl,
            b.Price, original, Saving(b.Price, original), courses, owned);
    }

    public async Task<IReadOnlyList<AdminBundleDto>> ListAllAsync(CancellationToken ct = default)
    {
        var rows = await db.Bundles.AsNoTracking().OrderByDescending(b => b.CreatedAt)
            .Select(b => new
            {
                b.Id, b.Title, b.Slug, b.Description, b.ThumbnailUrl, b.Price, b.IsActive, b.CreatedAt,
                Courses = b.Courses.OrderBy(x => x.SortOrder)
                    .Select(x => new BundleCourseBriefDto(x.CourseId, x.Course.Title, x.Course.Slug, x.Course.ThumbnailUrl, x.Course.Price)).ToList(),
                Orders = db.Orders.Count(o => o.BundleId == b.Id && o.Status == OrderStatus.Paid),
            }).ToListAsync(ct);
        return rows.Select(b => new AdminBundleDto(b.Id, b.Title, b.Slug, b.Description, b.ThumbnailUrl, b.Price,
            b.Courses.Sum(c => c.Price), b.IsActive, b.Courses, b.Orders, b.CreatedAt)).ToList();
    }

    private async Task<Result> ApplyAsync(Bundle bundle, SaveBundleRequest r, CancellationToken ct)
    {
        var ids = r.CourseIds.Distinct().ToList();
        var courses = await db.Courses.Where(c => ids.Contains(c.Id) && !c.IsDeleted).ToListAsync(ct);
        if (courses.Count != ids.Count || courses.Any(c => c.Status != CourseStatus.Published))
            return Result.Failure(Error.Validation("Combo chỉ gồm các khóa học đang xuất bản."));
        var original = courses.Sum(c => c.Price);
        if (r.Price >= original)
            return Result.Failure(Error.Validation($"Giá combo phải thấp hơn tổng giá gốc các khóa ({original.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))}đ)."));

        bundle.Title = r.Title.Trim();
        bundle.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        bundle.ThumbnailUrl = string.IsNullOrWhiteSpace(r.ThumbnailUrl) ? null : r.ThumbnailUrl.Trim();
        bundle.Price = r.Price;
        bundle.IsActive = r.IsActive;

        var baseSlug = "combo-" + SlugHelper.Slugify(bundle.Title);
        var slug = baseSlug;
        var i = 2;
        while (await db.Bundles.AnyAsync(b => b.Slug == slug && b.Id != bundle.Id, ct)) slug = $"{baseSlug}-{i++}";
        bundle.Slug = slug;

        var existing = await db.BundleCourses.Where(x => x.BundleId == bundle.Id).ToListAsync(ct);
        db.BundleCourses.RemoveRange(existing.Where(x => !ids.Contains(x.CourseId)));
        for (var k = 0; k < ids.Count; k++)
        {
            var row = existing.FirstOrDefault(x => x.CourseId == ids[k]);
            if (row is null) bundle.Courses.Add(new BundleCourse { CourseId = ids[k], SortOrder = k });
            else row.SortOrder = k;
        }
        return Result.Success();
    }

    public async Task<Result<AdminBundleDto>> CreateAsync(SaveBundleRequest request, CancellationToken ct = default)
    {
        var bundle = new Bundle();
        var applied = await ApplyAsync(bundle, request, ct);
        if (applied.IsFailure) return applied.Error!;
        db.Bundles.Add(bundle);
        await db.SaveChangesAsync(ct);
        return (await ListAllAsync(ct)).First(b => b.Id == bundle.Id);
    }

    public async Task<Result<AdminBundleDto>> UpdateAsync(Guid id, SaveBundleRequest request, CancellationToken ct = default)
    {
        var bundle = await db.Bundles.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (bundle is null) return Error.NotFound("Không tìm thấy combo.");
        var applied = await ApplyAsync(bundle, request, ct);
        if (applied.IsFailure) return applied.Error!;
        await db.SaveChangesAsync(ct);
        return (await ListAllAsync(ct)).First(b => b.Id == bundle.Id);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var bundle = await db.Bundles.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (bundle is null) return Result.Failure(Error.NotFound("Không tìm thấy combo."));
        if (await db.Orders.AnyAsync(o => o.BundleId == id, ct))
            return Result.Failure(Error.Conflict("Combo đã có đơn hàng nên không xóa được. Hãy tắt \"Đang bán\" để ẩn combo."));
        db.Bundles.Remove(bundle);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// =====================================================================
// Quà tặng
// =====================================================================
public interface IGiftService
{
    Task<Result<GiftViewDto>> GetAsync(string code, Guid? userId, CancellationToken ct = default);
    /// <summary>Người nhận (đăng nhập bằng đúng email được tặng) nhận khóa học; khóa đã sở hữu được bỏ qua.</summary>
    Task<Result<RedeemGiftResultDto>> RedeemAsync(Guid userId, string code, CancellationToken ct = default);
}

public sealed class GiftService(IAppDbContext db, IDateTimeProvider clock, INotificationService notifications, ILogger<GiftService> logger) : IGiftService
{
    private static string Mask(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1) return email;
        return email[0] + new string('*', Math.Max(1, at - 2)) + email[at - 1] + email[at..];
    }

    private async Task<(CourseGift? Gift, string? Email)> LoadAsync(string code, Guid? userId, bool tracking, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var q = db.CourseGifts.Include(g => g.Sender).Include(g => g.Order).ThenInclude(o => o.Items).AsQueryable();
        if (!tracking) q = q.AsNoTracking();
        var gift = await q.FirstOrDefaultAsync(g => g.Code == normalized, ct);
        var email = userId is { } uid ? await db.Users.Where(u => u.Id == uid).Select(u => u.Email).FirstOrDefaultAsync(ct) : null;
        return (gift, email);
    }

    private static string? Blocked(CourseGift g, string? email) =>
        g.Status == GiftStatus.Redeemed ? "Quà tặng này đã được nhận."
        : g.Status == GiftStatus.Cancelled || g.Order.Status != OrderStatus.Paid ? "Quà tặng không còn hiệu lực."
        : email is null ? "Đăng nhập hoặc đăng ký bằng email được tặng để nhận quà."
        : !string.Equals(email, g.RecipientEmail, StringComparison.OrdinalIgnoreCase) ? $"Quà tặng dành cho {Mask(g.RecipientEmail)}. Hãy đăng nhập bằng email đó."
        : null;

    public async Task<Result<GiftViewDto>> GetAsync(string code, Guid? userId, CancellationToken ct = default)
    {
        var (g, email) = await LoadAsync(code, userId, false, ct);
        if (g is null || g.Status == GiftStatus.Pending) return Error.NotFound("Không tìm thấy quà tặng.");
        var courseIds = g.Order.Items.Select(i => i.CourseId).ToList();
        var courses = await db.Courses.AsNoTracking().Where(c => courseIds.Contains(c.Id))
            .Select(c => new BundleCourseBriefDto(c.Id, c.Title, c.Slug, c.ThumbnailUrl, c.Price)).ToListAsync(ct);
        var blocked = Blocked(g, email);
        return new GiftViewDto(g.Sender.FullName, g.RecipientName, Mask(g.RecipientEmail), g.Message, g.Status, courses,
            blocked is null, blocked, g.RedeemedAt);
    }

    public async Task<Result<RedeemGiftResultDto>> RedeemAsync(Guid userId, string code, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (g, email) = await LoadAsync(code, userId, true, ct);
        if (g is null || g.Status == GiftStatus.Pending) return Error.NotFound("Không tìm thấy quà tặng.");
        if (Blocked(g, email) is { } reason) return Error.Conflict(reason);

        var now = clock.UtcNow;
        var courseIds = g.Order.Items.Select(i => i.CourseId).ToList();
        var owned = await db.Enrollments.Where(e => e.UserId == userId && courseIds.Contains(e.CourseId)).Select(e => e.CourseId).ToListAsync(ct);
        var courses = await db.Courses.Where(c => courseIds.Contains(c.Id)).ToListAsync(ct);
        var enrolled = 0;
        foreach (var c in courses.Where(c => !owned.Contains(c.Id)))
        {
            db.Enrollments.Add(new Enrollment { UserId = userId, CourseId = c.Id, OrderId = g.OrderId, EnrolledAt = now, Status = EnrollmentStatus.Active });
            c.EnrollmentCount += 1;
            enrolled++;
            notifications.Add(c.InstructorId, "Học viên mới", $"Có học viên mới đăng ký khóa học \"{c.Title}\" (quà tặng).", NotificationType.Order, "/giang-day/hoc-vien");
        }
        g.Status = GiftStatus.Redeemed;
        g.RedeemedAt = now;
        g.RedeemedById = userId;
        notifications.Add(g.SenderId, "Quà tặng đã được nhận", $"{g.RecipientName} đã nhận quà tặng của bạn (đơn {g.Order.OrderCode}).",
            NotificationType.Order, $"/don-hang/{g.OrderId}");

        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogInformation(ex, "Nhận quà tặng {Code} trùng lặp", g.Code);
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return Error.Conflict("Quà tặng đang được xử lý, vui lòng thử lại.");
        }
        return new RedeemGiftResultDto(enrolled, courses.Where(c => owned.Contains(c.Id)).Select(c => c.Title).ToList());
    }
}

// =====================================================================
// Nhắc giỏ hàng
// =====================================================================
public sealed class CartReminderOptions
{
    public const string SectionName = "CartReminder";
    public bool Enabled { get; set; } = true;
    /// <summary>Gửi nhắc khi khóa học thêm vào giỏ gần nhất đã quá số giờ này mà chưa thanh toán.</summary>
    public int DelayHours { get; set; } = 24;
}

public interface ICartReminderService
{
    /// <summary>Gửi email nhắc các giỏ hàng bị bỏ quên; mỗi lần giỏ thay đổi chỉ nhắc một lần. Trả về số email đã gửi.</summary>
    Task<int> SendDueRemindersAsync(CancellationToken ct = default);
}

public sealed class CartReminderService(
    IAppDbContext db, IEmailSender sender, IDateTimeProvider clock, IOptions<CartReminderOptions> options, IOptions<AppOptions> app,
    ILogger<CartReminderService> logger) : ICartReminderService
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public async Task<int> SendDueRemindersAsync(CancellationToken ct = default)
    {
        if (!options.Value.Enabled) return 0;
        var now = clock.UtcNow;
        var cutoff = now.AddHours(-Math.Max(1, options.Value.DelayHours));

        var due = await db.Carts
            .Where(c => c.Items.Any() && c.User.IsActive && c.User.Email != null)
            .Select(c => new { c.Id, c.UserId, c.ReminderSentAt, Last = c.Items.Max(i => i.AddedAt) })
            .Where(c => c.Last <= cutoff && (c.ReminderSentAt == null || c.ReminderSentAt < c.Last))
            .Take(200)
            .ToListAsync(ct);

        var sent = 0;
        var client = app.Value.ClientUrl.TrimEnd('/');
        foreach (var c in due)
        {
            var cart = await db.Carts.Include(x => x.User).Include(x => x.Items).ThenInclude(i => i.Course).FirstAsync(x => x.Id == c.Id, ct);
            cart.ReminderSentAt = now;
            var owned = await db.Enrollments.Where(e => e.UserId == c.UserId).Select(e => e.CourseId).ToListAsync(ct);
            var items = cart.Items.Where(i => i.Course.Status == CourseStatus.Published && !i.Course.IsDeleted && !owned.Contains(i.CourseId))
                .OrderBy(i => i.Course.Title).ToList();
            if (items.Count > 0)
            {
                try
                {
                    await sender.SendAsync(cart.User.Email!, "Bạn còn khóa học trong giỏ hàng", Body(cart.User.FullName, items, now, client), ct);
                    sent++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Không gửi được email nhắc giỏ hàng cho {UserId}", c.UserId);
                }
            }
            await db.SaveChangesAsync(ct);
        }
        if (sent > 0) logger.LogInformation("Đã gửi {Count} email nhắc giỏ hàng", sent);
        return sent;
    }

    private static string Body(string name, List<CartItem> items, DateTime now, string client)
    {
        static string E(string? s) => System.Text.Encodings.Web.HtmlEncoder.Create(System.Text.Unicode.UnicodeRanges.All).Encode(s ?? "");
        var sb = new StringBuilder();
        sb.Append($"<p>Chào {E(name)},</p><p>Bạn đã thêm {items.Count} khóa học vào giỏ nhưng chưa thanh toán:</p><ul>");
        foreach (var i in items)
        {
            var price = i.Course.EffectivePriceAt(now);
            var sale = i.Course.IsDiscountActive(now) && i.Course.DiscountEndsAt is { } end && end - now <= TimeSpan.FromHours(48)
                ? $" <b style=\"color:#d92d20\">· giảm giá kết thúc {end.AddHours(7).ToString("HH:mm dd/MM", Vi)}</b>" : "";
            sb.Append($"<li><a href=\"{client}/khoa-hoc/{i.Course.Slug}\">{E(i.Course.Title)}</a> · {price.ToString("#,0", Vi)}đ{sale}</li>");
        }
        sb.Append($"</ul><p><a href=\"{client}/gio-hang\">Hoàn tất thanh toán</a></p>");
        sb.Append("<p style=\"color:#64748b;font-size:12px\">Bạn nhận email này vì có khóa học trong giỏ hàng EduViet. Xóa khóa học khỏi giỏ để không nhận nhắc nữa.</p>");
        return sb.ToString();
    }
}
