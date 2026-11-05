using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OnlineLearning.Domain.Entities;

namespace OnlineLearning.Application.Common.Interfaces;

/// <summary>Cổng truy cập dữ liệu cho tầng Application (triển khai: Infrastructure.AppDbContext).</summary>
public interface IAppDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Category> Categories { get; }
    DbSet<Course> Courses { get; }
    DbSet<CourseSection> CourseSections { get; }
    DbSet<Lesson> Lessons { get; }
    DbSet<LessonResource> LessonResources { get; }
    DbSet<Enrollment> Enrollments { get; }
    DbSet<LessonProgress> LessonProgress { get; }
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Coupon> Coupons { get; }
    DbSet<Quiz> Quizzes { get; }
    DbSet<Question> Questions { get; }
    DbSet<AnswerOption> AnswerOptions { get; }
    DbSet<QuizAttempt> QuizAttempts { get; }
    DbSet<QuizAnswer> QuizAnswers { get; }
    DbSet<Certificate> Certificates { get; }
    DbSet<Review> Reviews { get; }
    DbSet<Notification> Notifications { get; }

    DatabaseFacade Database { get; }

    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
