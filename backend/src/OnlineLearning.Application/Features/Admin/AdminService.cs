using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Admin;

public sealed record MonthlyPointDto(string Month, decimal Revenue, int Orders, int NewUsers);
public sealed record AdminOverviewDto(
    int TotalUsers, int TotalStudents, int TotalInstructors, int NewUsersLast30Days,
    int PublishedCourses, int PendingCourses, int DraftCourses, int RejectedCourses,
    int PaidOrders, int PendingOrders, decimal TotalRevenue, decimal RevenueLast30Days,
    int TotalEnrollments, int TotalCertificates, IReadOnlyList<MonthlyPointDto> Monthly);

public sealed class AdminUserQuery : PagedQuery
{
    public string? Keyword { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}

public sealed record AdminUserDto(Guid Id, string Email, string FullName, string? AvatarUrl, string? PhoneNumber,
    bool IsActive, bool EmailConfirmed, IReadOnlyList<string> Roles, int EnrollmentCount, int CourseCount, DateTime CreatedAt);

public sealed record SetRolesRequest(IReadOnlyList<string> Roles);
public sealed record SetActiveRequest(bool IsActive);

public sealed class SetRolesRequestValidator : AbstractValidator<SetRolesRequest>
{
    public SetRolesRequestValidator() =>
        RuleFor(x => x.Roles).NotEmpty().WithMessage("Người dùng cần ít nhất một vai trò.")
            .Must(r => r.All(x => Domain.Constants.Roles.All.Contains(x))).WithMessage("Vai trò không hợp lệ.");
}

public interface IAdminService
{
    Task<AdminOverviewDto> GetOverviewAsync(CancellationToken ct = default);
    Task<PagedResult<AdminUserDto>> ListUsersAsync(AdminUserQuery query, CancellationToken ct = default);
    Task<Result<AdminUserDto>> SetRolesAsync(Guid actorId, Guid userId, SetRolesRequest request, CancellationToken ct = default);
    Task<Result<AdminUserDto>> SetActiveAsync(Guid actorId, Guid userId, bool isActive, CancellationToken ct = default);
}

public sealed class AdminService(IAppDbContext db, UserManager<AppUser> userManager, IDateTimeProvider clock) : IAdminService
{
    public async Task<AdminOverviewDto> GetOverviewAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var last30 = now.AddDays(-30);
        var from12 = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);

        var roleCounts = await (from ur in db.Set<IdentityUserRole<Guid>>()
                                join r in db.Set<AppRole>() on ur.RoleId equals r.Id
                                group ur by r.Name into g
                                select new { Role = g.Key, Count = g.Count() }).ToListAsync(ct);

