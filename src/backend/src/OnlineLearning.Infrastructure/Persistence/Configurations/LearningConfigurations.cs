using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Infrastructure.Persistence.Configurations;

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> b)
    {
        b.ToTable("Enrollments", t =>
            t.HasCheckConstraint("CK_Enrollments_Progress", "[ProgressPercent] BETWEEN 0 AND 100"));

        b.Property(x => x.ProgressPercent).HasPrecision(5, 2);
        b.Property(x => x.Status).HasDefaultValue(EnrollmentStatus.Active).HasSentinel(EnrollmentStatus.Active);

        // BR-03: không mua trùng / ghi danh trùng
        b.HasIndex(x => new { x.UserId, x.CourseId }).IsUnique();

        b.HasOne(x => x.User).WithMany(u => u.Enrollments)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Course).WithMany(c => c.Enrollments)
            .HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Order).WithMany()
            .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.LastLesson).WithMany()
            .HasForeignKey(x => x.LastLessonId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class LessonProgressConfiguration : IEntityTypeConfiguration<LessonProgress>
{
    public void Configure(EntityTypeBuilder<LessonProgress> b)
    {
        b.ToTable("LessonProgress");
        b.HasIndex(x => new { x.EnrollmentId, x.LessonId }).IsUnique();

        b.HasOne(x => x.Enrollment).WithMany(e => e.LessonProgresses)
            .HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Lesson).WithMany()
            .HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> b)
    {
        b.ToTable("Quizzes", t =>
        {
            t.HasCheckConstraint("CK_Quizzes_Passing", "[PassingScore] BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_Quizzes_MaxAttempts", "[MaxAttempts] IS NULL OR [MaxAttempts] > 0");
        });
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.PassingScore).HasPrecision(5, 2);

        b.HasOne(x => x.Course).WithMany(c => c.Quizzes)
            .HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Section).WithMany(s => s.Quizzes)
            .HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> b)
    {
        b.ToTable("Questions", t => t.HasCheckConstraint("CK_Questions_Points", "[Points] > 0"));
        b.Property(x => x.Content).HasMaxLength(2000).IsRequired();
        b.Property(x => x.Explanation).HasMaxLength(2000);
        b.Property(x => x.Points).HasPrecision(5, 2);

        b.HasOne(x => x.Quiz).WithMany(q => q.Questions)
            .HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AnswerOptionConfiguration : IEntityTypeConfiguration<AnswerOption>
{
    public void Configure(EntityTypeBuilder<AnswerOption> b)
    {
        b.ToTable("AnswerOptions");
        b.Property(x => x.Content).HasMaxLength(1000).IsRequired();

        b.HasOne(x => x.Question).WithMany(q => q.Options)
            .HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class QuizAttemptConfiguration : IEntityTypeConfiguration<QuizAttempt>
{
    public void Configure(EntityTypeBuilder<QuizAttempt> b)
    {
        b.ToTable("QuizAttempts");
        b.Property(x => x.Score).HasPrecision(7, 2);
        b.Property(x => x.MaxScore).HasPrecision(7, 2);
        b.Property(x => x.ScorePercent).HasPrecision(5, 2);
        b.HasIndex(x => new { x.QuizId, x.UserId, x.AttemptNumber }).IsUnique();

        b.HasOne(x => x.Quiz).WithMany(q => q.Attempts)
            .HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class QuizAnswerConfiguration : IEntityTypeConfiguration<QuizAnswer>
{
    public void Configure(EntityTypeBuilder<QuizAnswer> b)
    {
        b.ToTable("QuizAnswers");
        b.HasIndex(x => new { x.AttemptId, x.QuestionId, x.AnswerOptionId }).IsUnique();

        b.HasOne(x => x.Attempt).WithMany(a => a.Answers)
            .HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Question).WithMany()
            .HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.AnswerOption).WithMany()
            .HasForeignKey(x => x.AnswerOptionId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class CertificateConfiguration : IEntityTypeConfiguration<Certificate>
{
    public void Configure(EntityTypeBuilder<Certificate> b)
    {
        b.ToTable("Certificates");
        b.Property(x => x.CertificateCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        b.Property(x => x.StudentName).HasMaxLength(150).IsRequired();
        b.Property(x => x.CourseTitle).HasMaxLength(200).IsRequired();
        b.Property(x => x.InstructorName).HasMaxLength(150).IsRequired();
        b.Property(x => x.FinalScore).HasPrecision(5, 2);
        b.Property(x => x.PdfUrl).HasMaxLength(500);
        b.Property(x => x.RevokedReason).HasMaxLength(500);

        b.HasIndex(x => x.CertificateCode).IsUnique();
        b.HasIndex(x => new { x.UserId, x.CourseId }).IsUnique();
        b.HasIndex(x => x.EnrollmentId).IsUnique();

        b.HasOne(x => x.User).WithMany(u => u.Certificates)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Course).WithMany()
            .HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Enrollment).WithOne(e => e.Certificate)
            .HasForeignKey<Certificate>(x => x.EnrollmentId).OnDelete(DeleteBehavior.NoAction);
    }
}
