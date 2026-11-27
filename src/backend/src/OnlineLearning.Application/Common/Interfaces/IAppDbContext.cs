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
    DbSet<InstructorExperience> InstructorExperiences { get; }
    DbSet<Conversation> Conversations { get; }
    DbSet<Message> Messages { get; }
    DbSet<UserPoints> UserPoints { get; }
    DbSet<PointTransaction> PointTransactions { get; }
    DbSet<PointRule> PointRules { get; }
    DbSet<DailyCheckIn> DailyCheckIns { get; }
    DbSet<CourseShare> CourseShares { get; }
    DbSet<CourseShareOpen> CourseShareOpens { get; }
    DbSet<PointRedemption> PointRedemptions { get; }
    DbSet<ReferralCode> ReferralCodes { get; }
    DbSet<Referral> Referrals { get; }
    DbSet<InstructorEarning> InstructorEarnings { get; }
    DbSet<InstructorPayout> InstructorPayouts { get; }
    DbSet<RefundRequest> RefundRequests { get; }
    DbSet<WishlistItem> WishlistItems { get; }
    DbSet<Bundle> Bundles { get; }
    DbSet<BundleCourse> BundleCourses { get; }
    DbSet<CourseGift> CourseGifts { get; }
    DbSet<LessonQuestion> LessonQuestions { get; }
    DbSet<LessonAnswer> LessonAnswers { get; }
    DbSet<LessonNote> LessonNotes { get; }
    DbSet<QuizAttemptGrant> QuizAttemptGrants { get; }
    DbSet<LearningSupportRequest> LearningSupportRequests { get; }
    DbSet<LearningActionLog> LearningActionLogs { get; }

    DatabaseFacade Database { get; }

    Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker ChangeTracker { get; }

    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
