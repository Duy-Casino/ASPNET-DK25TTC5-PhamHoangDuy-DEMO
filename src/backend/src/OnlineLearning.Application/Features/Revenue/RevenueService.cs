using System.Text;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnlineLearning.Application.Common.Helpers;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Notifications;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Revenue;

/// <summary>Cấu hình chia doanh thu, rút tiền và hoàn tiền (mục "Revenue" trong appsettings).</summary>
public sealed class RevenueOptions
{
    public const string SectionName = "Revenue";

    /// <summary>Tỷ lệ % giảng viên được hưởng trên số tiền học viên thực trả.</summary>
    public decimal InstructorSharePercent { get; set; } = 70;
    /// <summary>Số ngày giữ tiền trước khi được rút (bằng thời hạn yêu cầu hoàn tiền).</summary>
    public int HoldDays { get; set; } = 7;
    /// <summary>Số tiền tối thiểu mỗi lần rút.</summary>
    public decimal MinPayoutAmount { get; set; } = 100_000;
    /// <summary>Số ngày kể từ khi thanh toán mà học viên còn được yêu cầu hoàn tiền.</summary>
    public int RefundRequestDays { get; set; } = 7;
    /// <summary>Tỷ lệ % nội dung tối đa đã học để còn được hoàn tiền.</summary>
    public decimal RefundMaxProgressPercent { get; set; } = 20;
}

// ---------- DTO ----------
public sealed record EarningsSummaryDto(
    decimal SharePercent, int HoldDays, decimal MinPayoutAmount,
    decimal TotalEarned, decimal PendingAmount, decimal AvailableAmount, decimal PaidOutAmount, decimal InProcessAmount,
    decimal WithdrawableAmount, int SalesCount, int RefundCount, bool HasOpenPayout, PayoutBankDto? LastBank);

public sealed record PayoutBankDto(string BankName, string BankAccountNumber, string BankAccountName);

public sealed record EarningDto(Guid Id, EarningType Type, string OrderCode, Guid CourseId, string CourseTitle,
    decimal GrossAmount, decimal SharePercent, decimal Amount, DateTime CreatedAt, DateTime AvailableAt, bool IsAvailable);

public sealed record PayoutDto(Guid Id, decimal Amount, PayoutStatus Status, string BankName, string BankAccountNumber, string BankAccountName,
    string? Note, string? TransferReference, string? AdminNote, DateTime CreatedAt, DateTime? ProcessedAt);

public sealed record AdminPayoutDto(Guid Id, Guid InstructorId, string InstructorName, string InstructorEmail, decimal Amount, PayoutStatus Status,
    string BankName, string BankAccountNumber, string BankAccountName, string? Note, string? TransferReference, string? AdminNote,
    DateTime CreatedAt, DateTime? ProcessedAt, decimal InstructorWithdrawable);

public sealed record InstructorBalanceDto(Guid InstructorId, string InstructorName, string InstructorEmail,
    decimal GrossSales, decimal TotalEarned, decimal PendingAmount, decimal PaidOutAmount, decimal InProcessAmount, decimal WithdrawableAmount, int SalesCount);

public sealed record RevenueOverviewDto(decimal GrossSales, decimal InstructorShare, decimal PlatformShare,
    decimal PaidOut, decimal InProcess, decimal OwedToInstructors, int OpenPayoutCount, decimal SharePercent,
    IReadOnlyList<InstructorBalanceDto> Instructors);

public sealed record RequestPayoutRequest(decimal Amount, string BankName, string BankAccountNumber, string BankAccountName, string? Note);
public sealed record MarkPayoutPaidRequest(string TransferReference, string? AdminNote);
public sealed record RejectPayoutRequest(string AdminNote);

public sealed class EarningQuery : PagedQuery
{
    public EarningType? Type { get; set; }
}

public sealed class AdminPayoutQuery : PagedQuery
{
    public PayoutStatus? Status { get; set; }
    public string? Keyword { get; set; }
}

