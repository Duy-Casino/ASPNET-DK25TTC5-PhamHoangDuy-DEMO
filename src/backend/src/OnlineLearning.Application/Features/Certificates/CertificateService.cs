using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnlineLearning.Application.Common.Helpers;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Common.Options;
using OnlineLearning.Application.Features.Notifications;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Certificates;

/// <summary>Dữ liệu in lên chứng chỉ PDF.</summary>
public sealed record CertificateDocument(
    string Code, string StudentName, string CourseTitle, string InstructorName, DateTime IssuedAt,
    decimal? FinalScore, int TotalLessons, int TotalDurationSeconds, string VerifyUrl, bool IsRevoked);

public interface ICertificatePdfGenerator
{
    byte[] Generate(CertificateDocument document);
}

public sealed record CertificateVerifyDto(
    string CertificateCode, string StudentName, string CourseTitle, string CourseSlug, string InstructorName,
    DateTime IssuedAt, decimal? FinalScore, int TotalLessons, int TotalDurationSeconds, bool IsRevoked, DateTime? RevokedAt, string? RevokedReason);

public sealed record AdminCertificateDto(
    Guid Id, string CertificateCode, string StudentName, string StudentEmail, string CourseTitle, DateTime IssuedAt,
    decimal? FinalScore, bool IsRevoked, DateTime? RevokedAt, string? RevokedReason);

public sealed class AdminCertificateQuery : PagedQuery
{
    public string? Keyword { get; set; }
    public bool? IsRevoked { get; set; }
}

public sealed record RevokeCertificateRequest(string Reason);

public sealed class RevokeCertificateRequestValidator : AbstractValidator<RevokeCertificateRequest>
{
    public RevokeCertificateRequestValidator() =>
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Vui lòng nhập lý do thu hồi.").MaximumLength(500);
}

public interface ICertificateService
{
    /// <summary>
    /// Cấp chứng chỉ khi lượt ghi danh đã "Completed" (100% bài bắt buộc + đạt mọi bài kiểm tra bắt buộc).
    /// Idempotent: mỗi lượt ghi danh chỉ có một chứng chỉ; chứng chỉ đã thu hồi không tự cấp lại.
    /// </summary>
    Task<Certificate?> IssueIfEligibleAsync(Guid enrollmentId, CancellationToken ct = default);
    Task<Result<(byte[] Pdf, string FileName)>> GetPdfAsync(Guid certificateId, Guid userId, bool isAdmin, CancellationToken ct = default);
    Task<Result<(byte[] Pdf, string FileName)>> GetPdfByCodeAsync(string code, CancellationToken ct = default);
    Task<Result<CertificateVerifyDto>> VerifyAsync(string code, CancellationToken ct = default);
    Task<PagedResult<AdminCertificateDto>> ListAsync(AdminCertificateQuery query, CancellationToken ct = default);
    Task<Result> RevokeAsync(Guid id, string reason, CancellationToken ct = default);
    Task<Result> RestoreAsync(Guid id, CancellationToken ct = default);
    /// <summary>Cấp chứng chỉ cho mọi lượt ghi danh đã hoàn thành nhưng chưa có (sau khi nâng cấp hệ thống).</summary>
    Task<int> BackfillAsync(CancellationToken ct = default);
}

