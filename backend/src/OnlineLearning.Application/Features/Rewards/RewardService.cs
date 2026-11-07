using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OnlineLearning.Application.Common.Helpers;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Notifications;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Rewards;

// ---------------------------------------------------------------- DTOs

public sealed record CheckInDayDto(DateOnly Date, bool Checked, bool IsToday);
public sealed record CheckInStatusDto(bool Enabled, bool CheckedInToday, int CurrentStreak, int StreakTarget, int StreakBonus,
    int DailyPoints, bool StreakBonusEnabled, IReadOnlyList<CheckInDayDto> RecentDays);
public sealed record RewardTaskDto(string Key, string Name, string? Description, int Points, string Condition, bool Enabled, bool DoneToday);
public sealed record ReferralInfoDto(string Code, int ReferredCount, int RewardedCount, int PointsPerReferral, bool Enabled);
public sealed record RedeemConfigDto(bool Enabled, int PointsPerUnit, decimal ValuePerUnit, int MinPoints, IReadOnlyList<int> Options);
public sealed record PointSummaryDto(int Balance, int TotalEarned, int TotalSpent, CheckInStatusDto CheckIn,
    IReadOnlyList<RewardTaskDto> Tasks, ReferralInfoDto Referral, RedeemConfigDto Redeem);
public sealed record PointTransactionDto(Guid Id, int Points, int BalanceAfter, PointTransactionType Type, string? RuleKey,
    string Description, DateTime CreatedAt);
public sealed record CheckInResultDto(int PointsAwarded, int StreakBonus, int Streak, int Balance);
public sealed record ShareLinkDto(string Token, Guid CourseId, string CourseSlug, string CourseTitle, ShareChannel Channel, string ReferralCode);
public sealed record RedemptionDto(Guid Id, int Points, decimal VoucherValue, string CouponCode, DateTime ExpiresAt, bool Used, DateTime CreatedAt);

public sealed record CreateShareRequest(Guid CourseId, ShareChannel Channel);
public sealed record RedeemRequest(int Points);

public sealed class CreateShareRequestValidator : AbstractValidator<CreateShareRequest>
{
    public CreateShareRequestValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Channel).IsInEnum();
    }
}

public sealed class RedeemRequestValidator : AbstractValidator<RedeemRequest>
{
    public RedeemRequestValidator() => RuleFor(x => x.Points).InclusiveBetween(1, 100_000).WithMessage("Số điểm không hợp lệ.");
}

/// <summary>Kết quả một bút toán trong sổ cái điểm.</summary>
public enum LedgerStatus { Applied, Duplicate, Insufficient, Skipped }
public sealed record LedgerResult(LedgerStatus Status, int Points, int BalanceAfter);

// ---------------------------------------------------------------- Service