public sealed class RequestPayoutRequestValidator : AbstractValidator<RequestPayoutRequest>
{
    public RequestPayoutRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Số tiền rút phải lớn hơn 0.");
        RuleFor(x => x.BankName).NotEmpty().WithMessage("Nhập tên ngân hàng.").MaximumLength(100);
        RuleFor(x => x.BankAccountNumber).NotEmpty().WithMessage("Nhập số tài khoản.")
            .Matches("^[0-9]{6,20}$").WithMessage("Số tài khoản chỉ gồm 6–20 chữ số.");
        RuleFor(x => x.BankAccountName).NotEmpty().WithMessage("Nhập tên chủ tài khoản.").MaximumLength(100);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

/// <summary>Tính các dòng sổ thu nhập khi một đơn được thanh toán (dùng chung cho dịch vụ và dữ liệu mẫu).</summary>
public static class EarningLedger
{
    /// <summary>
    /// Mỗi khóa học có phí trong đơn sinh một dòng bán. FinalPrice của OrderItem đã trừ phần mã giảm giá và điểm thưởng
    /// được phân bổ (PricingService bảo đảm tổng FinalPrice = TotalAmount), nên đó chính là số tiền học viên thực trả
    /// cho khóa học. Khóa học 0đ không ghi sổ.
    /// </summary>
    public static List<InstructorEarning> BuildSales(Order order, DateTime paidAt, decimal sharePercent, int holdDays) =>
        order.Items.Where(i => i.FinalPrice > 0).Select(item => new InstructorEarning
        {
            InstructorId = item.InstructorId,
            OrderId = order.Id,
            OrderItemId = item.Id,
            CourseId = item.CourseId,
            OrderCode = order.OrderCode,
            CourseTitle = item.CourseTitle,
            Type = EarningType.Sale,
            GrossAmount = item.FinalPrice,
            SharePercent = sharePercent,
            Amount = Math.Round(item.FinalPrice * sharePercent / 100m, 0, MidpointRounding.AwayFromZero),
            AvailableAt = paidAt.AddDays(holdDays),
            CreatedAt = paidAt,
        }).ToList();
}

public interface IRevenueService
{
    /// <summary>Ghi sổ thu nhập khi đơn được thanh toán (chưa SaveChanges). Idempotent.</summary>
    Task StageSaleEarningsAsync(Order order, DateTime paidAt, CancellationToken ct = default);
    /// <summary>Ghi dòng hoàn (âm) cho mọi dòng bán của đơn bị hoàn tiền (chưa SaveChanges). Idempotent.</summary>
    Task StageRefundReversalAsync(Order order, DateTime now, CancellationToken ct = default);

    Task<EarningsSummaryDto> GetSummaryAsync(Guid instructorId, CancellationToken ct = default);
    Task<PagedResult<EarningDto>> ListEarningsAsync(Guid instructorId, EarningQuery query, CancellationToken ct = default);
    Task<PagedResult<PayoutDto>> ListMyPayoutsAsync(Guid instructorId, PagedQuery query, CancellationToken ct = default);
    Task<Result<PayoutDto>> RequestPayoutAsync(Guid instructorId, RequestPayoutRequest request, CancellationToken ct = default);
    Task<Result> CancelPayoutAsync(Guid instructorId, Guid payoutId, CancellationToken ct = default);

