using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Common.Helpers;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Features.Notifications;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Courses;

public interface IInstructorCourseService
{
    Task<PagedResult<InstructorCourseListItemDto>> ListMineAsync(Guid instructorId, CourseStatus? status, PagedQuery query, CancellationToken ct = default);
    Task<Result<CourseEditDto>> GetForEditAsync(Guid courseId, CancellationToken ct = default);
    Task<Result<CourseEditDto>> CreateAsync(SaveCourseRequest request, CancellationToken ct = default);
    Task<Result<CourseEditDto>> UpdateAsync(Guid courseId, SaveCourseRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid courseId, CancellationToken ct = default);
    Task<Result> SubmitForReviewAsync(Guid courseId, CancellationToken ct = default);
    Task<Result> UnpublishAsync(Guid courseId, CancellationToken ct = default);

    Task<Result<SectionEditDto>> AddSectionAsync(Guid courseId, SaveSectionRequest request, CancellationToken ct = default);
    Task<Result<SectionEditDto>> UpdateSectionAsync(Guid sectionId, SaveSectionRequest request, CancellationToken ct = default);
    Task<Result> DeleteSectionAsync(Guid sectionId, CancellationToken ct = default);
    Task<Result> ReorderSectionsAsync(Guid courseId, ReorderRequest request, CancellationToken ct = default);

    Task<Result<LessonEditDto>> AddLessonAsync(Guid sectionId, SaveLessonRequest request, CancellationToken ct = default);
    Task<Result<LessonEditDto>> UpdateLessonAsync(Guid lessonId, SaveLessonRequest request, CancellationToken ct = default);
    Task<Result> DeleteLessonAsync(Guid lessonId, CancellationToken ct = default);
    Task<Result> ReorderLessonsAsync(Guid sectionId, ReorderRequest request, CancellationToken ct = default);

    Task<Result<LessonResourceDto>> AddResourceAsync(Guid lessonId, AddResourceRequest request, CancellationToken ct = default);
    Task<Result> DeleteResourceAsync(Guid resourceId, CancellationToken ct = default);
}

