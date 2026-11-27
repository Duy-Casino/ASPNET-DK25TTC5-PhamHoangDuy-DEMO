using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Notifications;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Learning;

// =====================================================================
// DTO
// =====================================================================
/// <summary>Tình trạng học của một lượt ghi danh (để Admin lọc và xử lý).</summary>
public static class LearningStates
{
    public const string NotStarted = "not-started";   // chưa hoàn thành bài nào, chưa làm bài kiểm tra
    public const string InProgress = "in-progress";
    public const string Completed = "completed";
    public const string Stuck = "stuck";               // chưa đạt bài kiểm tra bắt buộc và đã hết lượt
    public const string Inactive = "inactive";         // đang học dở nhưng không vào học quá 14 ngày
    public const string NoCertificate = "no-certificate"; // đã đủ điều kiện nhưng chưa có chứng chỉ
    public const string Revoked = "revoked";
}

public sealed record LearningOverviewDto(int Total, int NotStarted, int InProgress, int Completed, int Stuck, int Inactive, int NoCertificate,
    int Revoked, decimal CompletionRate, int OpenSupportRequests, int FailedAttempts30Days);

public sealed record AdminEnrollmentDto(Guid EnrollmentId, Guid UserId, string FullName, string Email, Guid CourseId, string CourseTitle, string CourseSlug,
    decimal ProgressPercent, int CompletedLessons, int RequiredLessons, EnrollmentStatus Status, string State,
    decimal? FinalBestScore, bool FinalPassed, int FinalAttemptsUsed, int? FinalMaxAttempts,
    DateTime EnrolledAt, DateTime? LastAccessedAt, bool HasCertificate, string Source);

public sealed record AdminLessonProgressDto(Guid LessonId, string SectionTitle, string Title, bool IsRequired, bool IsCompleted, DateTime? CompletedAt, int LastPositionSeconds);
public sealed record AdminAttemptDto(Guid Id, int AttemptNumber, DateTime StartedAt, DateTime? SubmittedAt, decimal ScorePercent, bool IsPassed);
public sealed record AdminQuizProgressDto(Guid QuizId, string Title, bool IsFinalExam, bool IsRequired, decimal PassingScore, int? BaseMaxAttempts,
    int ExtraAttempts, int AttemptsUsed, decimal? BestScore, bool IsPassed, IReadOnlyList<AdminAttemptDto> Attempts);
public sealed record LearningEventDto(DateTime At, string Kind, string Title, string? Detail);
public sealed record SupportRequestDto(Guid Id, Guid UserId, string UserName, string UserEmail, Guid CourseId, string CourseTitle, Guid? QuizId, Guid? LessonId,
    SupportRequestType Type, string Message, SupportRequestStatus Status, string? Response, DateTime CreatedAt, DateTime? ResolvedAt, Guid? EnrollmentId);

public sealed record AdminEnrollmentDetailDto(AdminEnrollmentDto Summary, IReadOnlyList<AdminLessonProgressDto> Lessons,
    IReadOnlyList<AdminQuizProgressDto> Quizzes, string? CertificateCode, IReadOnlyList<LearningEventDto> Timeline,
    IReadOnlyList<SupportRequestDto> SupportRequests);

public sealed class AdminEnrollmentQuery : PagedQuery
{
    public string? Keyword { get; set; }
    public Guid? CourseId { get; set; }
    /// <summary>Một trong <see cref="LearningStates"/>.</summary>
    public string? State { get; set; }
    /// <summary>Chỉ lượt ghi danh thuộc khóa của giảng viên này; API giảng viên luôn ghi đè bằng người đang đăng nhập.</summary>
    public Guid? InstructorId { get; set; }
}

// ---------- Giảng viên xem học viên cần hỗ trợ trong khóa của mình ----------
public sealed record StuckQuizDto(Guid QuizId, string Title, int AttemptsUsed, int MaxAttempts);
public sealed record InstructorLearnerDto(Guid EnrollmentId, Guid UserId, string FullName, string Email, string? AvatarUrl, Guid CourseId, string CourseTitle, string CourseSlug,
    decimal ProgressPercent, int CompletedLessons, int RequiredLessons, string State, decimal? FinalBestScore, bool FinalPassed,
    DateTime EnrolledAt, DateTime? LastAccessedAt, StuckQuizDto? StuckQuiz);
public sealed record InstructorLearnersDto(IReadOnlyDictionary<string, int> Counts, PagedResult<InstructorLearnerDto> Page);
public sealed record InstructorGrantRequest(Guid QuizId, int ExtraAttempts, string Reason);
public sealed class InstructorGrantRequestValidator : AbstractValidator<InstructorGrantRequest>
{
    public InstructorGrantRequestValidator()
    {
        RuleFor(x => x.ExtraAttempts).InclusiveBetween(1, 3).WithMessage("Giảng viên cấp thêm từ 1 đến 3 lượt mỗi lần.");
        RuleFor(x => x.Reason).Must(r => r is not null && r.Trim().Length >= 5).WithMessage("Bắt buộc ghi lý do (ít nhất 5 ký tự).").MaximumLength(500);
    }
}

public sealed record ReasonRequest(string Reason);
public sealed record GrantAttemptsRequest(Guid QuizId, int ExtraAttempts, string Reason);
public sealed record SetLessonRequest(Guid LessonId, bool Completed, string Reason);
public sealed record ManualEnrollRequest(string Email, Guid CourseId, string Reason);
public sealed record CreateSupportRequest(Guid CourseId, SupportRequestType Type, string Message, Guid? QuizId, Guid? LessonId);
public sealed record RespondSupportRequest(SupportRequestStatus Status, string Response);

public sealed class SupportRequestQuery : PagedQuery
{
    public SupportRequestStatus? Status { get; set; }
    public SupportRequestType? Type { get; set; }
    public string? Keyword { get; set; }
}