    Task<RevenueOverviewDto> GetOverviewAsync(CancellationToken ct = default);
    Task<PagedResult<AdminPayoutDto>> ListPayoutsAsync(AdminPayoutQuery query, CancellationToken ct = default);
    Task<Result> MarkPayoutPaidAsync(Guid adminId, Guid payoutId, MarkPayoutPaidRequest request, CancellationToken ct = default);
    Task<Result> RejectPayoutAsync(Guid adminId, Guid payoutId, RejectPayoutRequest request, CancellationToken ct = default);
    Task<byte[]> ExportPayoutsCsvAsync(AdminPayoutQuery query, CancellationToken ct = default);
}

public sealed class RevenueService(
    IAppDbContext db,
    IDateTimeProvider clock,
    INotificationService notifications,
    IOptions<RevenueOptions> options,
    ILogger<RevenueService> logger) : IRevenueService
{
    private RevenueOptions O => options.Value;

    public async Task StageSaleEarningsAsync(Order order, DateTime paidAt, CancellationToken ct = default)
    {
        if (await db.InstructorEarnings.AnyAsync(e => e.OrderId == order.Id && e.Type == EarningType.Sale, ct)) return;
        db.InstructorEarnings.AddRange(EarningLedger.BuildSales(order, paidAt, O.InstructorSharePercent, O.HoldDays));
    }

    public async Task StageRefundReversalAsync(Order order, DateTime now, CancellationToken ct = default)
    {
        var rows = await db.InstructorEarnings.Where(e => e.OrderId == order.Id).ToListAsync(ct);
        var reversed = rows.Where(r => r.Type == EarningType.Refund).Select(r => r.OrderItemId).ToHashSet();
        foreach (var sale in rows.Where(r => r.Type == EarningType.Sale && !reversed.Contains(r.OrderItemId)))
        {
            db.InstructorEarnings.Add(new InstructorEarning
            {
                InstructorId = sale.InstructorId,
                OrderId = sale.OrderId,
                OrderItemId = sale.OrderItemId,
                CourseId = sale.CourseId,
                OrderCode = sale.OrderCode,
                CourseTitle = sale.CourseTitle,
                Type = EarningType.Refund,
                GrossAmount = -sale.GrossAmount,
                SharePercent = sale.SharePercent,
                Amount = -sale.Amount,
                // Khoản bán chưa đến hạn rút thì khoản hoàn cũng đến hạn cùng lúc: số dư có thể rút không bị âm tạm thời
                AvailableAt = sale.AvailableAt > now ? sale.AvailableAt : now,
                CreatedAt = now,
            });
            notifications.Add(sale.InstructorId, "Đơn hàng bị hoàn tiền",
                $"Đơn {sale.OrderCode} (khóa học \"{sale.CourseTitle}\") đã được hoàn tiền; thu nhập {sale.Amount:N0}đ được trừ lại.",
                NotificationType.Order, "/giang-day/doanh-thu");
        }
    }

    // ================= Giảng viên =================

    private sealed record Totals(decimal Total, decimal Available, decimal Gross, int Sales, int Refunds);

    private async Task<Totals> EarningTotalsAsync(Guid instructorId, DateTime now, CancellationToken ct)
    {
        var rows = await db.InstructorEarnings.AsNoTracking().Where(e => e.InstructorId == instructorId)
            .Select(e => new { e.Amount, e.GrossAmount, e.AvailableAt, e.Type }).ToListAsync(ct);
        return new Totals(rows.Sum(r => r.Amount), rows.Where(r => r.AvailableAt <= now).Sum(r => r.Amount), rows.Sum(r => r.GrossAmount),
            rows.Count(r => r.Type == EarningType.Sale), rows.Count(r => r.Type == EarningType.Refund));
    }

    private async Task<(decimal PaidOut, decimal InProcess)> PayoutTotalsAsync(Guid instructorId, Guid? excludeId, CancellationToken ct)
    {
        var rows = await db.InstructorPayouts.AsNoTracking()
            .Where(p => p.InstructorId == instructorId && (p.Status == PayoutStatus.Paid || p.Status == PayoutStatus.Requested) && p.Id != excludeId)
            .Select(p => new { p.Amount, p.Status }).ToListAsync(ct);
        return (rows.Where(r => r.Status == PayoutStatus.Paid).Sum(r => r.Amount), rows.Where(r => r.Status == PayoutStatus.Requested).Sum(r => r.Amount));
    }

    private static decimal Withdrawable(decimal available, decimal paidOut, decimal inProcess) => Math.Max(0, available - paidOut - inProcess);

    public async Task<EarningsSummaryDto> GetSummaryAsync(Guid instructorId, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var t = await EarningTotalsAsync(instructorId, now, ct);
        var (paidOut, inProcess) = await PayoutTotalsAsync(instructorId, null, ct);
        var lastBank = await db.InstructorPayouts.AsNoTracking().Where(p => p.InstructorId == instructorId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PayoutBankDto(p.BankName, p.BankAccountNumber, p.BankAccountName)).FirstOrDefaultAsync(ct);

        return new EarningsSummaryDto(O.InstructorSharePercent, O.HoldDays, O.MinPayoutAmount,
            t.Total, t.Total - t.Available, t.Available, paidOut, inProcess, Withdrawable(t.Available, paidOut, inProcess),
            t.Sales, t.Refunds, inProcess > 0, lastBank);
    }

    public Task<PagedResult<EarningDto>> ListEarningsAsync(Guid instructorId, EarningQuery q, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        return db.InstructorEarnings.AsNoTracking()
            .Where(e => e.InstructorId == instructorId && (q.Type == null || e.Type == q.Type))
            .OrderByDescending(e => e.CreatedAt).ThenBy(e => e.Type)
            .Select(e => new EarningDto(e.Id, e.Type, e.OrderCode, e.CourseId, e.CourseTitle, e.GrossAmount, e.SharePercent, e.Amount,
                e.CreatedAt, e.AvailableAt, e.AvailableAt <= now))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);
    }