        var courseCounts = await db.Courses.Where(c => !c.IsDeleted).GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);

        var paidOrders = db.Orders.Where(o => o.Status == OrderStatus.Paid);
        var paidRows = await paidOrders.Where(o => o.PaidAt >= from12)
            .Select(o => new { o.PaidAt, o.TotalAmount }).ToListAsync(ct);
        var userRows = await db.Users.Where(u => u.CreatedAt >= from12).Select(u => u.CreatedAt).ToListAsync(ct);

        var monthly = Enumerable.Range(0, 12).Select(i =>
        {
            var m = from12.AddMonths(i);
            var inMonth = paidRows.Where(o => o.PaidAt!.Value.Year == m.Year && o.PaidAt.Value.Month == m.Month).ToList();
            return new MonthlyPointDto($"{m:yyyy-MM}", inMonth.Sum(o => o.TotalAmount), inMonth.Count,
                userRows.Count(u => u.Year == m.Year && u.Month == m.Month));
        }).ToList();

        int Count(CourseStatus s) => courseCounts.FirstOrDefault(c => c.Status == s)?.Count ?? 0;
        int RoleCount(string r) => roleCounts.FirstOrDefault(c => c.Role == r)?.Count ?? 0;

        var paidAmounts = await paidOrders.Select(o => new { o.TotalAmount, o.PaidAt }).ToListAsync(ct);

        return new AdminOverviewDto(
            await db.Users.CountAsync(ct), RoleCount(Roles.Student), RoleCount(Roles.Instructor),
            await db.Users.CountAsync(u => u.CreatedAt >= last30, ct),
            Count(CourseStatus.Published), Count(CourseStatus.Pending), Count(CourseStatus.Draft), Count(CourseStatus.Rejected),
            paidAmounts.Count, await db.Orders.CountAsync(o => o.Status == OrderStatus.Pending, ct),
            paidAmounts.Sum(o => o.TotalAmount), paidAmounts.Where(o => o.PaidAt >= last30).Sum(o => o.TotalAmount),
            await db.Enrollments.CountAsync(ct), await db.Certificates.CountAsync(c => !c.IsRevoked, ct), monthly);
    }

    public async Task<PagedResult<AdminUserDto>> ListUsersAsync(AdminUserQuery q, CancellationToken ct = default)
    {
        var users = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            users = users.Where(u => u.FullName.Contains(kw) || u.Email!.Contains(kw) || (u.PhoneNumber != null && u.PhoneNumber.Contains(kw)));
        }
        if (q.IsActive is { } active) users = users.Where(u => u.IsActive == active);
        if (!string.IsNullOrWhiteSpace(q.Role))
        {
            var role = q.Role.Trim().ToUpperInvariant();
            var ids = from ur in db.Set<IdentityUserRole<Guid>>()
                      join r in db.Set<AppRole>() on ur.RoleId equals r.Id
                      where r.NormalizedName == role
                      select ur.UserId;
            users = users.Where(u => ids.Contains(u.Id));
        }

        var page = await users.OrderByDescending(u => u.CreatedAt)
            .Select(u => new
            {
                u.Id, Email = u.Email!, u.FullName, u.AvatarUrl, u.PhoneNumber, u.IsActive, u.EmailConfirmed, u.CreatedAt,
                Enrollments = u.Enrollments.Count, Courses = u.TaughtCourses.Count(c => !c.IsDeleted),
            })
            .ToPagedResultAsync(q.Page, q.PageSize, ct);

        var userIds = page.Items.Select(i => i.Id).ToList();
        var roles = await (from ur in db.Set<IdentityUserRole<Guid>>()
                           join r in db.Set<AppRole>() on ur.RoleId equals r.Id
                           where userIds.Contains(ur.UserId)
                           select new { ur.UserId, r.Name }).ToListAsync(ct);

        var items = page.Items.Select(u => new AdminUserDto(u.Id, u.Email, u.FullName, u.AvatarUrl, u.PhoneNumber, u.IsActive,
            u.EmailConfirmed, roles.Where(r => r.UserId == u.Id).Select(r => r.Name!).OrderBy(n => n).ToList(),
            u.Enrollments, u.Courses, u.CreatedAt)).ToList();

        return new PagedResult<AdminUserDto>(items, page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<Result<AdminUserDto>> SetRolesAsync(Guid actorId, Guid userId, SetRolesRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Error.NotFound("Không tìm thấy người dùng.");

        var desired = request.Roles.Distinct().ToList();
        if (actorId == userId && !desired.Contains(Roles.Admin))
            return Error.Conflict("Bạn không thể tự gỡ quyền Admin của chính mình.");

        var current = await userManager.GetRolesAsync(user);
        var toRemove = current.Except(desired).ToList();
        var toAdd = desired.Except(current).ToList();
        if (toRemove.Count > 0) await userManager.RemoveFromRolesAsync(user, toRemove);
        if (toAdd.Count > 0) await userManager.AddToRolesAsync(user, toAdd);
        await userManager.UpdateSecurityStampAsync(user);

        return await LoadUserAsync(userId, ct);
    }

    public async Task<Result<AdminUserDto>> SetActiveAsync(Guid actorId, Guid userId, bool isActive, CancellationToken ct = default)
    {
        if (actorId == userId && !isActive) return Error.Conflict("Bạn không thể tự khóa tài khoản của mình.");

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Error.NotFound("Không tìm thấy người dùng.");

        user.IsActive = isActive;
        user.UpdatedAt = clock.UtcNow;
        await userManager.UpdateAsync(user);
        await userManager.SetLockoutEndDateAsync(user, isActive ? null : DateTimeOffset.MaxValue);

        if (!isActive)
        {
            var tokens = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
            foreach (var t in tokens) t.RevokedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return await LoadUserAsync(userId, ct);
    }

    private async Task<Result<AdminUserDto>> LoadUserAsync(Guid userId, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().Where(x => x.Id == userId)
            .Select(x => new { x.Id, Email = x.Email!, x.FullName, x.AvatarUrl, x.PhoneNumber, x.IsActive, x.EmailConfirmed, x.CreatedAt,
                Enrollments = x.Enrollments.Count, Courses = x.TaughtCourses.Count(c => !c.IsDeleted) })
            .FirstAsync(ct);
        var roles = await (from ur in db.Set<IdentityUserRole<Guid>>()
                           join r in db.Set<AppRole>() on ur.RoleId equals r.Id
                           where ur.UserId == userId
                           select r.Name!).ToListAsync(ct);
        return new AdminUserDto(u.Id, u.Email, u.FullName, u.AvatarUrl, u.PhoneNumber, u.IsActive, u.EmailConfirmed,
            roles.OrderBy(r => r).ToList(), u.Enrollments, u.Courses, u.CreatedAt);
    }
}
