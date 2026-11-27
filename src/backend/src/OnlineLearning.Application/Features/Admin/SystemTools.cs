using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Common.Helpers;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Admin;

// =====================================================================
// Cài đặt hệ thống
// =====================================================================
public enum SettingType { Integer, Decimal, Boolean, Text }

public sealed record SettingDefinition(string Key, string Group, string Label, string? Hint, SettingType Type, decimal? Min = null, decimal? Max = null);

/// <summary>Các cài đặt Admin chỉnh được trên giao diện (ghi đè appsettings.json, có hiệu lực ngay).</summary>
public static class SettingCatalog
{
    public static readonly IReadOnlyList<SettingDefinition> All =
    [
        new("Revenue:InstructorSharePercent", "Doanh thu và rút tiền", "Tỷ lệ giảng viên được hưởng (%)", "Áp dụng cho đơn bán sau thời điểm đổi; đơn cũ giữ tỷ lệ lúc bán", SettingType.Decimal, 0, 100),
        new("Revenue:HoldDays", "Doanh thu và rút tiền", "Số ngày giữ tiền trước khi được rút", "Nên bằng hạn yêu cầu hoàn tiền", SettingType.Integer, 0, 90),
        new("Revenue:MinPayoutAmount", "Doanh thu và rút tiền", "Số tiền rút tối thiểu (đ)", null, SettingType.Decimal, 0, 100_000_000),
        new("Revenue:RefundRequestDays", "Chính sách hoàn tiền", "Số ngày được yêu cầu hoàn tiền", "Hiển thị trên trang Chính sách hoàn tiền", SettingType.Integer, 0, 90),
        new("Revenue:RefundMaxProgressPercent", "Chính sách hoàn tiền", "Tỷ lệ nội dung tối đa đã học để được hoàn (%)", null, SettingType.Decimal, 0, 100),
        new("Invoice:SellerName", "Thông tin bên bán (biên nhận)", "Tên bên bán", null, SettingType.Text),
        new("Invoice:SellerAddress", "Thông tin bên bán (biên nhận)", "Địa chỉ", null, SettingType.Text),
        new("Invoice:SellerTaxCode", "Thông tin bên bán (biên nhận)", "Mã số thuế", "Để trống nếu chưa có", SettingType.Text),
        new("Invoice:SellerEmail", "Thông tin bên bán (biên nhận)", "Email hỗ trợ", null, SettingType.Text),
        new("Invoice:SellerPhone", "Thông tin bên bán (biên nhận)", "Điện thoại", null, SettingType.Text),
        new("StudyReminder:Enabled", "Email nhắc nhở", "Gửi email nhắc học", null, SettingType.Boolean),
        new("StudyReminder:InactiveDays", "Email nhắc nhở", "Nhắc học sau số ngày không vào học", null, SettingType.Integer, 1, 60),
        new("CartReminder:Enabled", "Email nhắc nhở", "Gửi email nhắc giỏ hàng", null, SettingType.Boolean),
        new("CartReminder:DelayHours", "Email nhắc nhở", "Nhắc giỏ hàng sau số giờ", null, SettingType.Integer, 1, 720),
    ];

