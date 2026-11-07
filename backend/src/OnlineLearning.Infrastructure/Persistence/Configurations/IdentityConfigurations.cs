using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearning.Domain.Entities;

namespace OnlineLearning.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.AvatarUrl).HasMaxLength(500);
        b.Property(x => x.Headline).HasMaxLength(200);
        b.Property(x => x.PhoneNumber).HasMaxLength(20);
        b.Property(x => x.IsActive).HasDefaultValue(true).HasSentinel(true);
        b.Property(x => x.Expertise).HasMaxLength(500);
        b.Property(x => x.WebsiteUrl).HasMaxLength(300);
        b.Property(x => x.LinkedInUrl).HasMaxLength(300);
        b.Property(x => x.YoutubeUrl).HasMaxLength(300);
        b.Property(x => x.FacebookUrl).HasMaxLength(300);
    }
}

public class InstructorExperienceConfiguration : IEntityTypeConfiguration<InstructorExperience>
{
    public void Configure(EntityTypeBuilder<InstructorExperience> b)
    {
        b.ToTable("InstructorExperiences");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Organization).HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.CredentialUrl).HasMaxLength(300);
        b.HasIndex(x => new { x.UserId, x.SortOrder });
        b.HasOne(x => x.User).WithMany(u => u.Experiences)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> b)
    {
        b.ToTable("Conversations");
        b.HasIndex(x => new { x.StudentId, x.InstructorId }).IsUnique();
        b.HasIndex(x => new { x.InstructorId, x.LastMessageAt });
        b.HasIndex(x => new { x.StudentId, x.LastMessageAt });
        // NoAction: SQL Server không cho nhiều đường cascade cùng tới AspNetUsers
        b.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Instructor).WithMany().HasForeignKey(x => x.InstructorId).OnDelete(DeleteBehavior.NoAction);
        // Khóa học chỉ xóa mềm nên không cần cascade
        b.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> b)
    {
        b.ToTable("Messages");
        b.Property(x => x.Body).HasMaxLength(2000).IsRequired();
        b.HasIndex(x => new { x.ConversationId, x.CreatedAt });
        b.HasIndex(x => new { x.SenderId, x.CreatedAt });
        b.HasOne(x => x.Conversation).WithMany(c => c.Messages)
            .HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> b)
    {
        b.Property(x => x.Description).HasMaxLength(256);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens");
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        b.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
        b.Property(x => x.CreatedByIp).HasMaxLength(64);
        b.Property(x => x.RevokedByIp).HasMaxLength(64);
        b.HasIndex(x => x.TokenHash).IsUnique();

        b.HasOne(x => x.User).WithMany(u => u.RefreshTokens)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Message).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Link).HasMaxLength(500);
        b.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });

        b.HasOne(x => x.User).WithMany(u => u.Notifications)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
