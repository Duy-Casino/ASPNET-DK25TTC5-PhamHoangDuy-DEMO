using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearning.Domain.Entities;

namespace OnlineLearning.Infrastructure.Persistence.Configurations;

public class UserPointsConfiguration : IEntityTypeConfiguration<UserPoints>
{
    public void Configure(EntityTypeBuilder<UserPoints> b)
    {
        b.ToTable("UserPoints", t =>
        {
            t.HasCheckConstraint("CK_UserPoints_Balance", "[Balance] >= 0");
            t.HasCheckConstraint("CK_UserPoints_Totals", "[TotalEarned] >= 0 AND [TotalSpent] >= 0");
        });
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasIndex(x => x.Balance);
        b.HasOne(x => x.User).WithOne().HasForeignKey<UserPoints>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PointTransactionConfiguration : IEntityTypeConfiguration<PointTransaction>
{
    public void Configure(EntityTypeBuilder<PointTransaction> b)
    {
        b.ToTable("PointTransactions");
        b.Property(x => x.RuleKey).HasMaxLength(50);
        b.Property(x => x.ReferenceKey).HasMaxLength(120).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500).IsRequired();
        // Chống cộng/trừ trùng: một tham chiếu chỉ áp dụng một lần cho mỗi người dùng
        b.HasIndex(x => new { x.UserId, x.ReferenceKey }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => new { x.RuleKey, x.CreatedAt });
        b.HasIndex(x => x.CreatedAt);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PointRuleConfiguration : IEntityTypeConfiguration<PointRule>
{
    public void Configure(EntityTypeBuilder<PointRule> b)
    {
        b.ToTable("PointRules", t => t.HasCheckConstraint("CK_PointRules_Points", "[Points] >= 0"));
        b.Property(x => x.Key).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.AmountUnit).HasPrecision(18, 2);
        b.HasIndex(x => x.Key).IsUnique();
    }
}

public class DailyCheckInConfiguration : IEntityTypeConfiguration<DailyCheckIn>
{
    public void Configure(EntityTypeBuilder<DailyCheckIn> b)
    {
        b.ToTable("DailyCheckIns");
        // Mỗi tài khoản điểm danh tối đa một lần mỗi ngày
        b.HasIndex(x => new { x.UserId, x.CheckInDate }).IsUnique();
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CourseShareConfiguration : IEntityTypeConfiguration<CourseShare>
{
    public void Configure(EntityTypeBuilder<CourseShare> b)
    {
        b.ToTable("CourseShares");
        b.Property(x => x.Token).HasMaxLength(32).IsRequired();
        b.Property(x => x.CreatorKey).HasMaxLength(64);
        b.HasIndex(x => x.Token).IsUnique();
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => new { x.CourseId, x.CreatedAt });
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class CourseShareOpenConfiguration : IEntityTypeConfiguration<CourseShareOpen>
{
    public void Configure(EntityTypeBuilder<CourseShareOpen> b)
    {
        b.ToTable("CourseShareOpens");
        b.Property(x => x.VisitorKey).HasMaxLength(64).IsRequired();
        b.HasIndex(x => new { x.CourseShareId, x.VisitorKey }).IsUnique();
        b.HasOne(x => x.CourseShare).WithMany(s => s.Opens).HasForeignKey(x => x.CourseShareId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PointRedemptionConfiguration : IEntityTypeConfiguration<PointRedemption>
{
    public void Configure(EntityTypeBuilder<PointRedemption> b)
    {
        b.ToTable("PointRedemptions", t => t.HasCheckConstraint("CK_PointRedemptions_Points", "[Points] > 0"));
        b.Property(x => x.VoucherValue).HasPrecision(18, 2);
        b.Property(x => x.CouponCode).HasMaxLength(50).IsRequired();
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => x.CouponId).IsUnique();
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Coupon).WithMany().HasForeignKey(x => x.CouponId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class ReferralCodeConfiguration : IEntityTypeConfiguration<ReferralCode>
{
    public void Configure(EntityTypeBuilder<ReferralCode> b)
    {
        b.ToTable("ReferralCodes");
        b.Property(x => x.Code).HasMaxLength(16).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasOne(x => x.User).WithOne().HasForeignKey<ReferralCode>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ReferralConfiguration : IEntityTypeConfiguration<Referral>
{
    public void Configure(EntityTypeBuilder<Referral> b)
    {
        b.ToTable("Referrals");
        // Mỗi người chỉ được giới thiệu bởi một người
        b.HasIndex(x => x.ReferredUserId).IsUnique();
        b.HasIndex(x => x.ReferrerId);
        b.HasOne(x => x.Referrer).WithMany().HasForeignKey(x => x.ReferrerId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.ReferredUser).WithMany().HasForeignKey(x => x.ReferredUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