/// <summary>
/// Quản lý khóa học của giảng viên. Mọi thao tác kiểm tra quyền sở hữu (Admin được phép thao tác thay).
/// Nội dung chỉ sửa được khi khóa học ở trạng thái Draft hoặc Rejected.
/// </summary>
public sealed class InstructorCourseService(
    IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock, INotificationService notifications) : IInstructorCourseService
{
    private static readonly Error NotFound = Error.NotFound("Không tìm thấy khóa học.");
    private static readonly Error Locked = Error.Conflict(
        "Khóa học đang chờ duyệt hoặc đã xuất bản nên không thể sửa nội dung. Hãy chuyển về bản nháp trước.");

    private Guid UserId => currentUser.UserId ?? throw new UnauthorizedAccessException();
    private bool IsAdmin => currentUser.IsInRole(Roles.Admin);

    public Task<PagedResult<InstructorCourseListItemDto>> ListMineAsync(Guid instructorId, CourseStatus? status, PagedQuery query, CancellationToken ct = default) =>
        db.Courses.AsNoTracking()
            .Where(c => c.InstructorId == instructorId && !c.IsDeleted && (status == null || c.Status == status))
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Select(c => new InstructorCourseListItemDto(c.Id, c.Title, c.Slug, c.ThumbnailUrl, c.Status, c.RejectionReason,
                c.Price, c.DiscountPrice, c.TotalLessons, c.EnrollmentCount, c.AverageRating, c.ReviewCount,
                c.CreatedAt, c.UpdatedAt, c.SubmittedAt, c.PublishedAt))
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

    public async Task<Result<CourseEditDto>> GetForEditAsync(Guid courseId, CancellationToken ct = default)
    {
        var course = await LoadFullAsync(courseId, ct);
        if (course is null || !CanManage(course)) return NotFound;
        return ToEditDto(course);
    }

    public async Task<Result<CourseEditDto>> CreateAsync(SaveCourseRequest request, CancellationToken ct = default)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId && c.IsActive, ct))
            return Error.Validation("Danh mục không tồn tại.");

        var course = new Course
        {
            InstructorId = UserId,
            Slug = await UniqueSlugAsync(request.Title, null, ct),
            Status = CourseStatus.Draft,
        };
        Apply(course, request);
        db.Courses.Add(course);
        await db.SaveChangesAsync(ct);
        return ToEditDto(course);
    }

    public async Task<Result<CourseEditDto>> UpdateAsync(Guid courseId, SaveCourseRequest request, CancellationToken ct = default)
    {
        var course = await LoadFullAsync(courseId, ct, tracking: true);
        if (course is null || !CanManage(course)) return NotFound;
        if (!course.IsEditable) return Locked;
        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId && c.IsActive, ct))
            return Error.Validation("Danh mục không tồn tại.");

        if (!string.Equals(course.Title, request.Title.Trim(), StringComparison.Ordinal) && course.PublishedAt is null)
            course.Slug = await UniqueSlugAsync(request.Title, course.Id, ct);

        Apply(course, request);
        await db.SaveChangesAsync(ct);
        return ToEditDto(course);
    }

    public async Task<Result> DeleteAsync(Guid courseId, CancellationToken ct = default)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted, ct);
        if (course is null || !CanManage(course)) return Result.Failure(NotFound);

        var hasHistory = await db.Enrollments.AnyAsync(e => e.CourseId == courseId, ct)
                         || await db.OrderItems.AnyAsync(i => i.CourseId == courseId, ct)
                         || await db.Coupons.AnyAsync(c => c.CourseId == courseId, ct)
                         || await db.Reviews.AnyAsync(r => r.CourseId == courseId, ct);

        var cartItems = await db.CartItems.Where(i => i.CourseId == courseId).ToListAsync(ct);
        foreach (var item in cartItems) db.CartItems.Remove(item);

        if (hasHistory)
        {
            // BR-11: đã có giao dịch/học viên → xóa mềm, học viên cũ vẫn học được
            course.IsDeleted = true;
            course.IsFeatured = false;
            course.Status = CourseStatus.Draft;
        }
        else
        {
            db.Courses.Remove(course);
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SubmitForReviewAsync(Guid courseId, CancellationToken ct = default)
    {
        var course = await LoadFullAsync(courseId, ct, tracking: true);
        if (course is null || !CanManage(course)) return Result.Failure(NotFound);
        if (!course.IsEditable) return Result.Failure(Error.Conflict("Chỉ gửi duyệt được khóa học ở trạng thái Bản nháp hoặc Bị từ chối."));

        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(course.ShortDescription)) problems.Add("mô tả ngắn");
        if (string.IsNullOrWhiteSpace(course.Description)) problems.Add("mô tả chi tiết");
        if (string.IsNullOrWhiteSpace(course.ThumbnailUrl)) problems.Add("ảnh đại diện");
        if (course.Sections.Count == 0 || course.Sections.All(s => s.Lessons.Count == 0)) problems.Add("ít nhất một bài học");
        if (course.Sections.Any(s => s.Lessons.Count == 0)) problems.Add("bài học cho mọi chương");
        if (problems.Count > 0)
            return Result.Failure(Error.Validation($"Khóa học còn thiếu: {string.Join(", ", problems)}."));

        course.Status = CourseStatus.Pending;
        course.SubmittedAt = clock.UtcNow;
        course.RejectionReason = null;

        await notifications.AddToRoleAsync(Roles.Admin, "Khóa học chờ duyệt",
            $"Khóa học \"{course.Title}\" vừa được gửi duyệt.", NotificationType.Course, "/admin/khoa-hoc?status=Pending", ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UnpublishAsync(Guid courseId, CancellationToken ct = default)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted, ct);
        if (course is null || !CanManage(course)) return Result.Failure(NotFound);
        if (course.Status is not (CourseStatus.Published or CourseStatus.Pending))
            return Result.Failure(Error.Conflict("Khóa học đã ở trạng thái bản nháp."));

        course.Status = CourseStatus.Draft;
        course.IsFeatured = false;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ---------------- Sections ----------------

    public async Task<Result<SectionEditDto>> AddSectionAsync(Guid courseId, SaveSectionRequest request, CancellationToken ct = default)
    {
        var course = await db.Courses.Include(c => c.Sections).FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted, ct);
        if (course is null || !CanManage(course)) return NotFound;
        if (!course.IsEditable) return Locked;

        var section = new CourseSection
        {
            CourseId = courseId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            SortOrder = course.Sections.Count == 0 ? 0 : course.Sections.Max(s => s.SortOrder) + 1,
        };
        db.CourseSections.Add(section);
        await db.SaveChangesAsync(ct);
        return new SectionEditDto(section.Id, section.Title, section.Description, section.SortOrder, []);
    }

    public async Task<Result<SectionEditDto>> UpdateSectionAsync(Guid sectionId, SaveSectionRequest request, CancellationToken ct = default)
    {
        var section = await db.CourseSections.Include(s => s.Course).Include(s => s.Lessons).ThenInclude(l => l.Resources)
            .FirstOrDefaultAsync(s => s.Id == sectionId, ct);
        if (section is null || section.Course.IsDeleted || !CanManage(section.Course)) return Error.NotFound("Không tìm thấy chương.");
        if (!section.Course.IsEditable) return Locked;

        section.Title = request.Title.Trim();
        section.Description = request.Description?.Trim();
        await db.SaveChangesAsync(ct);
        return ToSectionDto(section);
    }

    public async Task<Result> DeleteSectionAsync(Guid sectionId, CancellationToken ct = default)
    {
        var section = await db.CourseSections.Include(s => s.Course).Include(s => s.Lessons)
            .FirstOrDefaultAsync(s => s.Id == sectionId, ct);
        if (section is null || section.Course.IsDeleted || !CanManage(section.Course)) return Result.Failure(Error.NotFound("Không tìm thấy chương."));
        if (!section.Course.IsEditable) return Result.Failure(Locked);

        var lessonIds = section.Lessons.Select(l => l.Id).ToList();
        if (await db.LessonProgress.AnyAsync(p => lessonIds.Contains(p.LessonId), ct))
            return Result.Failure(Error.Conflict("Chương có bài học đã được học viên học, không thể xóa."));

        await DetachLastLessonAsync(lessonIds, ct);
        var quizzes = await db.Quizzes.Where(q => q.SectionId == sectionId).ToListAsync(ct);
        foreach (var q in quizzes) q.SectionId = null;

        db.CourseSections.Remove(section);
        await db.SaveChangesAsync(ct);
        await RecalculateCourseTotalsAsync(section.CourseId, ct);
        return Result.Success();
    }

    public async Task<Result> ReorderSectionsAsync(Guid courseId, ReorderRequest request, CancellationToken ct = default)
    {
        var course = await db.Courses.Include(c => c.Sections).FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted, ct);
        if (course is null || !CanManage(course)) return Result.Failure(NotFound);
        if (!course.IsEditable) return Result.Failure(Locked);

        var ids = course.Sections.Select(s => s.Id).ToHashSet();
        if (!ids.SetEquals(request.OrderedIds)) return Result.Failure(Error.Validation("Danh sách chương không khớp."));

        for (var i = 0; i < request.OrderedIds.Count; i++)
            course.Sections.First(s => s.Id == request.OrderedIds[i]).SortOrder = i;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ---------------- Lessons ----------------

    public async Task<Result<LessonEditDto>> AddLessonAsync(Guid sectionId, SaveLessonRequest request, CancellationToken ct = default)
    {
        var section = await db.CourseSections.Include(s => s.Course).Include(s => s.Lessons)
            .FirstOrDefaultAsync(s => s.Id == sectionId, ct);
        if (section is null || section.Course.IsDeleted || !CanManage(section.Course)) return Error.NotFound("Không tìm thấy chương.");
        if (!section.Course.IsEditable) return Locked;

        var lesson = new Lesson
        {
            SectionId = sectionId,
            SortOrder = section.Lessons.Count == 0 ? 0 : section.Lessons.Max(l => l.SortOrder) + 1,
        };
        Apply(lesson, request);
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync(ct);
        await RecalculateCourseTotalsAsync(section.CourseId, ct);
        return ToLessonDto(lesson);
    }

    public async Task<Result<LessonEditDto>> UpdateLessonAsync(Guid lessonId, SaveLessonRequest request, CancellationToken ct = default)
    {
        var lesson = await db.Lessons.Include(l => l.Section).ThenInclude(s => s.Course).Include(l => l.Resources)
            .FirstOrDefaultAsync(l => l.Id == lessonId, ct);
        if (lesson is null || lesson.Section.Course.IsDeleted || !CanManage(lesson.Section.Course)) return Error.NotFound("Không tìm thấy bài học.");
        if (!lesson.Section.Course.IsEditable) return Locked;

        Apply(lesson, request);
        await db.SaveChangesAsync(ct);
        await RecalculateCourseTotalsAsync(lesson.Section.CourseId, ct);
        return ToLessonDto(lesson);
    }

    public async Task<Result> DeleteLessonAsync(Guid lessonId, CancellationToken ct = default)
    {
        var lesson = await db.Lessons.Include(l => l.Section).ThenInclude(s => s.Course)
            .FirstOrDefaultAsync(l => l.Id == lessonId, ct);
        if (lesson is null || lesson.Section.Course.IsDeleted || !CanManage(lesson.Section.Course)) return Result.Failure(Error.NotFound("Không tìm thấy bài học."));
        if (!lesson.Section.Course.IsEditable) return Result.Failure(Locked);
        if (await db.LessonProgress.AnyAsync(p => p.LessonId == lessonId, ct))
            return Result.Failure(Error.Conflict("Bài học đã có học viên học, không thể xóa. Hãy bỏ cờ bắt buộc thay vì xóa."));

        await DetachLastLessonAsync([lessonId], ct);
        var courseId = lesson.Section.CourseId;
        db.Lessons.Remove(lesson);
        await db.SaveChangesAsync(ct);
        await RecalculateCourseTotalsAsync(courseId, ct);
        return Result.Success();
    }

    public async Task<Result> ReorderLessonsAsync(Guid sectionId, ReorderRequest request, CancellationToken ct = default)
    {
        var section = await db.CourseSections.Include(s => s.Course).Include(s => s.Lessons)
            .FirstOrDefaultAsync(s => s.Id == sectionId, ct);
        if (section is null || section.Course.IsDeleted || !CanManage(section.Course)) return Result.Failure(Error.NotFound("Không tìm thấy chương."));
        if (!section.Course.IsEditable) return Result.Failure(Locked);

        var ids = section.Lessons.Select(l => l.Id).ToHashSet();
        if (!ids.SetEquals(request.OrderedIds)) return Result.Failure(Error.Validation("Danh sách bài học không khớp."));

        for (var i = 0; i < request.OrderedIds.Count; i++)
            section.Lessons.First(l => l.Id == request.OrderedIds[i]).SortOrder = i;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ---------------- Resources ----------------

    public async Task<Result<LessonResourceDto>> AddResourceAsync(Guid lessonId, AddResourceRequest request, CancellationToken ct = default)
    {
        var lesson = await db.Lessons.Include(l => l.Section).ThenInclude(s => s.Course)
            .FirstOrDefaultAsync(l => l.Id == lessonId, ct);
        if (lesson is null || lesson.Section.Course.IsDeleted || !CanManage(lesson.Section.Course)) return Error.NotFound("Không tìm thấy bài học.");
        if (!lesson.Section.Course.IsEditable) return Locked;

        var resource = new LessonResource
        {
            LessonId = lessonId,
            FileName = request.FileName.Trim(),
            // Tệp riêng tư (đã tải lên kho lưu trữ) chỉ giữ publicId; URL có chữ ký được tạo khi học viên mở bài
            FileUrl = string.IsNullOrWhiteSpace(request.PublicId) ? request.FileUrl : $"private://{request.PublicId}",
            PublicId = request.PublicId,
            ContentType = request.ContentType,
            FileSize = request.FileSize,
        };
        db.LessonResources.Add(resource);
        await db.SaveChangesAsync(ct);
        return new LessonResourceDto(resource.Id, resource.FileName, resource.FileUrl, resource.ContentType, resource.FileSize);
    }

    public async Task<Result> DeleteResourceAsync(Guid resourceId, CancellationToken ct = default)
    {
        var resource = await db.LessonResources.Include(r => r.Lesson).ThenInclude(l => l.Section).ThenInclude(s => s.Course)
            .FirstOrDefaultAsync(r => r.Id == resourceId, ct);
        if (resource is null || !CanManage(resource.Lesson.Section.Course)) return Result.Failure(Error.NotFound("Không tìm thấy tài liệu."));
        if (!resource.Lesson.Section.Course.IsEditable) return Result.Failure(Locked);

        db.LessonResources.Remove(resource);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ---------------- helpers ----------------

    private bool CanManage(Course course) => IsAdmin || course.InstructorId == currentUser.UserId;

    private Task<Course?> LoadFullAsync(Guid courseId, CancellationToken ct, bool tracking = false)
    {
        IQueryable<Course> q = db.Courses
            .Include(c => c.Sections.OrderBy(s => s.SortOrder))
                .ThenInclude(s => s.Lessons.OrderBy(l => l.SortOrder))
                    .ThenInclude(l => l.Resources)
            .AsSplitQuery();
        if (!tracking) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(c => c.Id == courseId && !c.IsDeleted, ct);
    }

    private async Task<string> UniqueSlugAsync(string title, Guid? excludeId, CancellationToken ct)
    {
        var baseSlug = SlugHelper.Slugify(title);
        if (string.IsNullOrEmpty(baseSlug)) baseSlug = "khoa-hoc";
        var slug = baseSlug;
        var i = 2;
        while (await db.Courses.AnyAsync(c => c.Slug == slug && c.Id != excludeId, ct))
            slug = $"{baseSlug}-{i++}";
        return slug;
    }

    private static void Apply(Course course, SaveCourseRequest r)
    {
        course.Title = r.Title.Trim();
        course.CategoryId = r.CategoryId;
        course.ShortDescription = r.ShortDescription?.Trim();
        course.Description = ContentSanitizer.SanitizeHtml(r.Description);
        course.Requirements = ContentSanitizer.ToJsonList(r.Requirements);
        course.Outcomes = ContentSanitizer.ToJsonList(r.Outcomes);
        course.ThumbnailUrl = r.ThumbnailUrl;
        course.PreviewVideoUrl = r.PreviewVideoUrl;
        course.Language = string.IsNullOrWhiteSpace(r.Language) ? "vi" : r.Language.Trim();
        course.Level = r.Level;
        course.Price = r.Price;
        course.DiscountPrice = r.DiscountPrice;
    }

    private static void Apply(Lesson lesson, SaveLessonRequest r)
    {
        lesson.Title = r.Title.Trim();
        lesson.Type = r.Type;
        lesson.Content = ContentSanitizer.SanitizeHtml(r.Content);
        lesson.VideoUrl = r.Type == LessonType.Video ? r.VideoUrl : null;
        lesson.VideoPublicId = r.Type == LessonType.Video ? r.VideoPublicId : null;
        lesson.DurationSeconds = r.DurationSeconds;
        lesson.IsPreview = r.IsPreview;
        lesson.IsRequired = r.IsRequired;
    }

    private async Task DetachLastLessonAsync(IReadOnlyCollection<Guid> lessonIds, CancellationToken ct)
    {
        var enrollments = await db.Enrollments.Where(e => e.LastLessonId != null && lessonIds.Contains(e.LastLessonId.Value)).ToListAsync(ct);
        foreach (var e in enrollments) e.LastLessonId = null;
    }

    private async Task RecalculateCourseTotalsAsync(Guid courseId, CancellationToken ct)
    {
        var course = await db.Courses.FirstAsync(c => c.Id == courseId, ct);
        var lessons = db.Lessons.Where(l => l.Section.CourseId == courseId);
        course.TotalLessons = await lessons.CountAsync(ct);
        course.TotalDurationSeconds = await lessons.SumAsync(l => l.DurationSeconds, ct);
        await db.SaveChangesAsync(ct);
    }

    private static CourseEditDto ToEditDto(Course c) => new(
        c.Id, c.Title, c.Slug, c.CategoryId, c.ShortDescription, c.Description,
        ContentSanitizer.FromJsonList(c.Requirements), ContentSanitizer.FromJsonList(c.Outcomes),
        c.ThumbnailUrl, c.PreviewVideoUrl, c.Language, c.Level, c.Price, c.DiscountPrice, c.Status, c.RejectionReason,
        c.IsEditable, c.InstructorId,
        c.Sections.OrderBy(s => s.SortOrder).Select(ToSectionDto).ToList());

    private static SectionEditDto ToSectionDto(CourseSection s) =>
        new(s.Id, s.Title, s.Description, s.SortOrder, s.Lessons.OrderBy(l => l.SortOrder).Select(ToLessonDto).ToList());

    private static LessonEditDto ToLessonDto(Lesson l) => new(
        l.Id, l.SectionId, l.Title, l.Type, l.Content, l.VideoUrl, l.VideoPublicId, l.DurationSeconds, l.SortOrder,
        l.IsPreview, l.IsRequired,
        l.Resources.Select(r => new LessonResourceDto(r.Id, r.FileName, r.FileUrl, r.ContentType, r.FileSize)).ToList());
}