public sealed class ReasonRequestValidator : AbstractValidator<ReasonRequest>
{
    public ReasonRequestValidator() => RuleFor(x => x.Reason).Must(r => r is { Length: >= 5 } && r.Trim().Length >= 5).WithMessage("Bắt buộc ghi lý do (ít nhất 5 ký tự).").MaximumLength(500);
}
public sealed class GrantAttemptsRequestValidator : AbstractValidator<GrantAttemptsRequest>
{
    public GrantAttemptsRequestValidator()
    {
        RuleFor(x => x.ExtraAttempts).InclusiveBetween(1, 5).WithMessage("Cấp thêm từ 1 đến 5 lượt.");
        RuleFor(x => x.Reason).Must(r => r is not null && r.Trim().Length >= 5).WithMessage("Bắt buộc ghi lý do (ít nhất 5 ký tự).").MaximumLength(500);
    }
}
public sealed class SetLessonRequestValidator : AbstractValidator<SetLessonRequest>
{
    public SetLessonRequestValidator() => RuleFor(x => x.Reason).Must(r => r is not null && r.Trim().Length >= 5).WithMessage("Bắt buộc ghi lý do (ít nhất 5 ký tự).").MaximumLength(500);
}
public sealed class ManualEnrollRequestValidator : AbstractValidator<ManualEnrollRequest>
{
    public ManualEnrollRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Email không hợp lệ.");
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Reason).Must(r => r is not null && r.Trim().Length >= 5).WithMessage("Bắt buộc ghi lý do (ít nhất 5 ký tự).").MaximumLength(500);
    }
}
public sealed class CreateSupportRequestValidator : AbstractValidator<CreateSupportRequest>
{
    public CreateSupportRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Message).Must(m => m is not null && m.Trim().Length >= 10).WithMessage("Mô tả sự cố ít nhất 10 ký tự.").MaximumLength(1000);
    }
}
public sealed class RespondSupportRequestValidator : AbstractValidator<RespondSupportRequest>
{
    public RespondSupportRequestValidator()
    {
        RuleFor(x => x.Status).Must(s => s is SupportRequestStatus.Resolved or SupportRequestStatus.Rejected).WithMessage("Chọn Đã xử lý hoặc Từ chối.");
        RuleFor(x => x.Response).Must(r => r is not null && r.Trim().Length >= 5).WithMessage("Nhập phản hồi cho học viên (ít nhất 5 ký tự).").MaximumLength(1000);
    }
}

// =====================================================================
// Quản lý học tập (Admin)
// =====================================================================
public interface ILearningAdminService
{
    Task<LearningOverviewDto> GetOverviewAsync(CancellationToken ct = default);
    Task<PagedResult<AdminEnrollmentDto>> ListAsync(AdminEnrollmentQuery query, CancellationToken ct = default);
    Task<Result<AdminEnrollmentDetailDto>> GetDetailAsync(Guid enrollmentId, CancellationToken ct = default);
    Task<byte[]> ExportCsvAsync(AdminEnrollmentQuery query, CancellationToken ct = default);

    Task<Result> GrantAttemptsAsync(Guid adminId, Guid enrollmentId, GrantAttemptsRequest request, CancellationToken ct = default);
    Task<InstructorLearnersDto> ListForInstructorAsync(Guid instructorId, AdminEnrollmentQuery query, CancellationToken ct = default);
    /// <summary>Giảng viên cấp thêm lượt cho học viên hết lượt trong khóa của chính mình.</summary>
    Task<Result> InstructorGrantAsync(Guid instructorId, Guid enrollmentId, InstructorGrantRequest request, CancellationToken ct = default);
    Task<Result> SetLessonAsync(Guid adminId, Guid enrollmentId, SetLessonRequest request, CancellationToken ct = default);
    Task<Result> ResetProgressAsync(Guid adminId, Guid enrollmentId, string reason, CancellationToken ct = default);
    /// <summary>Tính lại tiến độ; đủ điều kiện thì cấp chứng chỉ (dùng khi tiến độ hiển thị sai hoặc chưa nhận chứng chỉ).</summary>
    Task<Result<ProgressDto>> RecalculateAsync(Guid adminId, Guid enrollmentId, string reason, CancellationToken ct = default);
    Task<Result> SetAccessAsync(Guid adminId, Guid enrollmentId, bool active, string reason, CancellationToken ct = default);
    Task<Result<Guid>> ManualEnrollAsync(Guid adminId, ManualEnrollRequest request, CancellationToken ct = default);