    public static SettingDefinition? Find(string key) => All.FirstOrDefault(d => d.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
}

public sealed record SettingDto(string Key, string Group, string Label, string? Hint, SettingType Type, decimal? Min, decimal? Max,
    string? Value, bool IsOverridden, DateTime? UpdatedAt);
public sealed record SaveSettingsRequest(IReadOnlyDictionary<string, string?> Values);
public sealed record PublicSettingsDto(int RefundRequestDays, decimal RefundMaxProgressPercent, decimal InstructorSharePercent,
    string SellerName, string? SellerEmail, string? SellerPhone, string? SellerAddress);

/// <summary>Đọc giá trị cấu hình hiện hành và áp dụng giá trị mới vào cấu hình đang chạy (triển khai ở Infrastructure).</summary>
public interface IRuntimeSettings
{
    string? Get(string key);
    void Apply(IReadOnlyDictionary<string, string?> overrides);
}

public interface ISettingsService
{
    Task<IReadOnlyList<SettingDto>> GetAllAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<SettingDto>>> SaveAsync(Guid adminId, SaveSettingsRequest request, CancellationToken ct = default);
    PublicSettingsDto GetPublic();
}

public sealed class SettingsService(IAppDbContext db, IRuntimeSettings runtime, IDateTimeProvider clock) : ISettingsService
{
    public async Task<IReadOnlyList<SettingDto>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = await db.SystemSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, ct);
        return SettingCatalog.All.Select(d =>
        {
            rows.TryGetValue(d.Key, out var row);
            return new SettingDto(d.Key, d.Group, d.Label, d.Hint, d.Type, d.Min, d.Max, runtime.Get(d.Key), row is not null, row?.UpdatedAt);
        }).ToList();
    }

    /// <summary>Giá trị null hoặc chuỗi rỗng ở cài đặt số/bật tắt = khôi phục giá trị mặc định trong appsettings.json.</summary>
    public async Task<Result<IReadOnlyList<SettingDto>>> SaveAsync(Guid adminId, SaveSettingsRequest r, CancellationToken ct = default)
    {
        var normalized = new Dictionary<string, string?>();
        foreach (var (key, raw) in r.Values)
        {
            var def = SettingCatalog.Find(key);
            if (def is null) return Error.Validation($"Cài đặt không hợp lệ: {key}.");
            var value = raw?.Trim();
            if (string.IsNullOrEmpty(value) && def.Type != SettingType.Text) { normalized[def.Key] = null; continue; }
            switch (def.Type)
            {
                case SettingType.Integer when !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _):
                case SettingType.Decimal when !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _):
                    return Error.Validation($"{def.Label}: phải là số.");
                case SettingType.Boolean when value is not ("true" or "false"):
                    return Error.Validation($"{def.Label}: chỉ nhận true/false.");
            }
            if (def.Type is SettingType.Integer or SettingType.Decimal)
            {
                var n = decimal.Parse(value!, CultureInfo.InvariantCulture);
                if ((def.Min is { } min && n < min) || (def.Max is { } max && n > max))
                    return Error.Validation($"{def.Label}: trong khoảng {def.Min:0.##}–{def.Max:0.##}.");
            }
            if (def.Type == SettingType.Text && value?.Length > 300) return Error.Validation($"{def.Label}: tối đa 300 ký tự.");
            normalized[def.Key] = value ?? "";
        }

        var now = clock.UtcNow;
        var keys = normalized.Keys.ToList();
        var existing = await db.SystemSettings.Where(s => keys.Contains(s.Key)).ToDictionaryAsync(s => s.Key, ct);
        foreach (var (key, value) in normalized)
        {
            existing.TryGetValue(key, out var row);
            if (value is null)
            {
                if (row is not null) db.SystemSettings.Remove(row);
            }
            else if (row is null) db.SystemSettings.Add(new SystemSetting { Key = key, Value = value, UpdatedAt = now, UpdatedById = adminId });
            else { row.Value = value; row.UpdatedAt = now; row.UpdatedById = adminId; }
        }
        await db.SaveChangesAsync(ct);
        runtime.Apply(await db.SystemSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => (string?)s.Value, ct));
        return (await GetAllAsync(ct)).ToList();
    }

    public PublicSettingsDto GetPublic()
    {
        int I(string k, int d) => int.TryParse(runtime.Get(k), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : d;
        decimal D(string k, decimal d) => decimal.TryParse(runtime.Get(k), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : d;
        return new PublicSettingsDto(I("Revenue:RefundRequestDays", 7), D("Revenue:RefundMaxProgressPercent", 20), D("Revenue:InstructorSharePercent", 70),
            runtime.Get("Invoice:SellerName") ?? "EduViet", runtime.Get("Invoice:SellerEmail"), runtime.Get("Invoice:SellerPhone"), runtime.Get("Invoice:SellerAddress"));
    }
}

// =====================================================================
// Báo cáo phân tích
// =====================================================================
public sealed record ReportTotalsDto(decimal Revenue, int PaidOrders, decimal RefundedAmount, int RefundedOrders, int NewUsers, int Enrollments, int Completions);
public sealed record ReportMonthDto(string Month, decimal Revenue, int Orders, int Enrollments);
public sealed record ReportCourseDto(Guid CourseId, string Title, string InstructorName, int Sales, decimal Revenue, int Enrollments,
    decimal CompletionRate, decimal AverageRating, int Refunds);
public sealed record ReportInstructorDto(Guid InstructorId, string Name, int Sales, decimal Revenue, int Students, int Courses);
public sealed record AnalyticsReportDto(DateOnly From, DateOnly To, ReportTotalsDto Totals, IReadOnlyList<ReportMonthDto> Monthly,
    IReadOnlyList<ReportCourseDto> TopCourses, IReadOnlyList<ReportInstructorDto> TopInstructors);
public sealed record LessonStatDto(Guid LessonId, string SectionTitle, string Title, bool IsRequired, int CompletedCount, decimal CompletedPercent);
public sealed record CourseLessonStatsDto(Guid CourseId, string Title, int ActiveLearners, IReadOnlyList<LessonStatDto> Lessons);

public sealed class ReportQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public interface IReportService
{
    Task<AnalyticsReportDto> GetAsync(ReportQuery query, CancellationToken ct = default);
    Task<byte[]> ExportCoursesCsvAsync(ReportQuery query, CancellationToken ct = default);
    /// <summary>Tỷ lệ học viên hoàn thành từng bài: thấy bài nào nhiều người bỏ dở. instructorId != null thì chỉ cho giảng viên sở hữu.</summary>
    Task<Result<CourseLessonStatsDto>> GetLessonStatsAsync(Guid courseId, Guid? instructorId, CancellationToken ct = default);
}

public sealed class ReportService(IAppDbContext db, IDateTimeProvider clock) : IReportService
{
    private (DateOnly From, DateOnly To, DateTime Start, DateTime End) Range(ReportQuery q)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.AddHours(7));
        var to = q.To ?? today;
        var from = q.From ?? to.AddMonths(-11).AddDays(1 - to.AddMonths(-11).Day);
        if (from > to) (from, to) = (to, from);
        return (from, to, from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7), to.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1).AddHours(-7));
    }

    public async Task<AnalyticsReportDto> GetAsync(ReportQuery q, CancellationToken ct = default)
    {
        var (from, to, start, end) = Range(q);
        var paid = await db.Orders.AsNoTracking()
            .Where(o => (o.Status == OrderStatus.Paid || o.Status == OrderStatus.Refunded) && o.PaidAt >= start && o.PaidAt < end)
            .Select(o => new { o.Id, o.PaidAt, o.TotalAmount, o.Status }).ToListAsync(ct);
        var orderIds = paid.Select(o => o.Id).ToList();
        var items = await db.OrderItems.AsNoTracking().Where(i => orderIds.Contains(i.OrderId))
            .Select(i => new { i.OrderId, i.CourseId, i.InstructorId, i.FinalPrice }).ToListAsync(ct);
        var refundedIds = paid.Where(o => o.Status == OrderStatus.Refunded).Select(o => o.Id).ToHashSet();
        var enrollRows = await db.Enrollments.AsNoTracking().Where(e => e.EnrolledAt >= start && e.EnrolledAt < end).Select(e => e.EnrolledAt).ToListAsync(ct);

        var totals = new ReportTotalsDto(
            paid.Where(o => o.Status == OrderStatus.Paid).Sum(o => o.TotalAmount), paid.Count(o => o.Status == OrderStatus.Paid),
            paid.Where(o => o.Status == OrderStatus.Refunded).Sum(o => o.TotalAmount), refundedIds.Count,
            await db.Users.CountAsync(u => u.CreatedAt >= start && u.CreatedAt < end, ct), enrollRows.Count,
            await db.Enrollments.CountAsync(e => e.CompletedAt >= start && e.CompletedAt < end, ct));

        var months = new List<ReportMonthDto>();
        for (var m = new DateOnly(from.Year, from.Month, 1); m <= to; m = m.AddMonths(1))
        {
            bool In(DateTime utc) { var d = utc.AddHours(7); return d.Year == m.Year && d.Month == m.Month; }
            var monthPaid = paid.Where(o => o.Status == OrderStatus.Paid && In(o.PaidAt!.Value)).ToList();
            months.Add(new ReportMonthDto($"{m:yyyy-MM}", monthPaid.Sum(o => o.TotalAmount), monthPaid.Count, enrollRows.Count(In)));
        }

        var sold = items.Where(i => !refundedIds.Contains(i.OrderId)).ToList();
        var courseIds = items.Select(i => i.CourseId).Distinct().ToList();
        var courses = await db.Courses.AsNoTracking().Where(c => courseIds.Contains(c.Id))
            .Select(c => new
            {
                c.Id, c.Title, Instructor = c.Instructor.FullName, c.AverageRating,
                Enrolled = c.Enrollments.Count(e => e.Status != EnrollmentStatus.Revoked),
                Completed = c.Enrollments.Count(e => e.Status == EnrollmentStatus.Completed),
            }).ToListAsync(ct);
        var topCourses = courses.Select(c => new ReportCourseDto(c.Id, c.Title, c.Instructor,
                sold.Count(i => i.CourseId == c.Id), sold.Where(i => i.CourseId == c.Id).Sum(i => i.FinalPrice), c.Enrolled,
                c.Enrolled == 0 ? 0 : Math.Round(c.Completed * 100m / c.Enrolled, 1), c.AverageRating,
                items.Count(i => i.CourseId == c.Id && refundedIds.Contains(i.OrderId))))
            .OrderByDescending(c => c.Revenue).ThenByDescending(c => c.Sales).Take(20).ToList();

        var instructorIds = sold.Select(i => i.InstructorId).Distinct().ToList();
        var instructors = await db.Users.AsNoTracking().Where(u => instructorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, Students = u.TaughtCourses.Sum(c => c.EnrollmentCount), Courses = u.TaughtCourses.Count(c => c.Status == CourseStatus.Published && !c.IsDeleted) })
            .ToListAsync(ct);
        var topInstructors = instructors.Select(u => new ReportInstructorDto(u.Id, u.FullName, sold.Count(i => i.InstructorId == u.Id),
                sold.Where(i => i.InstructorId == u.Id).Sum(i => i.FinalPrice), u.Students, u.Courses))
            .OrderByDescending(x => x.Revenue).Take(10).ToList();

        return new AnalyticsReportDto(from, to, totals, months, topCourses, topInstructors);
    }

    public async Task<byte[]> ExportCoursesCsvAsync(ReportQuery q, CancellationToken ct = default)
    {
        var r = await GetAsync(q, ct);
        var sb = new StringBuilder($"Báo cáo khóa học từ {r.From:dd/MM/yyyy} đến {r.To:dd/MM/yyyy}\n");
        sb.AppendLine("Khóa học,Giảng viên,Lượt bán,Doanh thu,Học viên,Tỷ lệ hoàn thành (%),Đánh giá TB,Hoàn tiền");
        foreach (var c in r.TopCourses)
            sb.Append(CsvExport.Field(c.Title)).Append(',').Append(CsvExport.Field(c.InstructorName)).Append(',').Append(c.Sales).Append(',')
              .Append(CsvExport.Money(c.Revenue)).Append(',').Append(c.Enrollments).Append(',').Append(c.CompletionRate.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(c.AverageRating.ToString("0.0", CultureInfo.InvariantCulture)).Append(',').Append(c.Refunds).AppendLine();
        return CsvExport.ToBytes(sb);
    }

    public async Task<Result<CourseLessonStatsDto>> GetLessonStatsAsync(Guid courseId, Guid? instructorId, CancellationToken ct = default)
    {
        var course = await db.Courses.AsNoTracking().Where(c => c.Id == courseId).Select(c => new { c.Id, c.Title, c.InstructorId }).FirstOrDefaultAsync(ct);
        if (course is null || (instructorId is { } iid && course.InstructorId != iid)) return Error.NotFound("Không tìm thấy khóa học.");
        var active = await db.Enrollments.CountAsync(e => e.CourseId == courseId && e.Status != EnrollmentStatus.Revoked, ct);
        var lessons = await db.Lessons.AsNoTracking().Where(l => l.Section.CourseId == courseId)
            .OrderBy(l => l.Section.SortOrder).ThenBy(l => l.SortOrder)
            .Select(l => new
            {
                l.Id, Section = l.Section.Title, l.Title, l.IsRequired,
                Done = db.LessonProgress.Count(p => p.LessonId == l.Id && p.IsCompleted && p.Enrollment.Status != EnrollmentStatus.Revoked),
            }).ToListAsync(ct);
        return new CourseLessonStatsDto(course.Id, course.Title, active, lessons.Select(l => new LessonStatDto(l.Id, l.Section, l.Title, l.IsRequired, l.Done,
            active == 0 ? 0 : Math.Round(l.Done * 100m / active, 1))).ToList());
    }
}

// =====================================================================
// Trạng thái hệ thống (triển khai ở Infrastructure)
// =====================================================================
public sealed record ComponentStatusDto(string Name, string Status, string Detail);
public sealed record JobStatusDto(string Key, string Name, string Interval, DateTime? LastRunAt, bool? LastSucceeded, string? LastMessage, bool Enabled);
public sealed record EmailEventDto(DateTime At, string To, string Subject, bool Succeeded, string? Error, bool Delivered);
public sealed record SystemStatusDto(DateTime ServerTimeUtc, string Environment, TimeSpan Uptime, IReadOnlyList<ComponentStatusDto> Components,
    IReadOnlyList<JobStatusDto> Jobs, IReadOnlyList<EmailEventDto> RecentEmails, int EmailFailures24h, IReadOnlyDictionary<string, int> Counts);

public interface ISystemStatusService
{
    Task<SystemStatusDto> GetAsync(CancellationToken ct = default);
    /// <summary>Chạy ngay một công việc nền: order-expiry | cart-reminder | study-reminder.</summary>
    Task<Result<string>> RunJobAsync(string key, CancellationToken ct = default);
}
