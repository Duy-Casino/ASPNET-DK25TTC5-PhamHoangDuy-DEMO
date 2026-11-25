using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Infrastructure.Persistence.Configurations;

public class InstructorEarningConfiguration : IEntityTypeConfiguration<InstructorEarning>
{
    public void Configure(EntityTypeBuilder<InstructorEarning> b)
    {
        b.ToTable("InstructorEarnings", t =>
        {
            t.HasCheckConstraint("CK_InstructorEarnings_Share", "[SharePercent] >= 0 AND [SharePercent] <= 100");
            t.HasCheckConstraint("CK_InstructorEarnings_Sign",
                "([Type] = 'Sale' AND [Amount] >= 0 AND [GrossAmount] >= 0) OR ([Type] = 'Refund' AND [Amount] <= 0 AND [GrossAmount] <= 0)");
        });

        b.Property(x => x.OrderCode).HasMaxLength(20).IsUnicode(false).IsRequired();
        b.Property(x => x.CourseTitle).HasMaxLength(200).IsRequired();

        // Mỗi khóa học trong đơn chỉ có tối đa một dòng bán và một dòng hoàn: IPN lặp hay hoàn tiền lặp không ghi sổ hai lần
        b.HasIndex(x => new { x.OrderItemId, x.Type }).IsUnique();
        b.HasIndex(x => new { x.InstructorId, x.AvailableAt });
        b.HasIndex(x => x.OrderId);

        b.HasOne(x => x.Instructor).WithMany()
            .HasForeignKey(x => x.InstructorId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Order).WithMany()
            .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.OrderItem).WithMany()
            .HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class InstructorPayoutConfiguration : IEntityTypeConfiguration<InstructorPayout>
{
    public void Configure(EntityTypeBuilder<InstructorPayout> b)
    {
        b.ToTable("InstructorPayouts", t => t.HasCheckConstraint("CK_InstructorPayouts_Amount", "[Amount] > 0"));

        b.Property(x => x.Status).HasDefaultValue(PayoutStatus.Requested).HasSentinel(PayoutStatus.Requested);
        b.Property(x => x.BankName).HasMaxLength(100).IsRequired();
        b.Property(x => x.BankAccountNumber).HasMaxLength(30).IsUnicode(false).IsRequired();
        b.Property(x => x.BankAccountName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.TransferReference).HasMaxLength(100);
        b.Property(x => x.AdminNote).HasMaxLength(500);

        // Mỗi giảng viên chỉ có một yêu cầu đang chờ xử lý (chống gửi trùng đồng thời)
        b.HasIndex(x => x.InstructorId).IsUnique().HasFilter("[Status] = 'Requested'")
            .HasDatabaseName("IX_InstructorPayouts_InstructorId_Open");
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasIndex(x => new { x.InstructorId, x.CreatedAt });

        b.HasOne(x => x.Instructor).WithMany()
            .HasForeignKey(x => x.InstructorId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class RefundRequestConfiguration : IEntityTypeConfiguration<RefundRequest>
{
    public void Configure(EntityTypeBuilder<RefundRequest> b)
    {
        b.ToTable("RefundRequests");

        b.Property(x => x.Status).HasDefaultValue(RefundRequestStatus.Pending).HasSentinel(RefundRequestStatus.Pending);
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.AdminNote).HasMaxLength(500);

        // Một đơn chỉ có một yêu cầu đang chờ xử lý
        b.HasIndex(x => x.OrderId).IsUnique().HasFilter("[Status] = 'Pending'")
            .HasDatabaseName("IX_RefundRequests_OrderId_Pending");
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasIndex(x => x.UserId);

        b.HasOne(x => x.Order).WithMany()
            .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
