using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Common.Helpers;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Categories;

public sealed record CategoryDto(
    Guid Id, string Name, string Slug, string? Description, string? IconUrl, Guid? ParentId,
    int DisplayOrder, bool IsActive, int CourseCount, IReadOnlyList<CategoryDto> Children);

public sealed record SaveCategoryRequest(string Name, string? Slug, string? Description, string? IconUrl, Guid? ParentId, int DisplayOrder, bool IsActive = true);

public sealed class SaveCategoryRequestValidator : AbstractValidator<SaveCategoryRequest>
{
    public SaveCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Vui lòng nhập tên danh mục.").MaximumLength(100);
        RuleFor(x => x.Slug).MaximumLength(120).Matches("^[a-z0-9-]*$").WithMessage("Slug chỉ gồm a-z, 0-9 và dấu gạch ngang.");
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.IconUrl).MaximumLength(500);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetTreeAsync(bool includeInactive, CancellationToken ct = default);
    Task<Result<CategoryDto>> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Result<CategoryDto>> CreateAsync(SaveCategoryRequest request, CancellationToken ct = default);
    Task<Result<CategoryDto>> UpdateAsync(Guid id, SaveCategoryRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

public sealed class CategoryService(IAppDbContext db) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetTreeAsync(bool includeInactive, CancellationToken ct = default)
    {
        var rows = await db.Categories.AsNoTracking()
            .Where(c => includeInactive || c.IsActive)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Select(c => new
            {
                c.Id, c.Name, c.Slug, c.Description, c.IconUrl, c.ParentId, c.DisplayOrder, c.IsActive,
                CourseCount = c.Courses.Count(x => x.Status == CourseStatus.Published && !x.IsDeleted),
            })
            .ToListAsync(ct);

        CategoryDto Build(Guid id)
        {
            var r = rows.First(x => x.Id == id);
            var children = rows.Where(x => x.ParentId == id).Select(x => Build(x.Id)).ToList();
            return new CategoryDto(r.Id, r.Name, r.Slug, r.Description, r.IconUrl, r.ParentId, r.DisplayOrder, r.IsActive,
                r.CourseCount + children.Sum(c => c.CourseCount), children);
        }

        var ids = rows.Select(r => r.Id).ToHashSet();
        return rows.Where(r => r.ParentId is null || !ids.Contains(r.ParentId.Value)).Select(r => Build(r.Id)).ToList();
    }

    public async Task<Result<CategoryDto>> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var tree = await GetTreeAsync(false, ct);
        var found = Flatten(tree).FirstOrDefault(c => c.Slug == slug);
        return found is null ? Error.NotFound("Không tìm thấy danh mục.") : found;
    }

    public async Task<Result<CategoryDto>> CreateAsync(SaveCategoryRequest request, CancellationToken ct = default)
    {
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? SlugHelper.Slugify(request.Name) : request.Slug;
        if (await db.Categories.AnyAsync(c => c.Slug == slug, ct)) return Error.Conflict("Slug danh mục đã tồn tại.");
        if (request.ParentId is { } pid && !await db.Categories.AnyAsync(c => c.Id == pid && c.ParentId == null, ct))
            return Error.Validation("Danh mục cha không hợp lệ (chỉ hỗ trợ 2 cấp).");

        var entity = new Category
        {
            Name = request.Name.Trim(), Slug = slug, Description = request.Description, IconUrl = request.IconUrl,
            ParentId = request.ParentId, DisplayOrder = request.DisplayOrder, IsActive = request.IsActive,
        };
        db.Categories.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<Result<CategoryDto>> UpdateAsync(Guid id, SaveCategoryRequest request, CancellationToken ct = default)
    {
        var entity = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return Error.NotFound("Không tìm thấy danh mục.");

        var slug = string.IsNullOrWhiteSpace(request.Slug) ? SlugHelper.Slugify(request.Name) : request.Slug;
        if (await db.Categories.AnyAsync(c => c.Slug == slug && c.Id != id, ct)) return Error.Conflict("Slug danh mục đã tồn tại.");
        if (request.ParentId is { } pid)
        {
            if (pid == id) return Error.Validation("Danh mục không thể là cha của chính nó.");
            if (!await db.Categories.AnyAsync(c => c.Id == pid && c.ParentId == null, ct))
                return Error.Validation("Danh mục cha không hợp lệ (chỉ hỗ trợ 2 cấp).");
            if (await db.Categories.AnyAsync(c => c.ParentId == id, ct))
                return Error.Validation("Danh mục đang có danh mục con nên không thể đặt làm cấp 2.");
        }

        entity.Name = request.Name.Trim();
        entity.Slug = slug;
        entity.Description = request.Description;
        entity.IconUrl = request.IconUrl;
        entity.ParentId = request.ParentId;
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return Result.Failure(Error.NotFound("Không tìm thấy danh mục."));
        if (await db.Categories.AnyAsync(c => c.ParentId == id, ct))
            return Result.Failure(Error.Conflict("Danh mục còn danh mục con, không thể xóa."));
        if (await db.Courses.AnyAsync(c => c.CategoryId == id, ct))
            return Result.Failure(Error.Conflict("Danh mục đang có khóa học, hãy ẩn danh mục thay vì xóa."));

        db.Categories.Remove(entity);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static CategoryDto ToDto(Category c) =>
        new(c.Id, c.Name, c.Slug, c.Description, c.IconUrl, c.ParentId, c.DisplayOrder, c.IsActive, 0, []);

    private static IEnumerable<CategoryDto> Flatten(IEnumerable<CategoryDto> nodes) =>
        nodes.SelectMany(n => new[] { n }.Concat(Flatten(n.Children)));
}
