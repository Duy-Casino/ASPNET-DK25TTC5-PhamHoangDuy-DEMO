using System.Globalization;
using System.Text;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Notifications;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Rewards;

// ---------------------------------------------------------------- DTOs

public sealed record RuleUsageDto(string RuleKey, string RuleName, int Count, int Points);
public sealed record RewardOverviewDto(int TotalIssued, int TotalSpent, int OutstandingBalance, int Holders, int TransactionCount,
    int IssuedLast30Days, IReadOnlyList<RuleUsageDto> TopRules);
public sealed record UserBalanceDto(Guid UserId, string FullName, string Email, string? AvatarUrl, int Balance, int TotalEarned,
    int TotalSpent, DateTime UpdatedAt);
public sealed record AdminPointTransactionDto(Guid Id, Guid UserId, string UserFullName, string UserEmail, int Points, int BalanceAfter,
    PointTransactionType Type, string? RuleKey, string Description, string? CreatedByName, DateTime CreatedAt);
public sealed record PointRuleDto(Guid Id, string Key, string Name, string? Description, int Points, decimal? AmountUnit, int? Threshold,
    bool IsActive, bool IsSystem, int SortOrder, string Condition, DateTime? UpdatedAt);

public sealed class PointTransactionQuery : PagedQuery
{
    public Guid? UserId { get; set; }
    public string? Keyword { get; set; }
    public string? RuleKey { get; set; }
    public PointTransactionType? Type { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public sealed class BalanceQuery : PagedQuery
{
    public string? Keyword { get; set; }
}

public sealed record SavePointRuleRequest(string? Key, string Name, string? Description, int Points, decimal? AmountUnit, int? Threshold,
    bool IsActive, int SortOrder);
public sealed record AdjustPointsRequest(Guid UserId, int Points, string Reason, string? RuleKey);

public sealed class SavePointRuleRequestValidator : AbstractValidator<SavePointRuleRequest>
{
    public SavePointRuleRequestValidator()
    {
        RuleFor(x => x.Key).MaximumLength(50).Matches("^[a-z0-9_]+$").When(x => !string.IsNullOrEmpty(x.Key))
            .WithMessage("Khóa chỉ gồm chữ thường, số và dấu gạch dưới.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Nhập tên nhiệm vụ.").MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Points).InclusiveBetween(0, 1_000_000).WithMessage("Số điểm từ 0 đến 1.000.000.");
        RuleFor(x => x.AmountUnit).GreaterThan(0).When(x => x.AmountUnit.HasValue).WithMessage("Đơn vị tiền phải lớn hơn 0.");
        RuleFor(x => x.Threshold).InclusiveBetween(1, 1_000_000).When(x => x.Threshold.HasValue).WithMessage("Ngưỡng phải lớn hơn 0.");
    }
}

public sealed class AdjustPointsRequestValidator : AbstractValidator<AdjustPointsRequest>
{
    public AdjustPointsRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Points).NotEqual(0).WithMessage("Số điểm điều chỉnh phải khác 0.")
            .InclusiveBetween(-1_000_000, 1_000_000).WithMessage("Số điểm điều chỉnh quá lớn.");
        RuleFor(x => x.Reason).Must(r => !string.IsNullOrWhiteSpace(r) && r.Trim().Length >= 5)
            .WithMessage("Bắt buộc ghi lý do điều chỉnh (ít nhất 5 ký tự).").MaximumLength(300);
    }
}

// ---------------------------------------------------------------- Service