public interface IRewardService
{
    Task<PointSummaryDto> GetSummaryAsync(Guid userId, CancellationToken ct = default);
    Task<PagedResult<PointTransactionDto>> GetTransactionsAsync(Guid userId, PagedQuery query, CancellationToken ct = default);
    Task<Result<CheckInResultDto>> CheckInAsync(Guid userId, CancellationToken ct = default);
    Task<Result<ShareLinkDto>> CreateShareAsync(Guid userId, CreateShareRequest request, string creatorKey, CancellationToken ct = default);
    /// <summary>Ghi nhận lượt mở liên kết chia sẻ. Chỉ lượt mở của người khác (khác tài khoản và khác IP người tạo) mới hợp lệ.</summary>
    Task RecordShareOpenAsync(string token, Guid? visitorUserId, string visitorIpKey, string visitorKey, CancellationToken ct = default);
    Task<Result<RedemptionDto>> RedeemAsync(Guid userId, RedeemRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<RedemptionDto>> GetRedemptionsAsync(Guid userId, CancellationToken ct = default);

    // ---- Tích hợp (gọi từ thanh toán, học tập, đăng ký) ----
    /// <summary>Cộng điểm mua hàng + thưởng giới thiệu. Gọi bên trong transaction của MarkPaidAsync, trước SaveChanges.</summary>
    Task StagePurchaseRewardsAsync(Order order, CancellationToken ct = default);
    /// <summary>Thu hồi điểm khi hoàn tiền. Gọi bên trong transaction hoàn tiền, trước SaveChanges.</summary>
    Task StageRefundRevocationAsync(Order order, Guid adminId, CancellationToken ct = default);
    /// <summary>Thưởng hoàn thành khóa học (mỗi khóa một lần). Tự mở transaction.</summary>
    Task AwardCourseCompletionAsync(Guid enrollmentId, CancellationToken ct = default);
    /// <summary>Gắn người giới thiệu cho tài khoản mới đăng ký (mã sai hoặc tự giới thiệu thì bỏ qua).</summary>
    Task AttachReferralAsync(Guid newUserId, string? referralCode, CancellationToken ct = default);
}

internal static class Vnd
{
    private static readonly System.Globalization.CultureInfo Vi = new("vi-VN");
    public static string Format(decimal amount) => amount.ToString("N0", Vi) + "đ";
}

public sealed class RewardService(IAppDbContext db, IDateTimeProvider clock, INotificationService notifications, ILogger<RewardService> logger)
    : IRewardService
{
    /// <summary>Số liên kết chia sẻ tối đa một người tạo mỗi ngày (chống tạo token hàng loạt).</summary>
    public const int MaxSharesPerDay = 30;
    /// <summary>Liên kết chia sẻ cũ hơn số ngày này không còn được tính thưởng.</summary>
    public const int ShareRewardWindowDays = 30;
    public const int VoucherValidDays = 90;
    private static readonly int[] RedeemOptions = [100, 200, 500, 1000];

    /// <summary>Ngày hiện tại theo giờ Việt Nam (UTC+7, không có giờ mùa hè).</summary>
    public static DateOnly VietnamDate(DateTime utc) => DateOnly.FromDateTime(utc.AddHours(7));

    // ================================================================ Sổ cái

    /// <summary>
    /// Ghi một bút toán điểm. BẮT BUỘC chạy trong transaction (của người gọi hoặc do service mở);
    /// người gọi chịu trách nhiệm SaveChanges + Commit. Chống trùng bằng (UserId, ReferenceKey) —
    /// kiểm tra trước, và unique index chặn ở tầng DB khi có yêu cầu đồng thời.
    /// Số dư cập nhật bằng một câu UPDATE nguyên tử; lệnh trừ chỉ thành công khi đủ điểm (không bao giờ âm).
    /// </summary>
    internal async Task<LedgerResult> ApplyAsync(Guid userId, int points, PointTransactionType type, string? ruleKey,
        string referenceKey, string description, Guid? createdById, bool allowPartialDebit, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Bút toán điểm phải chạy trong transaction.");
        if (points == 0) return new LedgerResult(LedgerStatus.Skipped, 0, 0);
        if (await db.PointTransactions.AnyAsync(t => t.UserId == userId && t.ReferenceKey == referenceKey, ct))
            return new LedgerResult(LedgerStatus.Duplicate, 0, 0);

        var now = clock.UtcNow;
        if (!await db.UserPoints.AnyAsync(p => p.UserId == userId, ct))
        {
            db.UserPoints.Add(new UserPoints { UserId = userId, CreatedAt = now, UpdatedAt = now });
            await db.SaveChangesAsync(ct);
        }

        var applied = points;
        if (points > 0)
        {
            await db.UserPoints.Where(p => p.UserId == userId).ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Balance, p => p.Balance + points)
                .SetProperty(p => p.TotalEarned, p => p.TotalEarned + points)
                .SetProperty(p => p.UpdatedAt, now), ct);
        }
        else
        {
            var amount = -points;
            if (allowPartialDebit)
            {
                var current = await db.UserPoints.Where(p => p.UserId == userId).Select(p => p.Balance).FirstAsync(ct);
                amount = Math.Min(amount, current);
                if (amount == 0) return new LedgerResult(LedgerStatus.Insufficient, 0, 0);
            }
            var rows = await db.UserPoints.Where(p => p.UserId == userId && p.Balance >= amount).ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Balance, p => p.Balance - amount)
                .SetProperty(p => p.TotalSpent, p => p.TotalSpent + amount)
                .SetProperty(p => p.UpdatedAt, now), ct);
            if (rows == 0) return new LedgerResult(LedgerStatus.Insufficient, 0, 0);
            applied = -amount;
        }

        var balance = await db.UserPoints.Where(p => p.UserId == userId).Select(p => p.Balance).FirstAsync(ct);
        db.PointTransactions.Add(new PointTransaction
        {
            UserId = userId, Points = applied, BalanceAfter = balance, Type = type, RuleKey = ruleKey,
            ReferenceKey = referenceKey, Description = description, CreatedById = createdById, CreatedAt = now,
        });
        return new LedgerResult(LedgerStatus.Applied, applied, balance);
    }

    /// <summary>Chạy một thao tác điểm độc lập trong transaction riêng; trùng khóa duy nhất (yêu cầu đồng thời) → lỗi Conflict.</summary>
    internal async Task<Result<T>> InTransactionAsync<T>(Func<Task<Result<T>>> work, string conflictMessage, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await work();
            if (result.IsFailure)
            {
                await tx.RollbackAsync(ct);
                return result;
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateException ex)
        {
            logger.LogInformation(ex, "Bút toán điểm bị từ chối do trùng/đồng thời");
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return Error.Conflict(conflictMessage);
        }
    }

    private Task<PointRule?> RuleAsync(string key, CancellationToken ct) =>
        db.PointRules.AsNoTracking().FirstOrDefaultAsync(r => r.Key == key, ct);

    // ================================================================ Học viên

    public async Task<PointSummaryDto> GetSummaryAsync(Guid userId, CancellationToken ct = default)
    {
        var account = await db.UserPoints.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct);
        var rules = await db.PointRules.AsNoTracking().ToDictionaryAsync(r => r.Key, ct);
        var today = VietnamDate(clock.UtcNow);

        var recent = await db.DailyCheckIns.AsNoTracking()
            .Where(c => c.UserId == userId && c.CheckInDate >= today.AddDays(-6))
            .ToListAsync(ct);
        var last = await db.DailyCheckIns.AsNoTracking().Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CheckInDate).FirstOrDefaultAsync(ct);
        var checkedToday = last?.CheckInDate == today;
        var streak = last is null ? 0 : last.CheckInDate >= today.AddDays(-1) ? last.StreakCount : 0;

        rules.TryGetValue(PointRuleKeys.DailyCheckIn, out var daily);
        rules.TryGetValue(PointRuleKeys.CheckInStreak, out var streakRule);
        var checkIn = new CheckInStatusDto(daily?.IsActive == true, checkedToday, streak,
            streakRule?.Threshold ?? 7, streakRule?.Points ?? 0, daily?.Points ?? 0, streakRule?.IsActive == true,
            Enumerable.Range(0, 7).Select(i => today.AddDays(i - 6))
                .Select(d => new CheckInDayDto(d, recent.Any(c => c.CheckInDate == d), d == today)).ToList());

        var sharedToday = await db.PointTransactions.AnyAsync(t => t.UserId == userId && t.ReferenceKey == $"share:{today:yyyy-MM-dd}", ct);
        var tasks = rules.Values.Where(r => r.IsSystem && r.Key != PointRuleKeys.RedeemVoucher).OrderBy(r => r.SortOrder)
            .Select(r => new RewardTaskDto(r.Key, r.Name, r.Description, r.Points, ConditionOf(r), r.IsActive,
                r.Key switch
                {
                    PointRuleKeys.DailyCheckIn => checkedToday,
                    PointRuleKeys.CourseShare => sharedToday,
                    _ => false,
                }))
            .ToList();

        var code = await EnsureReferralCodeAsync(userId, ct);
        var referred = await db.Referrals.CountAsync(r => r.ReferrerId == userId, ct);
        var rewarded = await db.Referrals.CountAsync(r => r.ReferrerId == userId && r.RewardedAt != null, ct);
        rules.TryGetValue(PointRuleKeys.Referral, out var referralRule);
        rules.TryGetValue(PointRuleKeys.RedeemVoucher, out var redeem);

        return new PointSummaryDto(account?.Balance ?? 0, account?.TotalEarned ?? 0, account?.TotalSpent ?? 0, checkIn, tasks,
            new ReferralInfoDto(code, referred, rewarded, referralRule?.Points ?? 0, referralRule?.IsActive == true),
            RedeemConfigOf(redeem));
    }

    private static RedeemConfigDto RedeemConfigOf(PointRule? r)
    {
        if (r is null || r.Points <= 0 || r.AmountUnit is not > 0) return new RedeemConfigDto(false, 0, 0, 0, []);
        var min = Math.Max(r.Threshold ?? r.Points, r.Points);
        return new RedeemConfigDto(r.IsActive, r.Points, r.AmountUnit.Value, min,
            RedeemOptions.Where(o => o >= min && o % r.Points == 0).ToList());
    }

    public static string ConditionOf(PointRule r) => r.Key switch
    {
        PointRuleKeys.Purchase => $"{r.Points} điểm cho mỗi {Vnd.Format(r.AmountUnit ?? 0)} thanh toán thành công (làm tròn xuống)",
        PointRuleKeys.CourseCompletion => "Hoàn thành khóa học (mỗi khóa một lần)",
        PointRuleKeys.DailyCheckIn => "Một lần mỗi ngày",
        PointRuleKeys.CheckInStreak => $"Điểm danh liên tục {r.Threshold ?? 7} ngày",
        PointRuleKeys.CourseShare => "Có người mở liên kết bạn chia sẻ, tối đa một lần mỗi ngày",
        PointRuleKeys.Referral => "Người được giới thiệu thanh toán thành công đơn đầu tiên",
        PointRuleKeys.RedeemVoucher => $"{r.Points} điểm = {Vnd.Format(r.AmountUnit ?? 0)} giảm giá",
        _ => "Do quản trị viên trao thủ công",
    };

    public Task<PagedResult<PointTransactionDto>> GetTransactionsAsync(Guid userId, PagedQuery query, CancellationToken ct = default) =>
        db.PointTransactions.AsNoTracking().Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new PointTransactionDto(t.Id, t.Points, t.BalanceAfter, t.Type, t.RuleKey, t.Description, t.CreatedAt))
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

    public Task<Result<CheckInResultDto>> CheckInAsync(Guid userId, CancellationToken ct = default) =>
        InTransactionAsync<CheckInResultDto>(async () =>
        {
            var daily = await RuleAsync(PointRuleKeys.DailyCheckIn, ct);
            if (daily is not { IsActive: true }) return Error.Conflict("Điểm danh đang tạm dừng.");

            var now = clock.UtcNow;
            var today = VietnamDate(now);
            if (await db.DailyCheckIns.AnyAsync(c => c.UserId == userId && c.CheckInDate == today, ct))
                return Error.Conflict("Hôm nay bạn đã điểm danh rồi. Quay lại vào ngày mai nhé!");

            var yesterday = today.AddDays(-1);
            var prev = await db.DailyCheckIns.Where(c => c.UserId == userId && c.CheckInDate == yesterday)
                .Select(c => (int?)c.StreakCount).FirstOrDefaultAsync(ct);
            // Bỏ lỡ một ngày thì chuỗi bắt đầu lại từ 1
            var streak = (prev ?? 0) + 1;

            var checkIn = new DailyCheckIn { UserId = userId, CheckInDate = today, StreakCount = streak, CreatedAt = now };
            db.DailyCheckIns.Add(checkIn);

            var main = await ApplyAsync(userId, daily.Points, PointTransactionType.Earn, daily.Key, $"checkin:{today:yyyy-MM-dd}",
                $"Điểm danh ngày {today:dd/MM/yyyy}", null, false, ct);
            if (main.Status == LedgerStatus.Duplicate) return Error.Conflict("Hôm nay bạn đã điểm danh rồi.");
            checkIn.PointsAwarded = main.Points;

            var bonus = 0;
            var streakRule = await RuleAsync(PointRuleKeys.CheckInStreak, ct);
            if (streakRule is { IsActive: true, Threshold: > 0 } && streak % streakRule.Threshold.Value == 0)
            {
                var b = await ApplyAsync(userId, streakRule.Points, PointTransactionType.Earn, streakRule.Key, $"streak:{today:yyyy-MM-dd}",
                    $"Thưởng điểm danh liên tục {streak} ngày", null, false, ct);
                bonus = b.Points;
                checkIn.StreakBonusAwarded = bonus;
                if (bonus > 0)
                    notifications.Add(userId, $"Chuỗi {streak} ngày: +{bonus} điểm",
                        $"Bạn đã điểm danh liên tục {streak} ngày và nhận thêm {bonus} điểm thưởng.", NotificationType.Points, "/hoc-vien/diem-thuong");
            }

            var balance = await db.UserPoints.Where(p => p.UserId == userId).Select(p => p.Balance).FirstAsync(ct);
            return new CheckInResultDto(main.Points, bonus, streak, balance);
        }, "Hôm nay bạn đã điểm danh rồi.", ct);

    public async Task<Result<ShareLinkDto>> CreateShareAsync(Guid userId, CreateShareRequest r, string creatorKey, CancellationToken ct = default)
    {
        var course = await db.Courses.AsNoTracking()
            .Where(c => c.Id == r.CourseId && c.Status == CourseStatus.Published && !c.IsDeleted)
            .Select(c => new { c.Id, c.Slug, c.Title }).FirstOrDefaultAsync(ct);
        if (course is null) return Error.NotFound("Không tìm thấy khóa học.");

        var since = clock.UtcNow.AddDays(-1);
        if (await db.CourseShares.CountAsync(s => s.UserId == userId && s.CreatedAt >= since, ct) >= MaxSharesPerDay)
            return Error.Conflict("Bạn đã tạo quá nhiều liên kết chia sẻ hôm nay. Hãy dùng lại liên kết đã có.");

        // Dùng lại liên kết cùng khóa + kênh trong ngày để không sinh token thừa
        var existing = await db.CourseShares.AsNoTracking()
            .Where(s => s.UserId == userId && s.CourseId == course.Id && s.Channel == r.Channel && s.CreatedAt >= since)
            .Select(s => s.Token).FirstOrDefaultAsync(ct);
        var code = await EnsureReferralCodeAsync(userId, ct);
        if (existing is not null) return new ShareLinkDto(existing, course.Id, course.Slug, course.Title, r.Channel, code);

        var share = new CourseShare
        {
            UserId = userId, CourseId = course.Id, Channel = r.Channel, Token = CodeGenerator.Random(12),
            CreatorKey = creatorKey, CreatedAt = clock.UtcNow,
        };
        db.CourseShares.Add(share);
        await db.SaveChangesAsync(ct);
        return new ShareLinkDto(share.Token, course.Id, course.Slug, course.Title, r.Channel, code);
    }

    public async Task RecordShareOpenAsync(string token, Guid? visitorUserId, string visitorIpKey, string visitorKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 32) return;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var share = await db.CourseShares.FirstOrDefaultAsync(s => s.Token == token, ct);
            if (share is null) return;
            if (await db.CourseShareOpens.AnyAsync(o => o.CourseShareId == share.Id && o.VisitorKey == visitorKey, ct)) return;

            var now = clock.UtcNow;
            // Lượt mở của chính người chia sẻ (cùng tài khoản hoặc cùng IP) không hợp lệ
            var valid = visitorUserId != share.UserId && visitorIpKey != share.CreatorKey;
            db.CourseShareOpens.Add(new CourseShareOpen
            {
                CourseShareId = share.Id, VisitorKey = visitorKey, VisitorUserId = visitorUserId, IsValid = valid, CreatedAt = now,
            });
            if (valid)
            {
                share.OpenCount += 1;
                share.FirstOpenedAt ??= now;
            }

            var rule = await RuleAsync(PointRuleKeys.CourseShare, ct);
            if (valid && rule is { IsActive: true } && share.CreatedAt >= now.AddDays(-ShareRewardWindowDays))
            {
                var today = VietnamDate(now);
                var courseTitle = await db.Courses.Where(c => c.Id == share.CourseId).Select(c => c.Title).FirstAsync(ct);
                var res = await ApplyAsync(share.UserId, rule.Points, PointTransactionType.Earn, rule.Key, $"share:{today:yyyy-MM-dd}",
                    $"Chia sẻ khóa học \"{courseTitle}\"", null, false, ct);
                if (res.Status == LedgerStatus.Applied)
                {
                    share.RewardedAt = now;
                    notifications.Add(share.UserId, $"+{res.Points} điểm từ lượt chia sẻ",
                        $"Có người vừa mở liên kết khóa học \"{courseTitle}\" bạn chia sẻ.", NotificationType.Points, "/hoc-vien/diem-thuong");
                }
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Hai lượt mở đồng thời của cùng người xem / cùng ngày thưởng: bỏ qua bản sau
            logger.LogInformation(ex, "Bỏ qua lượt mở chia sẻ trùng");
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
        }
    }

    public Task<Result<RedemptionDto>> RedeemAsync(Guid userId, RedeemRequest request, CancellationToken ct = default) =>
        InTransactionAsync<RedemptionDto>(async () =>
        {
            var rule = await RuleAsync(PointRuleKeys.RedeemVoucher, ct);
            var cfg = RedeemConfigOf(rule);
            if (!cfg.Enabled) return Error.Conflict("Đổi điểm đang tạm dừng.");
            if (request.Points < cfg.MinPoints || request.Points % cfg.PointsPerUnit != 0)
                return Error.Validation($"Số điểm đổi phải từ {cfg.MinPoints} và là bội số của {cfg.PointsPerUnit}.");

            var now = clock.UtcNow;
            var value = request.Points / cfg.PointsPerUnit * cfg.ValuePerUnit;
            var debit = await ApplyAsync(userId, -request.Points, PointTransactionType.Spend, PointRuleKeys.RedeemVoucher,
                $"redeem:{Guid.NewGuid():N}", $"Đổi {request.Points} điểm lấy mã giảm giá {Vnd.Format(value)}", null, false, ct);
            if (debit.Status == LedgerStatus.Insufficient) return Error.Validation("Bạn không đủ điểm để đổi.");

            string code;
            do code = $"DIEM{CodeGenerator.Random(8)}";
            while (await db.Coupons.AnyAsync(c => c.Code == code, ct));

            var coupon = new Coupon
            {
                Code = code, Description = $"Đổi từ {request.Points} điểm thưởng", DiscountType = DiscountType.FixedAmount,
                DiscountValue = value, MinOrderAmount = 0, UsageLimit = 1, PerUserLimit = 1, RestrictedToUserId = userId,
                StartsAt = now, ExpiresAt = now.AddDays(VoucherValidDays), IsActive = true,
            };
            db.Coupons.Add(coupon);
            var redemption = new PointRedemption
            {
                UserId = userId, Points = request.Points, VoucherValue = value, Coupon = coupon, CouponCode = code, CreatedAt = now,
            };
            db.PointRedemptions.Add(redemption);
            notifications.Add(userId, "Đổi điểm thành công", $"Mã {code} giảm {Vnd.Format(value)}, dùng được đến {coupon.ExpiresAt.AddHours(7):dd/MM/yyyy}.",
                NotificationType.Points, "/hoc-vien/diem-thuong");
            return new RedemptionDto(redemption.Id, request.Points, value, code, coupon.ExpiresAt, false, now);
        }, "Yêu cầu đổi điểm đang được xử lý, vui lòng thử lại.", ct);

    public async Task<IReadOnlyList<RedemptionDto>> GetRedemptionsAsync(Guid userId, CancellationToken ct = default) =>
        await db.PointRedemptions.AsNoTracking().Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt).Take(50)
            .Select(r => new RedemptionDto(r.Id, r.Points, r.VoucherValue, r.CouponCode, r.Coupon.ExpiresAt, r.Coupon.UsedCount > 0, r.CreatedAt))
            .ToListAsync(ct);

    // ================================================================ Tích hợp

    /// <summary>Số điểm mua hàng = floor(số tiền × Points / AmountUnit). Ví dụ 10 điểm/100.000đ: 249.000đ → 24 điểm.</summary>
    public static int PurchasePoints(decimal amount, PointRule rule) =>
        rule.AmountUnit is > 0 && amount > 0 ? (int)Math.Floor(amount * rule.Points / rule.AmountUnit.Value) : 0;

    public async Task StagePurchaseRewardsAsync(Order order, CancellationToken ct = default)
    {
        if (order.TotalAmount <= 0) return;

        var purchase = await RuleAsync(PointRuleKeys.Purchase, ct);
        if (purchase is { IsActive: true })
        {
            var pts = PurchasePoints(order.TotalAmount, purchase);
            var res = await ApplyAsync(order.UserId, pts, PointTransactionType.Earn, purchase.Key, $"purchase:{order.Id}",
                $"Mua khóa học, đơn {order.OrderCode} ({Vnd.Format(order.TotalAmount)})", null, false, ct);
            if (res.Status == LedgerStatus.Applied)
                notifications.Add(order.UserId, $"+{res.Points} điểm thưởng", $"Bạn nhận {res.Points} điểm từ đơn hàng {order.OrderCode}.",
                    NotificationType.Points, "/hoc-vien/diem-thuong");
        }

        // Thưởng người giới thiệu ở đơn có phí đầu tiên của người được giới thiệu
        var referral = await db.Referrals.FirstOrDefaultAsync(r => r.ReferredUserId == order.UserId && r.RewardedAt == null, ct);
        var referralRule = await RuleAsync(PointRuleKeys.Referral, ct);
        if (referral is not null && referralRule is { IsActive: true })
        {
            var res = await ApplyAsync(referral.ReferrerId, referralRule.Points, PointTransactionType.Earn, referralRule.Key,
                $"referral:{referral.ReferredUserId}", "Giới thiệu bạn bè mua khóa học thành công", null, false, ct);
            if (res.Status is LedgerStatus.Applied or LedgerStatus.Duplicate)
            {
                referral.RewardedAt = clock.UtcNow;
                referral.RewardOrderId = order.Id;
            }
            if (res.Status == LedgerStatus.Applied)
                notifications.Add(referral.ReferrerId, $"+{res.Points} điểm giới thiệu", "Người bạn giới thiệu vừa mua khóa học thành công.",
                    NotificationType.Points, "/hoc-vien/diem-thuong");
        }
    }

    public async Task StageRefundRevocationAsync(Order order, Guid adminId, CancellationToken ct = default)
    {
        // Thu hồi đúng số điểm đã cộng cho đơn; nếu người dùng đã tiêu bớt thì chỉ thu phần còn lại (không để số dư âm)
        var earned = await db.PointTransactions.Where(t => t.UserId == order.UserId && t.ReferenceKey == $"purchase:{order.Id}")
            .Select(t => t.Points).FirstOrDefaultAsync(ct);
        if (earned > 0)
        {
            var res = await ApplyAsync(order.UserId, -earned, PointTransactionType.Revoke, PointRuleKeys.Purchase, $"refund:{order.Id}",
                $"Thu hồi điểm do hoàn tiền đơn {order.OrderCode}", adminId, true, ct);
            if (res.Status == LedgerStatus.Applied)
                notifications.Add(order.UserId, $"Thu hồi {-res.Points} điểm", $"Đơn {order.OrderCode} đã được hoàn tiền nên điểm thưởng của đơn bị thu hồi.",
                    NotificationType.Points, "/hoc-vien/diem-thuong");
        }

        var referral = await db.Referrals.FirstOrDefaultAsync(r => r.RewardOrderId == order.Id, ct);
        if (referral is not null)
        {
            var refEarned = await db.PointTransactions
                .Where(t => t.UserId == referral.ReferrerId && t.ReferenceKey == $"referral:{referral.ReferredUserId}")
                .Select(t => t.Points).FirstOrDefaultAsync(ct);
            if (refEarned > 0)
                await ApplyAsync(referral.ReferrerId, -refEarned, PointTransactionType.Revoke, PointRuleKeys.Referral,
                    $"refund-referral:{order.Id}", $"Thu hồi thưởng giới thiệu do đơn {order.OrderCode} được hoàn tiền", adminId, true, ct);
        }
    }

    public async Task AwardCourseCompletionAsync(Guid enrollmentId, CancellationToken ct = default)
    {
        var rule = await RuleAsync(PointRuleKeys.CourseCompletion, ct);
        if (rule is not { IsActive: true }) return;
        var e = await db.Enrollments.AsNoTracking().Where(x => x.Id == enrollmentId && x.Status == EnrollmentStatus.Completed)
            .Select(x => new { x.UserId, x.CourseId, x.Course.Title }).FirstOrDefaultAsync(ct);
        if (e is null) return;
        // Đã thưởng khóa này rồi thì thôi (kể cả khi ghi danh lại)
        if (await db.PointTransactions.AnyAsync(t => t.UserId == e.UserId && t.ReferenceKey == $"course_completion:{e.CourseId}", ct)) return;

        var result = await InTransactionAsync<int>(async () =>
        {
            var res = await ApplyAsync(e.UserId, rule.Points, PointTransactionType.Earn, rule.Key, $"course_completion:{e.CourseId}",
                $"Hoàn thành khóa học \"{e.Title}\"", null, false, ct);
            if (res.Status == LedgerStatus.Applied)
                notifications.Add(e.UserId, $"+{res.Points} điểm hoàn thành khóa học", $"Chúc mừng bạn đã hoàn thành \"{e.Title}\".",
                    NotificationType.Points, "/hoc-vien/diem-thuong");
            return res.Points;
        }, "Đã thưởng hoàn thành khóa học.", ct);
        if (result.IsFailure) logger.LogInformation("Bỏ qua thưởng hoàn thành trùng cho ghi danh {EnrollmentId}", enrollmentId);
    }

    public async Task AttachReferralAsync(Guid newUserId, string? referralCode, CancellationToken ct = default)
    {
        var code = referralCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code) || code.Length > 16) return;
        var owner = await db.ReferralCodes.Where(r => r.Code == code).Select(r => (Guid?)r.UserId).FirstOrDefaultAsync(ct);
        if (owner is null || owner == newUserId || await db.Referrals.AnyAsync(r => r.ReferredUserId == newUserId, ct)) return;
        try
        {
            db.Referrals.Add(new Referral { ReferrerId = owner.Value, ReferredUserId = newUserId, CreatedAt = clock.UtcNow });
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogInformation(ex, "Không gắn được người giới thiệu cho {UserId}", newUserId);
            db.ChangeTracker.Clear();
        }
    }

    /// <summary>Mã giới thiệu riêng (8 ký tự), tạo khi cần lần đầu.</summary>
    internal async Task<string> EnsureReferralCodeAsync(Guid userId, CancellationToken ct)
    {
        var existing = await db.ReferralCodes.Where(r => r.UserId == userId).Select(r => r.Code).FirstOrDefaultAsync(ct);
        if (existing is not null) return existing;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = CodeGenerator.Random(8);
            if (await db.ReferralCodes.AnyAsync(r => r.Code == code, ct)) continue;
            try
            {
                var entity = new ReferralCode { UserId = userId, Code = code, CreatedAt = clock.UtcNow };
                db.ReferralCodes.Add(entity);
                await db.SaveChangesAsync(ct);
                return code;
            }
            catch (DbUpdateException)
            {
                // Yêu cầu song song đã tạo mã: đọc lại
                db.ChangeTracker.Clear();
                var again = await db.ReferralCodes.AsNoTracking().Where(r => r.UserId == userId).Select(r => r.Code).FirstOrDefaultAsync(ct);
                if (again is not null) return again;
            }
        }
        throw new InvalidOperationException("Không tạo được mã giới thiệu.");
    }
}
