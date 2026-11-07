using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Courses;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Instructors;

// ---------------------------------------------------------------- DTOs

public sealed record InstructorExperienceDto(Guid Id, ExperienceType Type, string Title, string? Organization,
    int? StartYear, int? EndYear, string? Description, string? CredentialUrl);

public sealed record InstructorLinksDto(string? Website, string? LinkedIn, string? Youtube, string? Facebook);

public sealed record InstructorReviewDto(Guid Id, string UserFullName, string? UserAvatarUrl, byte Rating, string? Comment,
    DateTime CreatedAt, string CourseTitle, string CourseSlug, string? InstructorReply);

/// <summary>Mức độ phản hồi tin nhắn trong 12 tháng gần nhất (chỉ trả về khi đủ số cuộc trao đổi để có ý nghĩa).</summary>
public sealed record ResponseStatsDto(int Conversations, int ReplyRatePercent, double? MedianReplyHours);

public sealed record InstructorDetailDto(
    Guid Id, string FullName, string? AvatarUrl, string? Headline, string? Bio,
    int TotalCourses, int TotalStudents, int TotalReviews, decimal AverageRating,
    bool IsVerified, DateTime? VerifiedAt, DateTime MemberSince, int? YearsOfExperience,
    IReadOnlyList<string> Expertise, InstructorLinksDto Links, IReadOnlyList<InstructorExperienceDto> Experiences,
    int CertificatesIssued, IReadOnlyDictionary<int, int> RatingDistribution, IReadOnlyList<InstructorReviewDto> RecentReviews,
    ResponseStatsDto? Response);

public sealed record InstructorProfileEditDto(
    string FullName, string? AvatarUrl, string? Headline, string? Bio, int? YearsOfExperience,
    IReadOnlyList<string> Expertise, InstructorLinksDto Links, IReadOnlyList<InstructorExperienceDto> Experiences,
    bool IsVerified, DateTime? VerifiedAt);

public sealed record SaveInstructorExperience(ExperienceType Type, string Title, string? Organization,
    int? StartYear, int? EndYear, string? Description, string? CredentialUrl);

public sealed record UpdateInstructorProfileRequest(
    string? AvatarUrl, string? Headline, string? Bio, int? YearsOfExperience, IReadOnlyList<string>? Expertise,
    string? WebsiteUrl, string? LinkedInUrl, string? YoutubeUrl, string? FacebookUrl,
    IReadOnlyList<SaveInstructorExperience>? Experiences);

public sealed class UpdateInstructorProfileRequestValidator : AbstractValidator<UpdateInstructorProfileRequest>
{
    public UpdateInstructorProfileRequestValidator()
    {
        var maxYear = DateTime.UtcNow.Year + 1;
        RuleFor(x => x.AvatarUrl).Must(SaveCourseRequestValidator.BeHttpsOrNull)
            .WithMessage("Ảnh đại diện phải là URL https hoặc ảnh đã tải lên.");
        RuleFor(x => x.Headline).MaximumLength(200);
        RuleFor(x => x.Bio).MaximumLength(4000);
        RuleFor(x => x.YearsOfExperience).InclusiveBetween(0, 60).When(x => x.YearsOfExperience.HasValue)
            .WithMessage("Số năm kinh nghiệm từ 0 đến 60.");
        RuleFor(x => x.Expertise).Must(e => e is null || e.Count <= 15).WithMessage("Tối đa 15 chuyên môn.");
        RuleForEach(x => x.Expertise).NotEmpty().MaximumLength(40).Must(t => !t.Contains(';'))
            .WithMessage("Mỗi chuyên môn tối đa 40 ký tự, không chứa dấu ;.");
        RuleFor(x => x.WebsiteUrl).Must(BeHttpsUrlOrEmpty).WithMessage("Website phải là đường dẫn https hợp lệ.");
        RuleFor(x => x.LinkedInUrl).Must(BeHttpsUrlOrEmpty).WithMessage("LinkedIn phải là đường dẫn https hợp lệ.");
        RuleFor(x => x.YoutubeUrl).Must(BeHttpsUrlOrEmpty).WithMessage("YouTube phải là đường dẫn https hợp lệ.");
        RuleFor(x => x.FacebookUrl).Must(BeHttpsUrlOrEmpty).WithMessage("Facebook phải là đường dẫn https hợp lệ.");
        RuleFor(x => x.Experiences).Must(e => e is null || e.Count <= 20).WithMessage("Tối đa 20 mục kinh nghiệm.");
        RuleForEach(x => x.Experiences).ChildRules(e =>
        {
            e.RuleFor(x => x.Type).IsInEnum();
            e.RuleFor(x => x.Title).NotEmpty().WithMessage("Nhập vị trí, bằng cấp hoặc tên chứng nhận.").MaximumLength(200);
            e.RuleFor(x => x.Organization).MaximumLength(200);
            e.RuleFor(x => x.Description).MaximumLength(1000);
            e.RuleFor(x => x.StartYear).InclusiveBetween(1950, maxYear).When(x => x.StartYear.HasValue).WithMessage("Năm bắt đầu không hợp lệ.");
            e.RuleFor(x => x.EndYear).InclusiveBetween(1950, maxYear + 10).When(x => x.EndYear.HasValue).WithMessage("Năm kết thúc không hợp lệ.");
            e.RuleFor(x => x).Must(x => x.StartYear is null || x.EndYear is null || x.EndYear >= x.StartYear)
                .WithName("EndYear").WithMessage("Năm kết thúc phải sau năm bắt đầu.");
            e.RuleFor(x => x.CredentialUrl).Must(BeHttpsUrlOrEmpty).WithMessage("Đường dẫn chứng nhận phải là https.");
        });
    }