public interface IRewardAdminService
{
    Task<RewardOverviewDto> GetOverviewAsync(CancellationToken ct = default);
    Task<PagedResult<UserBalanceDto>> ListBalancesAsync(BalanceQuery query, CancellationToken ct = default);
    Task<PagedResult<AdminPointTransactionDto>> ListTransactionsAsync(PointTransactionQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<PointRuleDto>> ListRulesAsync(CancellationToken ct = default);
    Task<Result<PointRuleDto>> CreateRuleAsync(SavePointRuleRequest request, CancellationToken ct = default);
    Task<Result<PointRuleDto>> UpdateRuleAsync(Guid id, SavePointRuleRequest request, CancellationToken ct = default);
    Task<Result> DeleteRuleAsync(Guid id, CancellationToken ct = default);
    Task<Result<AdminPointTransactionDto>> AdjustAsync(Guid adminId, AdjustPointsRequest request, CancellationToken ct = default);
    /// <summary>CSV (UTF-8 có BOM để mở bằng Excel) của lịch sử giao dịch theo bộ lọc, tối đa 50.000 dòng.</summary>
    Task<byte[]> ExportTransactionsCsvAsync(PointTransactionQuery query, CancellationToken ct = default);
}

public sealed class RewardAdminService(IAppDbContext db, IRewardService rewards, IDateTimeProvider clock, INotificationService notifications)
    : IRewardAdminService
{
    public async Task<RewardOverviewDto> GetOverviewAsync(CancellationToken ct = default)
    {
        var tx = db.PointTransactions.AsNoTracking();
        var issued = await tx.Where(t => t.Points > 0).SumAsync(t => (int?)t.Points, ct) ?? 0;
        var spent = -(await tx.Where(t => t.Points < 0).SumAsync(t => (int?)t.Points, ct) ?? 0);
        var outstanding = await db.UserPoints.SumAsync(p => (int?)p.Balance, ct) ?? 0;
        var holders = await db.UserPoints.CountAsync(p => p.Balance > 0, ct);
        var count = await tx.CountAsync(ct);
        var since = clock.UtcNow.AddDays(-30);
        var last30 = await tx.Where(t => t.Points > 0 && t.CreatedAt >= since).SumAsync(t => (int?)t.Points, ct) ?? 0;

        var names = await db.PointRules.AsNoTracking().ToDictionaryAsync(r => r.Key, r => r.Name, ct);
        var byRule = await tx.Where(t => t.Points > 0 && t.RuleKey != null)
            .GroupBy(t => t.RuleKey!)
            .Select(g => new { Key = g.Key, Count = g.Count(), Points = g.Sum(t => t.Points) })
            .ToListAsync(ct);
        var top = byRule.OrderByDescending(r => r.Count).ThenByDescending(r => r.Points)
            .Select(r => new RuleUsageDto(r.Key, names.GetValueOrDefault(r.Key, r.Key), r.Count, r.Points)).ToList();

        return new RewardOverviewDto(issued, spent, outstanding, holders, count, last30, top);
    }

    public Task<PagedResult<UserBalanceDto>> ListBalancesAsync(BalanceQuery q, CancellationToken ct = default)
    {
        var query = db.UserPoints.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(p => p.User.FullName.Contains(kw) || p.User.Email!.Contains(kw));
        }
        return query.OrderByDescending(p => p.Balance).ThenByDescending(p => p.TotalEarned)
            .Select(p => new UserBalanceDto(p.UserId, p.User.FullName, p.User.Email!, p.User.AvatarUrl, p.Balance, p.TotalEarned, p.TotalSpent, p.UpdatedAt))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);
    }

    private IQueryable<PointTransaction> Filter(PointTransactionQuery q)
    {
        var query = db.PointTransactions.AsNoTracking().AsQueryable();
        if (q.UserId is { } uid) query = query.Where(t => t.UserId == uid);
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(t => t.User.FullName.Contains(kw) || t.User.Email!.Contains(kw) || t.Description.Contains(kw));
        }
        if (!string.IsNullOrWhiteSpace(q.RuleKey)) query = query.Where(t => t.RuleKey == q.RuleKey);
        if (q.Type is { } type) query = query.Where(t => t.Type == type);
        if (q.From is { } from) query = query.Where(t => t.CreatedAt >= from);
        if (q.To is { } to) query = query.Where(t => t.CreatedAt < to);
        return query;
    }

    private static IQueryable<AdminPointTransactionDto> Project(IQueryable<PointTransaction> q, IAppDbContext db) =>
        q.OrderByDescending(t => t.CreatedAt).Select(t => new AdminPointTransactionDto(t.Id, t.UserId, t.User.FullName, t.User.Email!,
            t.Points, t.BalanceAfter, t.Type, t.RuleKey, t.Description,
            db.Users.Where(u => u.Id == t.CreatedById).Select(u => u.FullName).FirstOrDefault(), t.CreatedAt));

    public Task<PagedResult<AdminPointTransactionDto>> ListTransactionsAsync(PointTransactionQuery q, CancellationToken ct = default) =>
        Project(Filter(q), db).ToPagedResultAsync(q.Page, q.PageSize, ct);

    public async Task<IReadOnlyList<PointRuleDto>> ListRulesAsync(CancellationToken ct = default) =>
        (await db.PointRules.AsNoTracking().OrderBy(r => r.SortOrder).ThenBy(r => r.Name).ToListAsync(ct)).Select(ToDto).ToList();

    private static PointRuleDto ToDto(PointRule r) => new(r.Id, r.Key, r.Name, r.Description, r.Points, r.AmountUnit, r.Threshold,
        r.IsActive, r.IsSystem, r.SortOrder, RewardService.ConditionOf(r), r.UpdatedAt);

    /// <summary>Tạo nhiệm vụ tùy chỉnh (không gắn tự động với hoạt động nào): dùng làm nhãn khi Admin trao điểm thủ công.</summary>
    public async Task<Result<PointRuleDto>> CreateRuleAsync(SavePointRuleRequest r, CancellationToken ct = default)
    {
        var key = r.Key?.Trim();
        if (string.IsNullOrEmpty(key)) return Error.Validation("Nhập khóa cho nhiệm vụ.");
        if (PointRuleKeys.System.Contains(key) || await db.PointRules.AnyAsync(x => x.Key == key, ct))
            return Error.Conflict("Khóa nhiệm vụ đã tồn tại.");
        var rule = new PointRule
        {
            Key = key, Name = r.Name.Trim(), Description = r.Description?.Trim(), Points = r.Points, AmountUnit = r.AmountUnit,
            Threshold = r.Threshold, IsActive = r.IsActive, IsSystem = false, SortOrder = r.SortOrder,
        };
        db.PointRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task<Result<PointRuleDto>> UpdateRuleAsync(Guid id, SavePointRuleRequest r, CancellationToken ct = default)
    {
        var rule = await db.PointRules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (rule is null) return Error.NotFound("Không tìm thấy nhiệm vụ.");
        // Quy tắc có công thức cần đủ tham số để không phát điểm sai
        if (rule.Key is PointRuleKeys.Purchase or PointRuleKeys.RedeemVoucher && r.AmountUnit is not > 0)
            return Error.Validation("Quy tắc này cần đơn vị tiền lớn hơn 0.");
        if (rule.Key == PointRuleKeys.CheckInStreak && r.Threshold is not > 0)
            return Error.Validation("Nhập số ngày của chuỗi điểm danh.");
        if (rule.Key == PointRuleKeys.RedeemVoucher && r.Points <= 0)
            return Error.Validation("Số điểm mỗi đơn vị đổi phải lớn hơn 0.");

        rule.Name = r.Name.Trim();
        rule.Description = r.Description?.Trim();
        rule.Points = r.Points;
        rule.AmountUnit = r.AmountUnit;
        rule.Threshold = r.Threshold;
        rule.IsActive = r.IsActive;
        rule.SortOrder = r.SortOrder;
        await db.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task<Result> DeleteRuleAsync(Guid id, CancellationToken ct = default)
    {
        var rule = await db.PointRules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (rule is null) return Result.Failure(Error.NotFound("Không tìm thấy nhiệm vụ."));
        if (rule.IsSystem) return Result.Failure(Error.Conflict("Nhiệm vụ hệ thống không xóa được, chỉ có thể tắt."));
        db.PointRules.Remove(rule);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<AdminPointTransactionDto>> AdjustAsync(Guid adminId, AdjustPointsRequest r, CancellationToken ct = default)
    {
        if (!await db.Users.AnyAsync(u => u.Id == r.UserId, ct)) return Error.NotFound("Không tìm thấy người dùng.");
        PointRule? rule = null;
        if (!string.IsNullOrWhiteSpace(r.RuleKey))
        {
            rule = await db.PointRules.AsNoTracking().FirstOrDefaultAsync(x => x.Key == r.RuleKey, ct);
            if (rule is null) return Error.Validation("Nhiệm vụ không tồn tại.");
        }

        var service = (RewardService)rewards;
        var reason = r.Reason.Trim();
        var refKey = $"adjust:{Guid.NewGuid():N}";
        var result = await service.InTransactionAsync<int>(async () =>
        {
            var res = await service.ApplyAsync(r.UserId, r.Points, PointTransactionType.Adjust, rule?.Key,
                refKey, $"Điều chỉnh thủ công: {reason}", adminId, false, ct);
            if (res.Status == LedgerStatus.Insufficient)
                return Error.Validation("Không thể trừ quá số điểm hiện có của học viên.");
            notifications.Add(r.UserId, r.Points > 0 ? $"+{r.Points} điểm thưởng" : $"Điều chỉnh {r.Points} điểm",
                $"Quản trị viên đã điều chỉnh điểm của bạn. Lý do: {reason}", NotificationType.Points, "/hoc-vien/diem-thuong");
            return res.BalanceAfter;
        }, "Điều chỉnh đang được xử lý, vui lòng thử lại.", ct);
        if (result.IsFailure) return result.Error!;

        return await Project(db.PointTransactions.Where(t => t.UserId == r.UserId && t.ReferenceKey == refKey), db).FirstAsync(ct);
    }

    public async Task<byte[]> ExportTransactionsCsvAsync(PointTransactionQuery q, CancellationToken ct = default)
    {
        var rows = await Project(Filter(q), db).Take(50_000).ToListAsync(ct);
        var sb = new StringBuilder();
        sb.AppendLine("Thời gian (UTC+7),Học viên,Email,Loại,Nhiệm vụ,Điểm,Số dư sau,Mô tả,Người thực hiện");
        foreach (var t in rows)
        {
            sb.Append(t.CreatedAt.AddHours(7).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(',')
              .Append(Csv(t.UserFullName)).Append(',').Append(Csv(t.UserEmail)).Append(',')
              .Append(t.Type).Append(',').Append(Csv(t.RuleKey)).Append(',')
              .Append(t.Points.ToString(CultureInfo.InvariantCulture)).Append(',').Append(t.BalanceAfter.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(Csv(t.Description)).Append(',').Append(Csv(t.CreatedByName)).AppendLine();
        }
        var bom = Encoding.UTF8.GetPreamble();
        return [.. bom, .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    /// <summary>Trích CSV an toàn: bọc ngoặc kép và vô hiệu hóa công thức Excel (=, +, -, @).</summary>
    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var v = value.Replace("\"", "\"\"");
        if (v[0] is '=' or '+' or '-' or '@' or '\t' or '\r') v = "'" + v;
        return $"\"{v}\"";
    }
}
