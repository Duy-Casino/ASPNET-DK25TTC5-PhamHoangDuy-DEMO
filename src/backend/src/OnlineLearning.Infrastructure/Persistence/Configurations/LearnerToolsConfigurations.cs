using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearning.Domain.Entities;

namespace OnlineLearning.Infrastructure.Persistence.Configurations;

public class LessonQuestionConfiguration : IEntityTypeConfiguration<LessonQuestion>
{
    public void Configure(EntityTypeBuilder<LessonQuestion> b)
    {
        b.ToTable("LessonQuestions");
        b.Property(x => x.Content).HasMaxLength(2000).IsRequired();
        b.HasIndex(x => new { x.LessonId, x.LastActivityAt });
        b.HasIndex(x => new { x.CourseId, x.IsResolved });
        b.HasIndex(x => x.UserId);
        b.HasOne(x => x.Lesson).WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class LessonAnswerConfiguration : IEntityTypeConfiguration<LessonAnswer>
{
    public void Configure(EntityTypeBuilder<LessonAnswer> b)
    {
        b.ToTable("LessonAnswers");
        b.Property(x => x.Content).HasMaxLength(2000).IsRequired();
        b.HasIndex(x => new { x.QuestionId, x.CreatedAt });
        b.HasOne(x => x.Question).WithMany(q => q.Answers).HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class LessonNoteConfiguration : IEntityTypeConfiguration<LessonNote>
{
    public void Configure(EntityTypeBuilder<LessonNote> b)
    {
        b.ToTable("LessonNotes", t => t.HasCheckConstraint("CK_LessonNotes_Position", "[PositionSeconds] IS NULL OR [PositionSeconds] >= 0"));
        b.Property(x => x.Content).HasMaxLength(2000).IsRequired();
        b.HasIndex(x => new { x.UserId, x.LessonId });
        b.HasIndex(x => new { x.UserId, x.CourseId });
        // Xóa bài học thì xóa ghi chú của bài; một đường cascade duy nhất (SQL Server không cho nhiều đường)
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Lesson).WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Cascade);
    }
}