    internal static bool BeHttpsUrlOrEmpty(string? url) =>
        string.IsNullOrWhiteSpace(url) ||
        (url.Length <= 300 && Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps);
}

// ---------------------------------------------------------------- Service

public interface IInstructorProfileService
{
    /// <summary>Hồ sơ công khai: thông tin, kinh nghiệm, thống kê, đánh giá gần đây, mức độ phản hồi.</summary>
    Task<Result<InstructorDetailDto>> GetPublicAsync(Guid instructorId, CancellationToken ct = default);
    Task<Result<InstructorProfileEditDto>> GetMineAsync(Guid userId, CancellationToken ct = default);
    Task<Result<InstructorProfileEditDto>> UpdateMineAsync(Guid userId, UpdateInstructorProfileRequest request, CancellationToken ct = default);
}

public sealed class InstructorProfileService(IAppDbContext db, IDateTimeProvider clock) : IInstructorProfileService
{
    /// <summary>Cần ít nhất ngần này cuộc trao đổi thì mới hiển thị tỷ lệ phản hồi.</summary>
    public const int MinConversationsForResponseStats = 3;

    public async Task<Result<InstructorDetailDto>> GetPublicAsync(Guid instructorId, CancellationToken ct = default)
    {
        if (!await IsInstructorAsync(instructorId, ct)) return Error.NotFound("Không tìm thấy giảng viên.");

        var user = await db.Users.AsNoTracking().Include(u => u.Experiences)
            .FirstOrDefaultAsync(u => u.Id == instructorId && u.IsActive, ct);
        if (user is null) return Error.NotFound("Không tìm thấy giảng viên.");

        var summary = (await CatalogService.LoadInstructorProfilesAsync(db.Users.Where(u => u.Id == instructorId), ct)).First();

        var reviews = db.Reviews.AsNoTracking()
            .Where(r => !r.IsHidden && r.Course.InstructorId == instructorId && !r.Course.IsDeleted);
        var distributionRows = await reviews.GroupBy(r => r.Rating).Select(g => new { Rating = (int)g.Key, Count = g.Count() }).ToListAsync(ct);
        var distribution = Enumerable.Range(1, 5).ToDictionary(i => i, i => distributionRows.FirstOrDefault(d => d.Rating == i)?.Count ?? 0);
        var recent = await reviews.Where(r => r.Comment != null && r.Comment != "")
            .OrderByDescending(r => r.CreatedAt).Take(6)
            .Select(r => new InstructorReviewDto(r.Id, r.User.FullName, r.User.AvatarUrl, r.Rating, r.Comment, r.CreatedAt,
                r.Course.Title, r.Course.Slug, r.InstructorReply))
            .ToListAsync(ct);

        var certificates = await db.Certificates.CountAsync(c => !c.IsRevoked && c.Course.InstructorId == instructorId, ct);

        return new InstructorDetailDto(
            user.Id, user.FullName, user.AvatarUrl, user.Headline, user.Bio,
            summary.TotalCourses, summary.TotalStudents, summary.TotalReviews, summary.AverageRating,
            user.IsVerifiedInstructor, user.InstructorVerifiedAt, user.CreatedAt, user.YearsOfExperience,
            SplitExpertise(user.Expertise), LinksOf(user), MapExperiences(user.Experiences),
            certificates, distribution, recent, await GetResponseStatsAsync(instructorId, ct));
    }

