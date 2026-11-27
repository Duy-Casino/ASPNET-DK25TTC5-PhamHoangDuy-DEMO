using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Infrastructure.Persistence.Configurations;

public class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> b)
    {
        b.ToTable("WishlistItems");
        b.HasIndex(x => new { x.UserId, x.CourseId }).IsUnique();
        b.HasIndex(x => x.CourseId);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class BundleConfiguration : IEntityTypeConfiguration<Bundle>
{
    public void Configure(EntityTypeBuilder<Bundle> b)
    {
        b.ToTable("Bundles", t => t.HasCheckConstraint("CK_Bundles_Price", "[Price] >= 0"));
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(220).IsUnicode(false).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.ThumbnailUrl).HasMaxLength(500);
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => x.IsActive);
    }
}

public class BundleCourseConfiguration : IEntityTypeConfiguration<BundleCourse>
{
    public void Configure(EntityTypeBuilder<BundleCourse> b)
    {
        b.ToTable("BundleCourses");
        b.HasIndex(x => new { x.BundleId, x.CourseId }).IsUnique();
        b.HasIndex(x => x.CourseId);
        b.HasOne(x => x.Bundle).WithMany(x => x.Courses).HasForeignKey(x => x.BundleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class CourseGiftConfiguration : IEntityTypeConfiguration<CourseGift>
{
    public void Configure(EntityTypeBuilder<CourseGift> b)
    {
        b.ToTable("CourseGifts");
        b.Property(x => x.RecipientEmail).HasMaxLength(256).IsRequired();
        b.Property(x => x.RecipientName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Message).HasMaxLength(500);
        b.Property(x => x.Code).HasMaxLength(20).IsUnicode(false).IsRequired();
        b.Property(x => x.Status).HasDefaultValue(GiftStatus.Pending).HasSentinel(GiftStatus.Pending);

        b.HasIndex(x => x.OrderId).IsUnique();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.RecipientEmail);
        b.HasIndex(x => x.SenderId);

        b.HasOne(x => x.Order).WithOne(o => o.Gift).HasForeignKey<CourseGift>(x => x.OrderId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.NoAction);
    }
}
