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