    public Task<PagedResult<PayoutDto>> ListMyPayoutsAsync(Guid instructorId, PagedQuery q, CancellationToken ct = default) =>
        db.InstructorPayouts.AsNoTracking().Where(p => p.InstructorId == instructorId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PayoutDto(p.Id, p.Amount, p.Status, p.BankName, p.BankAccountNumber, p.BankAccountName,
                p.Note, p.TransferReference, p.AdminNote, p.CreatedAt, p.ProcessedAt))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);

    public async Task<Result<PayoutDto>> RequestPayoutAsync(Guid instructorId, RequestPayoutRequest r, CancellationToken ct = default)
    {
        var amount = Math.Round(r.Amount, 0, MidpointRounding.ToZero);
        if (amount < O.MinPayoutAmount) return Error.Validation($"Số tiền rút tối thiểu là {O.MinPayoutAmount:N0}đ.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await db.InstructorPayouts.AnyAsync(p => p.InstructorId == instructorId && p.Status == PayoutStatus.Requested, ct))
            return Error.Conflict("Bạn đang có một yêu cầu rút tiền chờ xử lý.");

        var now = clock.UtcNow;
        var t = await EarningTotalsAsync(instructorId, now, ct);
        var (paidOut, inProcess) = await PayoutTotalsAsync(instructorId, null, ct);
        var withdrawable = Withdrawable(t.Available, paidOut, inProcess);
        if (amount > withdrawable)
            return Error.Validation($"Số tiền rút vượt quá số dư có thể rút ({withdrawable:N0}đ).");

        var payout = new InstructorPayout
        {
            InstructorId = instructorId,
            Amount = amount,
            Status = PayoutStatus.Requested,
            BankName = r.BankName.Trim(),
            BankAccountNumber = r.BankAccountNumber.Trim(),
            BankAccountName = r.BankAccountName.Trim().ToUpperInvariant(),
            Note = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim(),
        };
        db.InstructorPayouts.Add(payout);
        var name = await db.Users.Where(u => u.Id == instructorId).Select(u => u.FullName).FirstAsync(ct);
        await notifications.AddToRoleAsync(Roles.Admin, "Yêu cầu rút tiền mới",
            $"Giảng viên {name} yêu cầu rút {amount:N0}đ.", NotificationType.System, "/quan-tri/chi-tra", ct);
        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogInformation(ex, "Yêu cầu rút tiền trùng của giảng viên {InstructorId}", instructorId);
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return Error.Conflict("Bạn đang có một yêu cầu rút tiền chờ xử lý.");
        }

        logger.LogInformation("Giảng viên {InstructorId} yêu cầu rút {Amount}", instructorId, amount);
        return new PayoutDto(payout.Id, payout.Amount, payout.Status, payout.BankName, payout.BankAccountNumber, payout.BankAccountName,
            payout.Note, null, null, payout.CreatedAt, null);
    }

    public async Task<Result> CancelPayoutAsync(Guid instructorId, Guid payoutId, CancellationToken ct = default)
    {
        var p = await db.InstructorPayouts.FirstOrDefaultAsync(x => x.Id == payoutId && x.InstructorId == instructorId, ct);
        if (p is null) return Result.Failure(Error.NotFound("Không tìm thấy yêu cầu rút tiền."));
        if (p.Status != PayoutStatus.Requested) return Result.Failure(Error.Conflict("Chỉ hủy được yêu cầu đang chờ xử lý."));
        p.Status = PayoutStatus.Cancelled;
        p.ProcessedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ================= Quản trị =================

    public async Task<RevenueOverviewDto> GetOverviewAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var earnings = await db.InstructorEarnings.AsNoTracking()
            .Select(e => new { e.InstructorId, e.Amount, e.GrossAmount, e.AvailableAt, e.Type }).ToListAsync(ct);
        var payouts = await db.InstructorPayouts.AsNoTracking()
            .Where(p => p.Status == PayoutStatus.Paid || p.Status == PayoutStatus.Requested)
            .Select(p => new { p.InstructorId, p.Amount, p.Status }).ToListAsync(ct);
        var ids = earnings.Select(e => e.InstructorId).Union(payouts.Select(p => p.InstructorId)).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, Email = u.Email ?? "" }).ToDictionaryAsync(u => u.Id, ct);

        var list = ids.Select(id =>
        {
            var e = earnings.Where(x => x.InstructorId == id).ToList();
            var p = payouts.Where(x => x.InstructorId == id).ToList();
            var total = e.Sum(x => x.Amount);
            var available = e.Where(x => x.AvailableAt <= now).Sum(x => x.Amount);
            var paid = p.Where(x => x.Status == PayoutStatus.Paid).Sum(x => x.Amount);
            var inProcess = p.Where(x => x.Status == PayoutStatus.Requested).Sum(x => x.Amount);
            var u = users.GetValueOrDefault(id);
            return new InstructorBalanceDto(id, u?.FullName ?? "(đã xóa)", u?.Email ?? "", e.Sum(x => x.GrossAmount), total, total - available,
                paid, inProcess, Withdrawable(available, paid, inProcess), e.Count(x => x.Type == EarningType.Sale));
        }).OrderByDescending(x => x.TotalEarned).ToList();

        var gross = earnings.Sum(e => e.GrossAmount);
        var share = earnings.Sum(e => e.Amount);
        var paidOut = payouts.Where(p => p.Status == PayoutStatus.Paid).Sum(p => p.Amount);
        var inProc = payouts.Where(p => p.Status == PayoutStatus.Requested).Sum(p => p.Amount);
        return new RevenueOverviewDto(gross, share, gross - share, paidOut, inProc, Math.Max(0, share - paidOut),
            payouts.Count(p => p.Status == PayoutStatus.Requested), O.InstructorSharePercent, list);
    }

    private IQueryable<InstructorPayout> FilterPayouts(AdminPayoutQuery q)
    {
        var query = db.InstructorPayouts.AsNoTracking().AsQueryable();
        if (q.Status is { } s) query = query.Where(p => p.Status == s);
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(p => p.Instructor.FullName.Contains(kw) || p.Instructor.Email!.Contains(kw) || p.BankAccountNumber.Contains(kw));
        }
        return query;
    }

    public async Task<PagedResult<AdminPayoutDto>> ListPayoutsAsync(AdminPayoutQuery q, CancellationToken ct = default)
    {
        var page = await FilterPayouts(q)
            .OrderBy(p => p.Status == PayoutStatus.Requested ? 0 : 1).ThenByDescending(p => p.CreatedAt)
            .Select(p => new AdminPayoutDto(p.Id, p.InstructorId, p.Instructor.FullName, p.Instructor.Email!, p.Amount, p.Status,
                p.BankName, p.BankAccountNumber, p.BankAccountName, p.Note, p.TransferReference, p.AdminNote, p.CreatedAt, p.ProcessedAt, 0))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);

        // Số dư hiện tại của giảng viên (không tính chính yêu cầu đang chờ) để Admin đối chiếu trước khi chi
        var now = clock.UtcNow;
        var items = new List<AdminPayoutDto>(page.Items.Count);
        foreach (var p in page.Items)
        {
            if (p.Status != PayoutStatus.Requested) { items.Add(p); continue; }
            var t = await EarningTotalsAsync(p.InstructorId, now, ct);
            var (paidOut, inProcess) = await PayoutTotalsAsync(p.InstructorId, p.Id, ct);
            items.Add(p with { InstructorWithdrawable = Withdrawable(t.Available, paidOut, inProcess) });
        }
        return page with { Items = items };
    }

    public async Task<Result> MarkPayoutPaidAsync(Guid adminId, Guid payoutId, MarkPayoutPaidRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.TransferReference) || r.TransferReference.Trim().Length < 3)
            return Result.Failure(Error.Validation("Nhập mã giao dịch chuyển khoản (ít nhất 3 ký tự)."));

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var p = await db.InstructorPayouts.FirstOrDefaultAsync(x => x.Id == payoutId, ct);
        if (p is null) return Result.Failure(Error.NotFound("Không tìm thấy yêu cầu rút tiền."));
        if (p.Status != PayoutStatus.Requested) return Result.Failure(Error.Conflict("Yêu cầu này đã được xử lý."));

        // Đơn có thể bị hoàn tiền sau khi giảng viên gửi yêu cầu: không chi quá số dư hiện có
        var now = clock.UtcNow;
        var t = await EarningTotalsAsync(p.InstructorId, now, ct);
        var (paidOut, inProcess) = await PayoutTotalsAsync(p.InstructorId, p.Id, ct);
        var withdrawable = Withdrawable(t.Available, paidOut, inProcess);
        if (p.Amount > withdrawable)
            return Result.Failure(Error.Conflict($"Số dư hiện tại của giảng viên chỉ còn {withdrawable:N0}đ (có đơn vừa hoàn tiền). Hãy từ chối yêu cầu này."));

        p.Status = PayoutStatus.Paid;
        p.TransferReference = r.TransferReference.Trim();
        p.AdminNote = string.IsNullOrWhiteSpace(r.AdminNote) ? null : r.AdminNote.Trim();
        p.ProcessedAt = now;
        p.ProcessedById = adminId;
        notifications.Add(p.InstructorId, "Đã chuyển tiền",
            $"EduViet đã chuyển {p.Amount:N0}đ vào tài khoản {p.BankName} {Mask(p.BankAccountNumber)}. Mã giao dịch: {p.TransferReference}.",
            NotificationType.System, "/giang-day/doanh-thu");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogInformation("Admin {AdminId} xác nhận đã chi {Amount} cho giảng viên {InstructorId}", adminId, p.Amount, p.InstructorId);
        return Result.Success();
    }

    public async Task<Result> RejectPayoutAsync(Guid adminId, Guid payoutId, RejectPayoutRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.AdminNote) || r.AdminNote.Trim().Length < 5)
            return Result.Failure(Error.Validation("Bắt buộc ghi lý do từ chối (ít nhất 5 ký tự)."));

        var p = await db.InstructorPayouts.FirstOrDefaultAsync(x => x.Id == payoutId, ct);
        if (p is null) return Result.Failure(Error.NotFound("Không tìm thấy yêu cầu rút tiền."));
        if (p.Status != PayoutStatus.Requested) return Result.Failure(Error.Conflict("Yêu cầu này đã được xử lý."));

        p.Status = PayoutStatus.Rejected;
        p.AdminNote = r.AdminNote.Trim();
        p.ProcessedAt = clock.UtcNow;
        p.ProcessedById = adminId;
        notifications.Add(p.InstructorId, "Yêu cầu rút tiền bị từ chối",
            $"Yêu cầu rút {p.Amount:N0}đ bị từ chối: {p.AdminNote}", NotificationType.System, "/giang-day/doanh-thu");
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<byte[]> ExportPayoutsCsvAsync(AdminPayoutQuery q, CancellationToken ct = default)
    {
        var rows = await FilterPayouts(q).OrderByDescending(p => p.CreatedAt).Take(50_000)
            .Select(p => new { p.CreatedAt, p.ProcessedAt, p.Instructor.FullName, p.Instructor.Email, p.Amount, p.Status,
                p.BankName, p.BankAccountNumber, p.BankAccountName, p.TransferReference, p.Note, p.AdminNote })
            .ToListAsync(ct);
        var sb = new StringBuilder();
        sb.AppendLine("Ngày yêu cầu (UTC+7),Ngày xử lý,Giảng viên,Email,Số tiền,Trạng thái,Ngân hàng,Số tài khoản,Chủ tài khoản,Mã chuyển khoản,Ghi chú giảng viên,Ghi chú Admin");
        foreach (var p in rows)
        {
            sb.Append(CsvExport.Time(p.CreatedAt)).Append(',').Append(CsvExport.Time(p.ProcessedAt)).Append(',')
              .Append(CsvExport.Field(p.FullName)).Append(',').Append(CsvExport.Field(p.Email)).Append(',')
              .Append(CsvExport.Money(p.Amount)).Append(',').Append(StatusText(p.Status)).Append(',')
              .Append(CsvExport.Field(p.BankName)).Append(',').Append(CsvExport.Field(p.BankAccountNumber)).Append(',')
              .Append(CsvExport.Field(p.BankAccountName)).Append(',').Append(CsvExport.Field(p.TransferReference)).Append(',')
              .Append(CsvExport.Field(p.Note)).Append(',').Append(CsvExport.Field(p.AdminNote)).AppendLine();
        }
        return CsvExport.ToBytes(sb);
    }

    private static string StatusText(PayoutStatus s) => s switch
    {
        PayoutStatus.Requested => "Chờ xử lý",
        PayoutStatus.Paid => "Đã chi",
        PayoutStatus.Rejected => "Từ chối",
        _ => "Đã hủy",
    };

    private static string Mask(string account) => account.Length <= 4 ? account : new string('*', account.Length - 4) + account[^4..];
}
