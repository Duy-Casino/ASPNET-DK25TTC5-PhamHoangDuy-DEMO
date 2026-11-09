using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OnlineLearning.Application.Features.Admin;
using OnlineLearning.Application.Features.Auth;
using OnlineLearning.Application.Features.Categories;
using OnlineLearning.Application.Features.Courses;
using OnlineLearning.Application.Features.Notifications;
using OnlineLearning.Application.Features.Orders;

namespace OnlineLearning.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IInstructorCourseService, InstructorCourseService>();
        services.AddScoped<ICourseModerationService, CourseModerationService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IOrderFulfillmentService, OrderFulfillmentService>();
        services.AddScoped<ICouponService, CouponService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<Features.Learning.ILearningService, Features.Learning.LearningService>();
        services.AddScoped<Features.Quizzes.IQuizAuthoringService, Features.Quizzes.QuizAuthoringService>();
        services.AddScoped<Features.Quizzes.IQuizTakingService, Features.Quizzes.QuizTakingService>();
        services.AddScoped<Features.Dashboards.IDashboardService, Features.Dashboards.DashboardService>();
        services.AddScoped<Features.Payments.IPaymentService, Features.Payments.PaymentService>();
        services.AddScoped<Features.Certificates.ICertificateService, Features.Certificates.CertificateService>();
        services.AddScoped<Features.Instructors.IInstructorProfileService, Features.Instructors.InstructorProfileService>();
        services.AddScoped<Features.Messaging.IMessagingService, Features.Messaging.MessagingService>();
        services.AddScoped<Features.Rewards.IRewardService, Features.Rewards.RewardService>();
        services.AddScoped<Features.Rewards.IRewardAdminService, Features.Rewards.RewardAdminService>();

        return services;
    }
}