    Task<Result<SupportRequestDto>> CreateRequestAsync(Guid userId, CreateSupportRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<SupportRequestDto>> ListMyRequestsAsync(Guid userId, Guid? courseId, CancellationToken ct = default);
    Task<PagedResult<SupportRequestDto>> ListRequestsAsync(SupportRequestQuery query, CancellationToken ct = default);
    Task<Result> RespondAsync(Guid adminId, Guid requestId, RespondSupportRequest request, CancellationToken ct = default);
}

public sealed class LearningAdminService(
    IAppDbContext db, IDateTimeProvider clock, ILearningService learning, INotificationService notifications) : ILearningAdminService
{
    private const int InactiveDays = 14;
    private static readonly Error NotFoundEnrollment = Error.NotFound("Không tìm thấy lượt ghi danh.");

    // ---------------------------------------------------------------- phân loại tình trạng
    private sealed record Row(Guid Id, Guid UserId, string FullName, string Email, Guid CourseId, string CourseTitle, string CourseSlug,
        decimal Progress, int Completed, int Required, EnrollmentStatus Status, DateTime EnrolledAt, DateTime? LastAccessedAt, bool HasCert, Guid? OrderId);

    private sealed record QuizStat(Guid QuizId, Guid CourseId, bool IsRequired, bool IsFinal, int? Max);

    private sealed record Classified(Row Row, string State, decimal? FinalBest, bool FinalPassed, int FinalUsed, int? FinalMax,
        Guid? StuckQuizId = null, int StuckUsed = 0, int StuckMax = 0);

    /// <summary>Lọc và sắp xếp trên Enrollment trước khi chiếu sang Row (EF không dịch được điều kiện trên record đã chiếu).</summary>
    private IQueryable<Row> Rows(AdminEnrollmentQuery? q, Guid? enrollmentId = null)
    {
        var query = db.Enrollments.AsNoTracking().AsQueryable();
        if (enrollmentId is { } eid) query = query.Where(e => e.Id == eid);
        if (q?.CourseId is { } cid) query = query.Where(e => e.CourseId == cid);
        if (q?.InstructorId is { } iid) query = query.Where(e => e.Course.InstructorId == iid);
        if (!string.IsNullOrWhiteSpace(q?.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(e => e.User.FullName.Contains(kw) || e.User.Email!.Contains(kw) || e.Course.Title.Contains(kw));
        }
        return query.OrderByDescending(e => e.LastAccessedAt ?? e.EnrolledAt).Select(e => new Row(e.Id, e.UserId, e.User.FullName, e.User.Email ?? "", e.CourseId, e.Course.Title, e.Course.Slug,
            e.ProgressPercent, e.LessonProgresses.Count(p => p.IsCompleted && p.Lesson.IsRequired), e.Course.Sections.SelectMany(s => s.Lessons).Count(l => l.IsRequired),
            e.Status, e.EnrolledAt, e.LastAccessedAt, e.Certificate != null && !e.Certificate.IsRevoked, e.OrderId));
    }

    private async Task<List<Classified>> ClassifyAsync(List<Row> rows, CancellationToken ct)
    {
        var courseIds = rows.Select(r => r.CourseId).Distinct().ToList();
        var userIds = rows.Select(r => r.UserId).Distinct().ToList();
        var quizzes = await db.Quizzes.AsNoTracking().Where(q => courseIds.Contains(q.CourseId) && q.IsRequired)
            .Select(q => new QuizStat(q.Id, q.CourseId, q.IsRequired, q.IsFinalExam, q.MaxAttempts)).ToListAsync(ct);
        var quizIds = quizzes.Select(q => q.QuizId).ToList();
        var attempts = await db.QuizAttempts.AsNoTracking().Where(a => quizIds.Contains(a.QuizId) && userIds.Contains(a.UserId))
            .GroupBy(a => new { a.QuizId, a.UserId })
            .Select(g => new { g.Key.QuizId, g.Key.UserId, Used = g.Count(), Best = g.Where(a => a.SubmittedAt != null).Max(a => (decimal?)a.ScorePercent), Passed = g.Any(a => a.IsPassed) })
            .ToListAsync(ct);
        var grants = await db.QuizAttemptGrants.AsNoTracking().Where(g => quizIds.Contains(g.QuizId) && userIds.Contains(g.UserId))
            .GroupBy(g => new { g.QuizId, g.UserId }).Select(g => new { g.Key.QuizId, g.Key.UserId, Extra = g.Sum(x => x.ExtraAttempts) }).ToListAsync(ct);
        var now = clock.UtcNow;

        return rows.Select(r =>
        {
            var courseQuizzes = quizzes.Where(q => q.CourseId == r.CourseId).ToList();
            bool stuck = false;
            Guid? stuckQuiz = null;
            int stuckUsed = 0, stuckMax = 0;
            foreach (var q in courseQuizzes)
            {
                var a = attempts.FirstOrDefault(x => x.QuizId == q.QuizId && x.UserId == r.UserId);
                var extra = grants.FirstOrDefault(x => x.QuizId == q.QuizId && x.UserId == r.UserId)?.Extra ?? 0;
                if (a is { Passed: false } && q.Max is { } m && a.Used >= m + extra)
                {
                    stuck = true;
                    if (stuckQuiz is null) (stuckQuiz, stuckUsed, stuckMax) = (q.QuizId, a.Used, m + extra);
                }
            }
            var final = courseQuizzes.FirstOrDefault(q => q.IsFinal) ?? courseQuizzes.FirstOrDefault();
            var fa = final is null ? null : attempts.FirstOrDefault(x => x.QuizId == final.QuizId && x.UserId == r.UserId);
            var fExtra = final is null ? 0 : grants.FirstOrDefault(x => x.QuizId == final.QuizId && x.UserId == r.UserId)?.Extra ?? 0;
            var anyAttempt = attempts.Any(x => x.UserId == r.UserId && courseQuizzes.Any(q => q.QuizId == x.QuizId));
            var allPassed = courseQuizzes.All(q => attempts.Any(x => x.QuizId == q.QuizId && x.UserId == r.UserId && x.Passed));

            var state = r.Status == EnrollmentStatus.Revoked ? LearningStates.Revoked
                : stuck ? LearningStates.Stuck
                : r.Progress >= 100 && allPassed && !r.HasCert ? LearningStates.NoCertificate
                : r.Status == EnrollmentStatus.Completed ? LearningStates.Completed
                : r.Completed == 0 && !anyAttempt ? LearningStates.NotStarted
                : (r.LastAccessedAt ?? r.EnrolledAt) < now.AddDays(-InactiveDays) ? LearningStates.Inactive
                : LearningStates.InProgress;
            return new Classified(r, state, fa?.Best, fa?.Passed ?? false, fa?.Used ?? 0, final?.Max is { } fm ? fm + fExtra : null,
                stuckQuiz, stuckUsed, stuckMax);
        }).ToList();
    }

    private static AdminEnrollmentDto ToDto(Classified c) => new(c.Row.Id, c.Row.UserId, c.Row.FullName, c.Row.Email, c.Row.CourseId, c.Row.CourseTitle, c.Row.CourseSlug,
        c.Row.Progress, c.Row.Completed, c.Row.Required, c.Row.Status, c.State, c.FinalBest, c.FinalPassed, c.FinalUsed, c.FinalMax,
        c.Row.EnrolledAt, c.Row.LastAccessedAt, c.Row.HasCert, c.Row.OrderId is null ? "Admin cấp" : "Đơn hàng");

    public async Task<LearningOverviewDto> GetOverviewAsync(CancellationToken ct = default)
    {
        var all = await ClassifyAsync(await Rows(null).ToListAsync(ct), ct);
        int Count(string s) => all.Count(x => x.State == s);
        var active = all.Count(x => x.State != LearningStates.Revoked);
        var completed = all.Count(x => x.Row.Status == EnrollmentStatus.Completed);
        var since = clock.UtcNow.AddDays(-30);
        return new LearningOverviewDto(active, Count(LearningStates.NotStarted), Count(LearningStates.InProgress), completed,
            Count(LearningStates.Stuck), Count(LearningStates.Inactive), Count(LearningStates.NoCertificate), Count(LearningStates.Revoked),
            active == 0 ? 0 : Math.Round(completed * 100m / active, 1),
            await db.LearningSupportRequests.CountAsync(r => r.Status == SupportRequestStatus.Open, ct),
            await db.QuizAttempts.CountAsync(a => a.SubmittedAt != null && a.SubmittedAt >= since && !a.IsPassed, ct));
    }

    public async Task<PagedResult<AdminEnrollmentDto>> ListAsync(AdminEnrollmentQuery q, CancellationToken ct = default)
    {
        var rows = await Rows(q).Take(5000).ToListAsync(ct);
        var classified = await ClassifyAsync(rows, ct);
        if (!string.IsNullOrWhiteSpace(q.State))
            classified = q.State == LearningStates.Completed
                ? classified.Where(c => c.Row.Status == EnrollmentStatus.Completed).ToList()
                : classified.Where(c => c.State == q.State).ToList();
        var items = classified.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Select(ToDto).ToList();
        return new PagedResult<AdminEnrollmentDto>(items, q.Page, q.PageSize, classified.Count);
    }

    public async Task<InstructorLearnersDto> ListForInstructorAsync(Guid instructorId, AdminEnrollmentQuery q, CancellationToken ct = default)
    {
        var filterState = q.State;
        q.InstructorId = instructorId;
        q.State = null;
        var classified = (await ClassifyAsync(await Rows(q).Take(5000).ToListAsync(ct), ct))
            .Where(c => c.State != LearningStates.Revoked).ToList();
        var counts = classified.GroupBy(c => c.State).ToDictionary(g => g.Key, g => g.Count());
        counts["all"] = classified.Count;
        counts[LearningStates.Completed] = classified.Count(c => c.Row.Status == EnrollmentStatus.Completed);

        var filtered = string.IsNullOrWhiteSpace(filterState) ? classified
            : filterState == LearningStates.Completed ? classified.Where(c => c.Row.Status == EnrollmentStatus.Completed).ToList()
            : classified.Where(c => c.State == filterState).ToList();
        // Ưu tiên học viên cần hỗ trợ: hết lượt → bỏ dở → còn lại
        int Rank(string s) => s switch { LearningStates.Stuck => 0, LearningStates.Inactive => 1, _ => 2 };
        var pageRows = filtered.OrderBy(c => Rank(c.State)).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToList();

        var userIds = pageRows.Select(c => c.Row.UserId).Distinct().ToList();
        var avatars = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.AvatarUrl, ct);
        var quizIds = pageRows.Where(c => c.StuckQuizId != null).Select(c => c.StuckQuizId!.Value).Distinct().ToList();
        var quizTitles = await db.Quizzes.AsNoTracking().Where(x => quizIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Title, ct);

        var items = pageRows.Select(c => new InstructorLearnerDto(c.Row.Id, c.Row.UserId, c.Row.FullName, c.Row.Email, avatars.GetValueOrDefault(c.Row.UserId),
            c.Row.CourseId, c.Row.CourseTitle, c.Row.CourseSlug, c.Row.Progress, c.Row.Completed, c.Row.Required, c.State, c.FinalBest, c.FinalPassed,
            c.Row.EnrolledAt, c.Row.LastAccessedAt,
            c.StuckQuizId is { } sq ? new StuckQuizDto(sq, quizTitles.GetValueOrDefault(sq, "Bài kiểm tra"), c.StuckUsed, c.StuckMax) : null)).ToList();
        return new InstructorLearnersDto(counts, new PagedResult<InstructorLearnerDto>(items, q.Page, q.PageSize, filtered.Count));
    }

    public async Task<Result> InstructorGrantAsync(Guid instructorId, Guid enrollmentId, InstructorGrantRequest r, CancellationToken ct = default)
    {
        var owns = await db.Enrollments.AsNoTracking().AnyAsync(e => e.Id == enrollmentId && e.Course.InstructorId == instructorId, ct);
        if (!owns) return Result.Failure(NotFoundEnrollment);
        return await GrantAttemptsAsync(instructorId, enrollmentId, new GrantAttemptsRequest(r.QuizId, r.ExtraAttempts, r.Reason), ct);
    }

    public async Task<byte[]> ExportCsvAsync(AdminEnrollmentQuery q, CancellationToken ct = default)
    {
        q.Page = 1;
        q.PageSize = 100;
        var all = new List<AdminEnrollmentDto>();
        while (true)
        {
            var page = await ListAsync(q, ct);
            all.AddRange(page.Items);
            if (q.Page >= page.TotalPages || all.Count >= 50_000) break;
            q.Page++;
        }
        var labels = new Dictionary<string, string>
        {
            [LearningStates.NotStarted] = "Chưa bắt đầu", [LearningStates.InProgress] = "Đang học", [LearningStates.Completed] = "Hoàn thành",
            [LearningStates.Stuck] = "Hết lượt, chưa đạt", [LearningStates.Inactive] = "Bỏ dở > 14 ngày",
            [LearningStates.NoCertificate] = "Đủ điều kiện, chưa có chứng chỉ", [LearningStates.Revoked] = "Đã thu hồi",
        };
        var sb = new System.Text.StringBuilder("Học viên,Email,Khóa học,Nguồn,Tiến độ (%),Bài bắt buộc đã xong,Tổng bài bắt buộc,Kiểm tra cuối khóa,Điểm cao nhất,Lượt đã làm,Tình trạng,Chứng chỉ,Ngày ghi danh (UTC+7),Lần học gần nhất\n");
        foreach (var e in all)
            sb.Append(Common.Helpers.CsvExport.Field(e.FullName)).Append(',').Append(Common.Helpers.CsvExport.Field(e.Email)).Append(',')
              .Append(Common.Helpers.CsvExport.Field(e.CourseTitle)).Append(',').Append(Common.Helpers.CsvExport.Field(e.Source)).Append(',')
              .Append(e.ProgressPercent.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(e.CompletedLessons).Append(',').Append(e.RequiredLessons).Append(',')
              .Append(e.FinalAttemptsUsed == 0 ? "Chưa làm" : e.FinalPassed ? "Đạt" : "Chưa đạt").Append(',')
              .Append(e.FinalBestScore?.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) ?? "").Append(',').Append(e.FinalAttemptsUsed).Append(',')
              .Append(labels.GetValueOrDefault(e.State, e.State)).Append(',').Append(e.HasCertificate ? "Có" : "Chưa").Append(',')
              .Append(Common.Helpers.CsvExport.Time(e.EnrolledAt)).Append(',').Append(Common.Helpers.CsvExport.Time(e.LastAccessedAt)).AppendLine();
        return Common.Helpers.CsvExport.ToBytes(sb);
    }

    public async Task<Result<AdminEnrollmentDetailDto>> GetDetailAsync(Guid enrollmentId, CancellationToken ct = default)
    {
        var row = await Rows(null, enrollmentId).FirstOrDefaultAsync(ct);
        if (row is null) return NotFoundEnrollment;
        var summary = ToDto((await ClassifyAsync([row], ct))[0]);

        var progress = await db.LessonProgress.AsNoTracking().Where(p => p.EnrollmentId == enrollmentId).ToListAsync(ct);
        var lessons = await db.Lessons.AsNoTracking().Where(l => l.Section.CourseId == row.CourseId)
            .OrderBy(l => l.Section.SortOrder).ThenBy(l => l.SortOrder)
            .Select(l => new { l.Id, Section = l.Section.Title, l.Title, l.IsRequired }).ToListAsync(ct);
        var lessonDtos = lessons.Select(l =>
        {
            var p = progress.FirstOrDefault(x => x.LessonId == l.Id);
            return new AdminLessonProgressDto(l.Id, l.Section, l.Title, l.IsRequired, p?.IsCompleted ?? false, p?.CompletedAt, p?.LastPositionSeconds ?? 0);
        }).ToList();

        var quizzes = await db.Quizzes.AsNoTracking().Where(q => q.CourseId == row.CourseId).OrderBy(q => q.SortOrder).ToListAsync(ct);
        var quizIds = quizzes.Select(q => q.Id).ToList();
        var attempts = await db.QuizAttempts.AsNoTracking().Where(a => a.UserId == row.UserId && quizIds.Contains(a.QuizId))
            .OrderBy(a => a.AttemptNumber).ToListAsync(ct);
        var grants = await db.QuizAttemptGrants.AsNoTracking().Where(g => g.UserId == row.UserId && quizIds.Contains(g.QuizId)).ToListAsync(ct);
        var quizDtos = quizzes.Select(q =>
        {
            var mine = attempts.Where(a => a.QuizId == q.Id).ToList();
            return new AdminQuizProgressDto(q.Id, q.Title, q.IsFinalExam, q.IsRequired, q.PassingScore, q.MaxAttempts,
                grants.Where(g => g.QuizId == q.Id).Sum(g => g.ExtraAttempts), mine.Count,
                mine.Where(a => a.SubmittedAt != null).Select(a => (decimal?)a.ScorePercent).Max(), mine.Any(a => a.IsPassed),
                mine.Select(a => new AdminAttemptDto(a.Id, a.AttemptNumber, a.StartedAt, a.SubmittedAt, a.ScorePercent, a.IsPassed)).ToList());
        }).ToList();

        var cert = await db.Certificates.AsNoTracking().Where(c => c.EnrollmentId == enrollmentId)
            .Select(c => new { c.CertificateCode, c.IssuedAt, c.IsRevoked, c.RevokedAt, c.RevokedReason }).FirstOrDefaultAsync(ct);
        var logs = await db.LearningActionLogs.AsNoTracking().Where(l => l.LearnerId == row.UserId && l.CourseId == row.CourseId).ToListAsync(ct);
        var actorIds = logs.Select(l => l.ActorId).Distinct().ToList();
        var actors = await db.Users.AsNoTracking().Where(u => actorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var requests = await ListMyRequestsAsync(row.UserId, row.CourseId, ct);

        // Dòng thời gian: dựng lại từ dữ liệu học thật (ghi danh, bài học, bài kiểm tra, chứng chỉ, nhắc học, hỗ trợ, thao tác Admin)
        var timeline = new List<LearningEventDto> { new(row.EnrolledAt, "enroll", "Ghi danh khóa học", summary.Source) };
        var enrollment = await db.Enrollments.AsNoTracking().Where(e => e.Id == enrollmentId)
            .Select(e => new { e.CompletedAt, e.StudyReminderSentAt }).FirstAsync(ct);
        timeline.AddRange(lessonDtos.Where(l => l.CompletedAt is not null)
            .Select(l => new LearningEventDto(l.CompletedAt!.Value, "lesson", $"Hoàn thành bài \"{l.Title}\"", l.SectionTitle)));
        foreach (var a in attempts)
        {
            var q = quizzes.First(x => x.Id == a.QuizId);
            timeline.Add(a.SubmittedAt is { } at
                ? new LearningEventDto(at, a.IsPassed ? "quiz-pass" : "quiz-fail",
                    $"{(a.IsPassed ? "Đạt" : "Chưa đạt")} \"{q.Title}\" (lượt {a.AttemptNumber})", $"{a.ScorePercent:0.#}% / cần {q.PassingScore:0.#}%")
                : new LearningEventDto(a.StartedAt, "quiz-start", $"Bắt đầu \"{q.Title}\" (lượt {a.AttemptNumber})", "Chưa nộp bài"));
        }
        if (enrollment.CompletedAt is { } done) timeline.Add(new(done, "complete", "Hoàn thành khóa học", null));
        if (cert is not null)
        {
            timeline.Add(new(cert.IssuedAt, "certificate", "Được cấp chứng chỉ", cert.CertificateCode));
            if (cert.IsRevoked && cert.RevokedAt is { } rv) timeline.Add(new(rv, "certificate-revoked", "Chứng chỉ bị thu hồi", cert.RevokedReason));
        }
        if (enrollment.StudyReminderSentAt is { } rs) timeline.Add(new(rs, "reminder", "Hệ thống gửi email nhắc học", null));
        timeline.AddRange(requests.Select(r => new LearningEventDto(r.CreatedAt, "support", $"Gửi yêu cầu hỗ trợ: {SupportTypeText(r.Type)}", r.Message)));
        timeline.AddRange(logs.Select(l => new LearningEventDto(l.CreatedAt, "admin", $"{actors.GetValueOrDefault(l.ActorId, "Quản trị viên")}: {l.Detail}", l.Reason)));

        return new AdminEnrollmentDetailDto(summary, lessonDtos, quizDtos, cert is { IsRevoked: false } ? cert.CertificateCode : null,
            timeline.OrderByDescending(t => t.At).ToList(), requests);
    }

    public static string SupportTypeText(SupportRequestType t) => t switch
    {
        SupportRequestType.QuizRetake => "xin thêm lượt làm bài kiểm tra",
        SupportRequestType.VideoIssue => "video không phát được",
        SupportRequestType.ProgressIssue => "tiến độ không được ghi nhận",
        SupportRequestType.CertificateIssue => "chưa nhận được chứng chỉ",
        _ => "vấn đề khác",
    };

    // ---------------------------------------------------------------- thao tác hỗ trợ
    private async Task<Enrollment?> LoadAsync(Guid enrollmentId, CancellationToken ct) =>
        await db.Enrollments.Include(e => e.Course).FirstOrDefaultAsync(e => e.Id == enrollmentId, ct);

    private void Log(Enrollment e, Guid adminId, string action, string detail, string? reason) =>
        db.LearningActionLogs.Add(new LearningActionLog
        {
            LearnerId = e.UserId, CourseId = e.CourseId, EnrollmentId = e.Id, ActorId = adminId,
            Action = action, Detail = detail.Length > 500 ? detail[..500] : detail, Reason = reason?.Trim(), CreatedAt = clock.UtcNow,
        });

    /// <summary>Đóng các yêu cầu hỗ trợ đang mở cùng loại khi Admin đã xử lý bằng thao tác tương ứng.</summary>
    private async Task CloseOpenRequestsAsync(Enrollment e, Guid adminId, SupportRequestType type, Guid? quizId, string response, CancellationToken ct)
    {
        var open = await db.LearningSupportRequests.Where(r => r.UserId == e.UserId && r.CourseId == e.CourseId && r.Type == type
            && r.Status == SupportRequestStatus.Open && (quizId == null || r.QuizId == null || r.QuizId == quizId)).ToListAsync(ct);
        foreach (var r in open)
        {
            r.Status = SupportRequestStatus.Resolved;
            r.Response = response;
            r.ResolvedById = adminId;
            r.ResolvedAt = clock.UtcNow;
        }
    }

    public async Task<Result> GrantAttemptsAsync(Guid adminId, Guid enrollmentId, GrantAttemptsRequest r, CancellationToken ct = default)
    {
        var e = await LoadAsync(enrollmentId, ct);
        if (e is null) return Result.Failure(NotFoundEnrollment);
        var quiz = await db.Quizzes.FirstOrDefaultAsync(q => q.Id == r.QuizId && q.CourseId == e.CourseId, ct);
        if (quiz is null) return Result.Failure(Error.NotFound("Bài kiểm tra không thuộc khóa học này."));
        if (quiz.MaxAttempts is null) return Result.Failure(Error.Conflict("Bài kiểm tra không giới hạn lượt, không cần cấp thêm."));
        if (await db.QuizAttempts.AnyAsync(a => a.QuizId == quiz.Id && a.UserId == e.UserId && a.IsPassed, ct))
            return Result.Failure(Error.Conflict("Học viên đã đạt bài kiểm tra này."));

        db.QuizAttemptGrants.Add(new QuizAttemptGrant { QuizId = quiz.Id, UserId = e.UserId, ExtraAttempts = r.ExtraAttempts, Reason = r.Reason.Trim(), GrantedById = adminId });
        Log(e, adminId, "GrantAttempts", $"Cấp thêm {r.ExtraAttempts} lượt cho \"{quiz.Title}\"", r.Reason);
        await CloseOpenRequestsAsync(e, adminId, SupportRequestType.QuizRetake, quiz.Id, $"Đã cấp thêm {r.ExtraAttempts} lượt làm bài.", ct);
        notifications.Add(e.UserId, "Bạn được cấp thêm lượt làm bài",
            $"Bạn có thêm {r.ExtraAttempts} lượt cho \"{quiz.Title}\" ({e.Course.Title}). Hãy ôn lại bài trước khi làm.",
            NotificationType.Course, $"/hoc/{e.Course.Slug}/kiem-tra/{quiz.Id}");
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetLessonAsync(Guid adminId, Guid enrollmentId, SetLessonRequest r, CancellationToken ct = default)
    {
        var e = await LoadAsync(enrollmentId, ct);
        if (e is null) return Result.Failure(NotFoundEnrollment);
        var lesson = await db.Lessons.AsNoTracking().Where(l => l.Id == r.LessonId && l.Section.CourseId == e.CourseId).Select(l => new { l.Id, l.Title }).FirstOrDefaultAsync(ct);
        if (lesson is null) return Result.Failure(Error.NotFound("Bài học không thuộc khóa học này."));

        var p = await db.LessonProgress.FirstOrDefaultAsync(x => x.EnrollmentId == e.Id && x.LessonId == lesson.Id, ct);
        if (p is null)
        {
            p = new LessonProgress { EnrollmentId = e.Id, LessonId = lesson.Id };
            db.LessonProgress.Add(p);
        }
        p.IsCompleted = r.Completed;
        p.CompletedAt = r.Completed ? p.CompletedAt ?? clock.UtcNow : null;
        p.UpdatedAt = clock.UtcNow;
        Log(e, adminId, r.Completed ? "CompleteLesson" : "UncompleteLesson", $"{(r.Completed ? "Đánh dấu hoàn thành" : "Bỏ hoàn thành")} bài \"{lesson.Title}\"", r.Reason);
        if (r.Completed) await CloseOpenRequestsAsync(e, adminId, SupportRequestType.ProgressIssue, null, $"Đã ghi nhận hoàn thành bài \"{lesson.Title}\".", ct);
        await db.SaveChangesAsync(ct);
        await learning.RecalculateAsync(e.Id, ct);
        return Result.Success();
    }

    public async Task<Result> ResetProgressAsync(Guid adminId, Guid enrollmentId, string reason, CancellationToken ct = default)
    {
        var e = await LoadAsync(enrollmentId, ct);
        if (e is null) return Result.Failure(NotFoundEnrollment);
        if (await db.Certificates.AnyAsync(c => c.EnrollmentId == e.Id && !c.IsRevoked, ct))
            return Result.Failure(Error.Conflict("Học viên đã có chứng chỉ. Thu hồi chứng chỉ trước khi đặt lại tiến độ."));

        await db.LessonProgress.Where(p => p.EnrollmentId == e.Id).ExecuteDeleteAsync(ct);
        e.LastLessonId = null;
        Log(e, adminId, "ResetProgress", "Đặt lại tiến độ học (bài kiểm tra đã làm vẫn giữ lịch sử)", reason);
        notifications.Add(e.UserId, "Tiến độ khóa học đã được đặt lại", $"Tiến độ khóa \"{e.Course.Title}\" đã được đặt lại để bạn học lại từ đầu.",
            NotificationType.Course, $"/hoc/{e.Course.Slug}");
        await db.SaveChangesAsync(ct);
        await learning.RecalculateAsync(e.Id, ct);
        return Result.Success();
    }

    public async Task<Result<ProgressDto>> RecalculateAsync(Guid adminId, Guid enrollmentId, string reason, CancellationToken ct = default)
    {
        var e = await LoadAsync(enrollmentId, ct);
        if (e is null) return NotFoundEnrollment;
        if (e.Status == EnrollmentStatus.Revoked) return Error.Conflict("Quyền học đã bị thu hồi.");
        var p = await learning.RecalculateAsync(e.Id, ct);
        Log(e, adminId, "Recalculate", $"Tính lại tiến độ: {p.ProgressPercent:0.#}%{(p.CertificateCode is { } code ? $", chứng chỉ {code}" : "")}", reason);
        if (p.CertificateCode is not null)
            await CloseOpenRequestsAsync(e, adminId, SupportRequestType.CertificateIssue, null, $"Đã cấp chứng chỉ {p.CertificateCode}.", ct);
        await db.SaveChangesAsync(ct);
        return p;
    }

    public async Task<Result> SetAccessAsync(Guid adminId, Guid enrollmentId, bool active, string reason, CancellationToken ct = default)
    {
        var e = await LoadAsync(enrollmentId, ct);
        if (e is null) return Result.Failure(NotFoundEnrollment);
        if (active == (e.Status != EnrollmentStatus.Revoked)) return Result.Failure(Error.Conflict(active ? "Quyền học đang hoạt động." : "Quyền học đã bị thu hồi."));

        var course = await db.Courses.FirstAsync(c => c.Id == e.CourseId, ct);
        if (active)
        {
            e.Status = EnrollmentStatus.Active;
            course.EnrollmentCount += 1;
        }
        else
        {
            e.Status = EnrollmentStatus.Revoked;
            course.EnrollmentCount = Math.Max(0, course.EnrollmentCount - 1);
        }
        Log(e, adminId, active ? "RestoreAccess" : "RevokeAccess", active ? "Khôi phục quyền học" : "Thu hồi quyền học", reason);
        notifications.Add(e.UserId, active ? "Quyền học đã được khôi phục" : "Quyền học đã bị thu hồi",
            $"Khóa \"{course.Title}\": {reason.Trim()}", NotificationType.Course, active ? $"/hoc/{course.Slug}" : "/hoc-vien");
        await db.SaveChangesAsync(ct);
        if (active) await learning.RecalculateAsync(e.Id, ct);
        return Result.Success();
    }

    public async Task<Result<Guid>> ManualEnrollAsync(Guid adminId, ManualEnrollRequest r, CancellationToken ct = default)
    {
        var email = r.Email.Trim().ToUpperInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == email, ct);
        if (user is null) return Error.NotFound("Không tìm thấy tài khoản với email này.");
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == r.CourseId && !c.IsDeleted, ct);
        if (course is null) return Error.NotFound("Không tìm thấy khóa học.");
        var existing = await db.Enrollments.FirstOrDefaultAsync(e => e.UserId == user.Id && e.CourseId == course.Id, ct);
        if (existing is not null)
            return existing.Status == EnrollmentStatus.Revoked
                ? Error.Conflict("Học viên đã có lượt ghi danh bị thu hồi. Hãy dùng \"Khôi phục quyền học\".")
                : Error.Conflict("Học viên đã có quyền học khóa này.");

        var enrollment = new Enrollment { UserId = user.Id, CourseId = course.Id, EnrolledAt = clock.UtcNow, Status = EnrollmentStatus.Active };
        db.Enrollments.Add(enrollment);
        course.EnrollmentCount += 1;
        Log(enrollment, adminId, "ManualEnroll", $"Cấp quyền học thủ công khóa \"{course.Title}\"", r.Reason);
        notifications.Add(user.Id, "Bạn được cấp quyền học", $"Bạn có thể học khóa \"{course.Title}\" ngay bây giờ.", NotificationType.Course, $"/hoc/{course.Slug}");
        await db.SaveChangesAsync(ct);
        return enrollment.Id;
    }

    // ---------------------------------------------------------------- yêu cầu hỗ trợ
    private IQueryable<SupportRequestDto> ProjectRequests(IQueryable<LearningSupportRequest> q) =>
        q.Select(r => new SupportRequestDto(r.Id, r.UserId, r.User.FullName, r.User.Email ?? "", r.CourseId, r.Course.Title, r.QuizId, r.LessonId,
            r.Type, r.Message, r.Status, r.Response, r.CreatedAt, r.ResolvedAt,
            db.Enrollments.Where(e => e.UserId == r.UserId && e.CourseId == r.CourseId).Select(e => (Guid?)e.Id).FirstOrDefault()));

    public async Task<Result<SupportRequestDto>> CreateRequestAsync(Guid userId, CreateSupportRequest r, CancellationToken ct = default)
    {
        var course = await db.Courses.AsNoTracking().Where(c => c.Id == r.CourseId).Select(c => new { c.Id, c.Title, c.InstructorId }).FirstOrDefaultAsync(ct);
        if (course is null) return Error.NotFound("Không tìm thấy khóa học.");
        if (!await db.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseId == course.Id && e.Status != EnrollmentStatus.Revoked, ct))
            return Error.Forbidden("Bạn chưa sở hữu khóa học này.");
        if (r.QuizId is { } qid && !await db.Quizzes.AnyAsync(q => q.Id == qid && q.CourseId == course.Id, ct))
            return Error.Validation("Bài kiểm tra không thuộc khóa học.");
        if (await db.LearningSupportRequests.AnyAsync(x => x.UserId == userId && x.CourseId == course.Id && x.Type == r.Type
                && x.Status == SupportRequestStatus.Open && (r.QuizId == null || x.QuizId == r.QuizId), ct))
            return Error.Conflict("Bạn đã gửi yêu cầu tương tự, đang chờ xử lý.");

        var entity = new LearningSupportRequest
        {
            UserId = userId, CourseId = course.Id, QuizId = r.QuizId, LessonId = r.LessonId, Type = r.Type, Message = r.Message.Trim(),
        };
        db.LearningSupportRequests.Add(entity);
        await notifications.AddToRoleAsync(Roles.Admin, "Yêu cầu hỗ trợ học tập", $"Học viên báo: {SupportTypeText(r.Type)} ({course.Title}).",
            NotificationType.System, "/quan-tri/ho-tro-hoc-tap", ct);
        if (r.Type == SupportRequestType.QuizRetake)
            notifications.Add(course.InstructorId, "Học viên xin thêm lượt làm bài", $"Có học viên hết lượt bài kiểm tra trong khóa \"{course.Title}\".",
                NotificationType.Course, "/giang-day/hoc-vien");
        await db.SaveChangesAsync(ct);
        return (await ProjectRequests(db.LearningSupportRequests.AsNoTracking().Where(x => x.Id == entity.Id)).FirstAsync(ct));
    }

