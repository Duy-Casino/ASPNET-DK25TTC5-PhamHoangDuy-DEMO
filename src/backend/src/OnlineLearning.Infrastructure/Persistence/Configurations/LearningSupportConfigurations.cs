using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Infrastructure.Persistence.Configurations;

public class QuizAttemptGrantConfiguration : IEntityTypeConfiguration<QuizAttemptGrant>
{
    public void Configure(EntityTypeBuilder<QuizAttemptGrant> b)
    {
        b.ToTable("QuizAttemptGrants", t => t.HasCheckConstraint("CK_QuizAttemptGrants_Extra", "[ExtraAttempts] BETWEEN 1 AND 10"));
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.HasIndex(x => new { x.QuizId, x.UserId });
        b.HasOne(x => x.Quiz).WithMany().HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class LearningSupportRequestConfiguration : IEntityTypeConfiguration<LearningSupportRequest>
{
    public void Configure(EntityTypeBuilder<LearningSupportRequest> b)
    {
        b.ToTable("LearningSupportRequests");
        b.Property(x => x.Message).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Response).HasMaxLength(1000);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20)
            .HasDefaultValue(SupportRequestStatus.Open).HasSentinel(SupportRequestStatus.Open);
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasIndex(x => new { x.UserId, x.CourseId });
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class LearningActionLogConfiguration : IEntityTypeConfiguration<LearningActionLog>
{
    public void Configure(EntityTypeBuilder<LearningActionLog> b)
    {
        b.ToTable("LearningActionLogs");
        b.Property(x => x.Action).HasMaxLength(50).IsUnicode(false).IsRequired();
        b.Property(x => x.Detail).HasMaxLength(500).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(500);
        b.HasIndex(x => new { x.LearnerId, x.CourseId, x.CreatedAt });
        b.HasIndex(x => x.CreatedAt);
    }
}
