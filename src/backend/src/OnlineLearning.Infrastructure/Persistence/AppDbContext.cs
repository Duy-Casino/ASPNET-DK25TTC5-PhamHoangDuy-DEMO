using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Domain.Common;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid>(options), IAppDbContext
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseSection> CourseSections => Set<CourseSection>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonResource> LessonResources => Set<LessonResource>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<LessonProgress> LessonProgress => Set<LessonProgress>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<AnswerOption> AnswerOptions => Set<AnswerOption>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
    public DbSet<QuizAnswer> QuizAnswers => Set<QuizAnswer>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<InstructorExperience> InstructorExperiences => Set<InstructorExperience>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<UserPoints> UserPoints => Set<UserPoints>();
    public DbSet<PointTransaction> PointTransactions => Set<PointTransaction>();
    public DbSet<PointRule> PointRules => Set<PointRule>();
    public DbSet<DailyCheckIn> DailyCheckIns => Set<DailyCheckIn>();
    public DbSet<CourseShare> CourseShares => Set<CourseShare>();
    public DbSet<CourseShareOpen> CourseShareOpens => Set<CourseShareOpen>();
    public DbSet<PointRedemption> PointRedemptions => Set<PointRedemption>();
    public DbSet<ReferralCode> ReferralCodes => Set<ReferralCode>();
    public DbSet<Referral> Referrals => Set<Referral>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Đổi tên bảng Identity: AspNetUsers -> Users ...
        builder.Entity<AppUser>().ToTable("Users");
        builder.Entity<AppRole>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        if (IsSqlite)
        {
            // Chỉ dùng cho kiểm thử tích hợp (SQLite in-memory): SQLite không tự sinh rowversion.
            foreach (var entity in builder.Model.GetEntityTypes())
            {
                var rowVersion = entity.FindProperty("RowVersion");
                if (rowVersion is not null)
                    builder.Entity(entity.ClrType).Property("RowVersion").ValueGeneratedNever().IsConcurrencyToken(false);
            }
        }
    }

    private bool IsSqlite => Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        if (IsSqlite)
            configurationBuilder.Properties<decimal>().HaveConversion<double>(); // SQLite không hỗ trợ SUM/ORDER BY trên decimal
        else
            configurationBuilder.Properties<decimal>().HavePrecision(18, 2); // Tiền VND mặc định decimal(18,2)

        // Mọi thời gian lưu UTC; khi đọc ra gắn Kind=Utc để JSON có hậu tố "Z" (client tự đổi sang giờ Việt Nam)
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();

        // Enum lưu dạng chuỗi
        configurationBuilder.Properties<CourseLevel>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<CourseStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<LessonType>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<EnrollmentStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<OrderStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<PaymentProvider>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<PaymentStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<DiscountType>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<QuestionType>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<NotificationType>().HaveConversion<string>().HaveMaxLength(20);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
                entry.Entity.CreatedAt = now;
            else if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = now;
        }

        foreach (var entry in ChangeTracker.Entries<AppUser>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
                entry.Entity.CreatedAt = now;
            else if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = now;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class UtcDateTimeConverter() : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

internal sealed class NullableUtcDateTimeConverter() : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
    v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()) : v,
    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
