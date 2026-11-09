using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Options;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Infrastructure.BackgroundJobs;
using OnlineLearning.Infrastructure.Email;
using OnlineLearning.Infrastructure.Identity;
using OnlineLearning.Infrastructure.Payments;
using OnlineLearning.Infrastructure.Persistence;
using OnlineLearning.Infrastructure.Storage;

namespace OnlineLearning.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool configureDatabase = true)
    {
        if (configureDatabase)
        {
            if (IsSqliteDemo(configuration))
            {
                // Chế độ demo không cần SQL Server: file SQLite, tạo schema bằng EnsureCreated (không dùng migration).
                var sqlite = configuration.GetConnectionString("SqliteDemo") ?? "Data Source=eduviet-demo.db";
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(sqlite));
            }
            else
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("Thiếu ConnectionStrings:DefaultConnection.");

                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer(connectionString, sql =>
                        sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
            }
        }

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.SecretKey.Length >= 32, "Jwt:SecretKey phải dài ít nhất 32 ký tự (đặt bằng User Secrets hoặc biến môi trường).")
            .ValidateOnStart();
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<Application.Features.Certificates.ICertificatePdfGenerator, Certificates.QuestPdfCertificateGenerator>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        // ---------- Thanh toán ----------
        services.Configure<Application.Features.Payments.PaymentOptions>(configuration.GetSection(Application.Features.Payments.PaymentOptions.SectionName));
        services.Configure<VnPayOptions>(configuration.GetSection(VnPayOptions.SectionName));
        services.Configure<MoMoOptions>(configuration.GetSection(MoMoOptions.SectionName));
        services.Configure<TestGatewayOptions>(configuration.GetSection(TestGatewayOptions.SectionName));
        services.AddSingleton<VnPayGateway>();
        services.AddSingleton<TestPaymentGateway>();
        services.AddHttpClient<MoMoGateway>(c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<VnPayGateway>());
        services.AddTransient<IPaymentGateway>(sp => sp.GetRequiredService<MoMoGateway>());
        services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<TestPaymentGateway>());

        // ---------- Lưu trữ tệp: Cloudinary nếu có khóa, ngược lại lưu cục bộ ----------
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<CloudinaryOptions>(configuration.GetSection(CloudinaryOptions.SectionName));
        services.AddSingleton<MediaUrlSigner>();
        services.AddSingleton<LocalFileStorage>();
        var cloudinary = configuration.GetSection(CloudinaryOptions.SectionName).Get<CloudinaryOptions>() ?? new CloudinaryOptions();
        if (cloudinary.IsConfigured)
            services.AddSingleton<IFileStorage, CloudinaryFileStorage>();
        else
            services.AddSingleton<IFileStorage>(sp => sp.GetRequiredService<LocalFileStorage>());

        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<VietnameseIdentityErrorDescriber>();

        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(1));

        // Khóa Data Protection (ký token đặt lại mật khẩu/xác nhận email). Trong container phải lưu ra volume,
        // nếu không mỗi lần khởi động lại sẽ làm mất hiệu lực các liên kết đã gửi qua email.
        var dataProtection = services.AddDataProtection().SetApplicationName("EduViet");
        var keysPath = configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(keysPath))
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        if (configuration.GetValue("BackgroundJobs:Enabled", true))
            services.AddHostedService<OrderExpiryService>();

        return services;
    }

    /// <summary>Database:Provider = "SqliteDemo" → chạy demo bằng SQLite (chỉ dùng phát triển/trình diễn).</summary>
    public static bool IsSqliteDemo(IConfiguration configuration) =>
        string.Equals(configuration["Database:Provider"], "SqliteDemo", StringComparison.OrdinalIgnoreCase);
}

internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