    public async Task<IReadOnlyList<SupportRequestDto>> ListMyRequestsAsync(Guid userId, Guid? courseId, CancellationToken ct = default) =>
        await ProjectRequests(db.LearningSupportRequests.AsNoTracking().Where(r => r.UserId == userId && (courseId == null || r.CourseId == courseId))
            .OrderByDescending(r => r.CreatedAt)).Take(50).ToListAsync(ct);

    public Task<PagedResult<SupportRequestDto>> ListRequestsAsync(SupportRequestQuery q, CancellationToken ct = default)
    {
        var query = db.LearningSupportRequests.AsNoTracking().AsQueryable();
        if (q.Status is { } s) query = query.Where(r => r.Status == s);
        if (q.Type is { } t) query = query.Where(r => r.Type == t);
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.Trim();
            query = query.Where(r => r.User.FullName.Contains(kw) || r.User.Email!.Contains(kw) || r.Course.Title.Contains(kw));
        }
        return ProjectRequests(query.OrderBy(r => r.Status == SupportRequestStatus.Open ? 0 : 1).ThenByDescending(r => r.CreatedAt))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);
    }

    public async Task<Result> RespondAsync(Guid adminId, Guid requestId, RespondSupportRequest r, CancellationToken ct = default)
    {
        var req = await db.LearningSupportRequests.Include(x => x.Course).FirstOrDefaultAsync(x => x.Id == requestId, ct);
        if (req is null) return Result.Failure(Error.NotFound("Không tìm thấy yêu cầu."));
        if (req.Status != SupportRequestStatus.Open) return Result.Failure(Error.Conflict("Yêu cầu đã được xử lý."));
        req.Status = r.Status;
        req.Response = r.Response.Trim();
        req.ResolvedById = adminId;
        req.ResolvedAt = clock.UtcNow;
        db.LearningActionLogs.Add(new LearningActionLog
        {
            LearnerId = req.UserId, CourseId = req.CourseId, ActorId = adminId, CreatedAt = clock.UtcNow,
            Action = r.Status == SupportRequestStatus.Resolved ? "ResolveRequest" : "RejectRequest",
            Detail = $"{(r.Status == SupportRequestStatus.Resolved ? "Đã xử lý" : "Từ chối")} yêu cầu: {SupportTypeText(req.Type)}", Reason = req.Response,
        });
        notifications.Add(req.UserId, r.Status == SupportRequestStatus.Resolved ? "Yêu cầu hỗ trợ đã được xử lý" : "Yêu cầu hỗ trợ bị từ chối",
            $"{req.Course.Title}: {req.Response}", NotificationType.Course, $"/hoc/{req.Course.Slug}");
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