public sealed class CertificateService(
    IAppDbContext db,
    ICertificatePdfGenerator pdf,
    INotificationService notifications,
    IDateTimeProvider clock,
    IOptions<AppOptions> app,
    ILogger<CertificateService> logger) : ICertificateService
{
    private static readonly Error NotFound = Error.NotFound("Không tìm thấy chứng chỉ.");

    public async Task<Certificate?> IssueIfEligibleAsync(Guid enrollmentId, CancellationToken ct = default)
    {
        var existing = await db.Certificates.FirstOrDefaultAsync(c => c.EnrollmentId == enrollmentId, ct);
        if (existing is not null) return existing;

        var enrollment = await db.Enrollments
            .Include(e => e.User)
            .Include(e => e.Course).ThenInclude(c => c.Instructor)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId, ct);
        if (enrollment is null || enrollment.Status != EnrollmentStatus.Completed) return null;

        // Điểm in trên chứng chỉ: điểm cao nhất của bài thi cuối khóa; không có thì trung bình điểm cao nhất các bài bắt buộc
        var quizzes = await db.Quizzes.Where(q => q.CourseId == enrollment.CourseId && q.IsRequired)
            .Select(q => new
            {
                q.IsFinalExam,
                Best = q.Attempts.Where(a => a.UserId == enrollment.UserId && a.SubmittedAt != null).Max(a => (decimal?)a.ScorePercent),
            })
            .ToListAsync(ct);
        var finalExam = quizzes.Where(q => q.IsFinalExam && q.Best.HasValue).Select(q => q.Best).FirstOrDefault();
        var scored = quizzes.Where(q => q.Best.HasValue).Select(q => q.Best!.Value).ToList();
        decimal? finalScore = finalExam ?? (scored.Count > 0 ? Math.Round(scored.Average(), 2) : null);

        var now = clock.UtcNow;
        var certificate = new Certificate
        {
            CertificateCode = await UniqueCodeAsync(now, ct),
            UserId = enrollment.UserId,
            CourseId = enrollment.CourseId,
            EnrollmentId = enrollment.Id,
            StudentName = enrollment.User.FullName,
            CourseTitle = enrollment.Course.Title,
            InstructorName = enrollment.Course.Instructor.FullName,
            FinalScore = finalScore,
            IssuedAt = now,
        };
        db.Certificates.Add(certificate);
        notifications.Add(enrollment.UserId, "Bạn đã nhận chứng chỉ",
            $"Chúc mừng bạn hoàn thành khóa \"{enrollment.Course.Title}\". Mã chứng chỉ: {certificate.CertificateCode}.",
            NotificationType.Certificate, "/hoc-vien/chung-chi");

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Hai yêu cầu hoàn thành cùng lúc: unique index EnrollmentId chặn bản thứ hai
            logger.LogWarning(ex, "Chứng chỉ cho lượt ghi danh {EnrollmentId} đã được cấp bởi yêu cầu khác", enrollmentId);
            db.Certificates.Remove(certificate);
            return await db.Certificates.AsNoTracking().FirstOrDefaultAsync(c => c.EnrollmentId == enrollmentId, ct);
        }

        logger.LogInformation("Cấp chứng chỉ {Code} cho {User} — khóa {Course}", certificate.CertificateCode, enrollment.UserId, enrollment.CourseId);
        return certificate;
    }

    public async Task<Result<(byte[] Pdf, string FileName)>> GetPdfAsync(Guid certificateId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var c = await db.Certificates.AsNoTracking().Include(x => x.Course).FirstOrDefaultAsync(x => x.Id == certificateId, ct);
        if (c is null || (!isAdmin && c.UserId != userId)) return NotFound;
        return Render(c);
    }

    public async Task<Result<(byte[] Pdf, string FileName)>> GetPdfByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalized = Normalize(code);
        var c = await db.Certificates.AsNoTracking().Include(x => x.Course).FirstOrDefaultAsync(x => x.CertificateCode == normalized, ct);
        if (c is null || c.IsRevoked) return NotFound;
        return Render(c);
    }

    public async Task<Result<CertificateVerifyDto>> VerifyAsync(string code, CancellationToken ct = default)
    {
        var normalized = Normalize(code);
        if (normalized.Length is < 10 or > 30) return Error.Validation("Mã chứng chỉ không hợp lệ.");

        var c = await db.Certificates.AsNoTracking().Include(x => x.Course)
            .FirstOrDefaultAsync(x => x.CertificateCode == normalized, ct);
        if (c is null) return NotFound;

        return new CertificateVerifyDto(c.CertificateCode, c.StudentName, c.CourseTitle, c.Course.Slug, c.InstructorName, c.IssuedAt,
            c.FinalScore, c.Course.TotalLessons, c.Course.TotalDurationSeconds, c.IsRevoked, c.RevokedAt, c.RevokedReason);
    }

    public Task<PagedResult<AdminCertificateDto>> ListAsync(AdminCertificateQuery q, CancellationToken ct = default)
    {
        var query = db.Certificates.AsNoTracking().AsQueryable();
        if (q.IsRevoked is { } revoked) query = query.Where(c => c.IsRevoked == revoked);
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(c => c.CertificateCode.Contains(kw.ToUpper()) || c.StudentName.Contains(kw) || c.CourseTitle.Contains(kw) || c.User.Email!.Contains(kw));
        }
        return query.OrderByDescending(c => c.IssuedAt)
            .Select(c => new AdminCertificateDto(c.Id, c.CertificateCode, c.StudentName, c.User.Email!, c.CourseTitle, c.IssuedAt,
                c.FinalScore, c.IsRevoked, c.RevokedAt, c.RevokedReason))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);
    }

    public async Task<Result> RevokeAsync(Guid id, string reason, CancellationToken ct = default)
    {
        var c = await db.Certificates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result.Failure(NotFound);
        if (c.IsRevoked) return Result.Failure(Error.Conflict("Chứng chỉ đã bị thu hồi."));

        c.IsRevoked = true;
        c.RevokedAt = clock.UtcNow;
        c.RevokedReason = reason.Trim();
        notifications.Add(c.UserId, "Chứng chỉ bị thu hồi", $"Chứng chỉ {c.CertificateCode} đã bị thu hồi: {c.RevokedReason}",
            NotificationType.Certificate, "/hoc-vien/chung-chi");
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RestoreAsync(Guid id, CancellationToken ct = default)
    {
        var c = await db.Certificates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result.Failure(NotFound);
        if (!c.IsRevoked) return Result.Failure(Error.Conflict("Chứng chỉ đang có hiệu lực."));
        c.IsRevoked = false;
        c.RevokedAt = null;
        c.RevokedReason = null;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<int> BackfillAsync(CancellationToken ct = default)
    {
        var ids = await db.Enrollments
            .Where(e => e.Status == EnrollmentStatus.Completed && e.Certificate == null)
            .Select(e => e.Id).ToListAsync(ct);
        var issued = 0;
        foreach (var id in ids)
            if (await IssueIfEligibleAsync(id, ct) is not null) issued++;
        return issued;
    }

    // ---------------- helpers ----------------

    private (byte[] Pdf, string FileName) Render(Certificate c)
    {
        var doc = new CertificateDocument(c.CertificateCode, c.StudentName, c.CourseTitle, c.InstructorName, c.IssuedAt, c.FinalScore,
            c.Course.TotalLessons, c.Course.TotalDurationSeconds, VerifyUrl(c.CertificateCode), c.IsRevoked);
        return (pdf.Generate(doc), $"chung-chi-{c.CertificateCode}.pdf");
    }

    private string VerifyUrl(string code) => $"{app.Value.ClientUrl.TrimEnd('/')}/xac-minh/{code}";

    private static string Normalize(string code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    private async Task<string> UniqueCodeAsync(DateTime now, CancellationToken ct)
    {
        while (true)
        {
            var code = CodeGenerator.CertificateCode(now);
            if (!await db.Certificates.AnyAsync(c => c.CertificateCode == code, ct)) return code;
        }
    }
}
