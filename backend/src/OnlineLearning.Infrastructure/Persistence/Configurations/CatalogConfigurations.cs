using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("Categories");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.IconUrl).HasMaxLength(500);
        b.HasIndex(x => x.Slug).IsUnique();

        b.HasOne(x => x.Parent).WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> b)
    {
        b.ToTable("Courses", t =>
        {
            t.HasCheckConstraint("CK_Courses_Price", "[Price] >= 0");
            t.HasCheckConstraint("CK_Courses_DiscountPrice", "[DiscountPrice] IS NULL OR ([DiscountPrice] >= 0 AND [DiscountPrice] <= [Price])");
            t.HasCheckConstraint("CK_Courses_Rating", "[AverageRating] BETWEEN 0 AND 5");
        });

        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(220).IsRequired();
        b.Property(x => x.ShortDescription).HasMaxLength(500);
        b.Property(x => x.ThumbnailUrl).HasMaxLength(500);
        b.Property(x => x.PreviewVideoUrl).HasMaxLength(500);
        b.Property(x => x.Language).HasMaxLength(10).HasDefaultValue("vi");
        b.Property(x => x.RejectionReason).HasMaxLength(1000);
        b.Property(x => x.Status).HasDefaultValue(CourseStatus.Draft).HasSentinel(CourseStatus.Draft);
        b.Property(x => x.AverageRating).HasPrecision(3, 2);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Ignore(x => x.EffectivePrice);
        b.Ignore(x => x.IsEditable);

        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => new { x.Status, x.PublishedAt });
        b.HasIndex(x => x.EnrollmentCount);

        b.HasOne(x => x.Instructor).WithMany(u => u.TaughtCourses)
            .HasForeignKey(x => x.InstructorId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Category).WithMany(c => c.Courses)
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class CourseSectionConfiguration : IEntityTypeConfiguration<CourseSection>
{
    public void Configure(EntityTypeBuilder<CourseSection> b)
    {
        b.ToTable("CourseSections");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasIndex(x => new { x.CourseId, x.SortOrder });

        b.HasOne(x => x.Course).WithMany(c => c.Sections)
            .HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> b)
    {
        b.ToTable("Lessons", t => t.HasCheckConstraint("CK_Lessons_Duration", "[DurationSeconds] >= 0"));
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.VideoUrl).HasMaxLength(500);
        b.Property(x => x.VideoPublicId).HasMaxLength(300);
        b.Property(x => x.IsRequired).HasDefaultValue(true).HasSentinel(true);
        b.HasIndex(x => new { x.SectionId, x.SortOrder });

        b.HasOne(x => x.Section).WithMany(s => s.Lessons)
            .HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class LessonResourceConfiguration : IEntityTypeConfiguration<LessonResource>
{
    public void Configure(EntityTypeBuilder<LessonResource> b)
    {
        b.ToTable("LessonResources", t => t.HasCheckConstraint("CK_LessonResources_FileSize", "[FileSize] >= 0"));
        b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.FileUrl).HasMaxLength(500).IsRequired();
        b.Property(x => x.PublicId).HasMaxLength(300);
        b.Property(x => x.ContentType).HasMaxLength(100);

        b.HasOne(x => x.Lesson).WithMany(l => l.Resources)
            .HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> b)
    {
        b.ToTable("Reviews", t => t.HasCheckConstraint("CK_Reviews_Rating", "[Rating] BETWEEN 1 AND 5"));
        b.Property(x => x.Comment).HasMaxLength(2000);
        b.Property(x => x.InstructorReply).HasMaxLength(2000);
        b.HasIndex(x => new { x.CourseId, x.UserId }).IsUnique();
        b.HasIndex(x => new { x.CourseId, x.CreatedAt });

        b.HasOne(x => x.Course).WithMany(c => c.Reviews)
            .HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.User).WithMany(u => u.Reviews)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
