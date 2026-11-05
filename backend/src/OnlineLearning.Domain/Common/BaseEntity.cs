namespace OnlineLearning.Domain.Common;

/// <summary>Entity gốc: khóa chính Guid (EF Core sinh sequential GUID cho SQL Server).</summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; }
}

/// <summary>Entity có dấu thời gian tạo/sửa (gán tự động trong AppDbContext.SaveChangesAsync).</summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
