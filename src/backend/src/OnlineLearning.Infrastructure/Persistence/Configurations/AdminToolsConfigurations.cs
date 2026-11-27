using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Infrastructure.Persistence.Configurations;

public class InstructorApplicationConfiguration : IEntityTypeConfiguration<InstructorApplication>
{
    public void Configure(EntityTypeBuilder<InstructorApplication> b)
    {
        b.ToTable("InstructorApplications");
        b.Property(x => x.Headline).HasMaxLength(200).IsRequired();
        b.Property(x => x.Bio).HasMaxLength(2000).IsRequired();
        b.Property(x => x.Expertise).HasMaxLength(500).IsRequired();
        b.Property(x => x.ProposedTopic).HasMaxLength(500).IsRequired();
        b.Property(x => x.PortfolioUrl).HasMaxLength(500);
        b.Property(x => x.AdminNote).HasMaxLength(500);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20)
            .HasDefaultValue(InstructorApplicationStatus.Pending).HasSentinel(InstructorApplicationStatus.Pending);
        // Mỗi người chỉ có một hồ sơ đang chờ duyệt
        b.HasIndex(x => x.UserId).IsUnique().HasFilter("[Status] = 'Pending'").HasDatabaseName("IX_InstructorApplications_UserId_Pending");
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> b)
    {
        b.ToTable("AdminAuditLogs");
        b.Property(x => x.ActorName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Action).HasMaxLength(100).IsUnicode(false).IsRequired();
        b.Property(x => x.Summary).HasMaxLength(300).IsRequired();
        b.Property(x => x.HttpMethod).HasMaxLength(10).IsUnicode(false).IsRequired();
        b.Property(x => x.Path).HasMaxLength(300).IsRequired();
        b.Property(x => x.Payload).HasMaxLength(2000);
        b.Property(x => x.IpAddress).HasMaxLength(64).IsUnicode(false);
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => new { x.ActorId, x.CreatedAt });
        b.HasIndex(x => new { x.Action, x.CreatedAt });
    }
}

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> b)
    {
        b.ToTable("SystemSettings");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(100).IsUnicode(false);
        b.Property(x => x.Value).HasMaxLength(500).IsRequired();
    }
}

public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> b)
    {
        b.ToTable("Announcements");
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Message).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Link).HasMaxLength(300);
        b.Property(x => x.Audience).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => x.CreatedAt);
    }
}