    public async Task<Result<InstructorProfileEditDto>> GetMineAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.Experiences).FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user is null ? Error.NotFound("Không tìm thấy tài khoản.") : ToEdit(user);
    }

    public async Task<Result<InstructorProfileEditDto>> UpdateMineAsync(Guid userId, UpdateInstructorProfileRequest r, CancellationToken ct = default)
    {
        var user = await db.Users.Include(u => u.Experiences).FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Error.NotFound("Không tìm thấy tài khoản.");

        user.AvatarUrl = Clean(r.AvatarUrl);
        user.Headline = Clean(r.Headline);
        user.Bio = Clean(r.Bio);
        user.YearsOfExperience = r.YearsOfExperience;
        var tags = (r.Expertise ?? []).Select(t => t.Trim()).Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(15).ToList();
        user.Expertise = tags.Count == 0 ? null : string.Join(';', tags);
        user.WebsiteUrl = Clean(r.WebsiteUrl);
        user.LinkedInUrl = Clean(r.LinkedInUrl);
        user.YoutubeUrl = Clean(r.YoutubeUrl);
        user.FacebookUrl = Clean(r.FacebookUrl);
        user.UpdatedAt = clock.UtcNow;

        // Thay toàn bộ danh sách kinh nghiệm theo thứ tự gửi lên
        db.InstructorExperiences.RemoveRange(user.Experiences);
        var order = 0;
        foreach (var e in r.Experiences ?? [])
        {
            db.InstructorExperiences.Add(new InstructorExperience
            {
                UserId = user.Id, Type = e.Type, Title = e.Title.Trim(), Organization = Clean(e.Organization),
                StartYear = e.StartYear, EndYear = e.EndYear, Description = Clean(e.Description),
                CredentialUrl = Clean(e.CredentialUrl), SortOrder = order++,
            });
        }
        await db.SaveChangesAsync(ct);
        return await GetMineAsync(userId, ct);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<bool> IsInstructorAsync(Guid userId, CancellationToken ct)
    {
        var role = Roles.Instructor.ToUpperInvariant();
        return await (from ur in db.Set<IdentityUserRole<Guid>>()
                      join r in db.Set<AppRole>() on ur.RoleId equals r.Id
                      where ur.UserId == userId && r.NormalizedName == role
                      select ur).AnyAsync(ct);
    }

    /// <summary>
    /// Với mỗi cuộc trao đổi người học mở trong 12 tháng: giảng viên có trả lời không, và sau bao lâu
    /// (từ tin nhắn đầu tiên của người học tới tin trả lời đầu tiên của giảng viên).
    /// </summary>
    private async Task<ResponseStatsDto?> GetResponseStatsAsync(Guid instructorId, CancellationToken ct)
    {
        var since = clock.UtcNow.AddMonths(-12);
        var messages = await db.Messages.AsNoTracking()
            .Where(m => m.Conversation.InstructorId == instructorId && m.Conversation.CreatedAt >= since)
            .Select(m => new { m.ConversationId, m.SenderId, m.CreatedAt })
            .ToListAsync(ct);

        var replyHours = new List<double>();
        var asked = 0;
        foreach (var g in messages.GroupBy(m => m.ConversationId))
        {
            var ordered = g.OrderBy(m => m.CreatedAt).ToList();
            var firstQuestion = ordered.FirstOrDefault(m => m.SenderId != instructorId);
            if (firstQuestion is null) continue;
            asked++;
            var reply = ordered.FirstOrDefault(m => m.SenderId == instructorId && m.CreatedAt >= firstQuestion.CreatedAt);
            if (reply is not null) replyHours.Add((reply.CreatedAt - firstQuestion.CreatedAt).TotalHours);
        }

        if (asked < MinConversationsForResponseStats) return null;
        double? median = null;
        if (replyHours.Count > 0)
        {
            replyHours.Sort();
            var mid = replyHours.Count / 2;
            median = Math.Round(replyHours.Count % 2 == 1 ? replyHours[mid] : (replyHours[mid - 1] + replyHours[mid]) / 2, 1);
        }
        return new ResponseStatsDto(asked, (int)Math.Round(100.0 * replyHours.Count / asked), median);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static IReadOnlyList<string> SplitExpertise(string? s) =>
        string.IsNullOrWhiteSpace(s) ? [] : s.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static InstructorLinksDto LinksOf(AppUser u) => new(u.WebsiteUrl, u.LinkedInUrl, u.YoutubeUrl, u.FacebookUrl);

    private static IReadOnlyList<InstructorExperienceDto> MapExperiences(IEnumerable<InstructorExperience> items) =>
        items.OrderBy(e => e.SortOrder)
            .Select(e => new InstructorExperienceDto(e.Id, e.Type, e.Title, e.Organization, e.StartYear, e.EndYear, e.Description, e.CredentialUrl))
            .ToList();

    private static InstructorProfileEditDto ToEdit(AppUser u) => new(
        u.FullName, u.AvatarUrl, u.Headline, u.Bio, u.YearsOfExperience, SplitExpertise(u.Expertise), LinksOf(u),
        MapExperiences(u.Experiences), u.IsVerifiedInstructor, u.InstructorVerifiedAt);
}
