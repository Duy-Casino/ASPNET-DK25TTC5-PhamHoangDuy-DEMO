using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineLearning.Infrastructure.Persistence;
using OnlineLearning.Infrastructure.Persistence.Seed;

namespace OnlineLearning.IntegrationTests;

/// <summary>
/// Chạy toàn bộ API thật (controller → service → EF Core) trên SQLite in-memory,
/// dữ liệu mẫu giống môi trường Development.
/// </summary>
public sealed class TestApp : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Demo@12345";
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:SecretKey", "test-secret-key-0123456789-abcdefghijklmnopqrstuvwxyz");
        builder.UseSetting("Seed:DemoPassword", Password);
        builder.UseSetting("BackgroundJobs:Enabled", "false");
        builder.UseSetting("Smtp:Host", "");
        builder.UseSetting("Payments:ReturnUrl", ReturnUrl);
        builder.UseSetting("Payments:TestGateway:Enabled", "true");
        builder.UseSetting("VnPay:TmnCode", VnPayTmn);
        builder.UseSetting("VnPay:HashSecret", VnPaySecret);
        builder.UseSetting("MoMo:PartnerCode", MoMoPartner);
        builder.UseSetting("MoMo:AccessKey", MoMoAccess);
        builder.UseSetting("MoMo:SecretKey", MoMoSecret);
        builder.UseSetting("Cloudinary:CloudName", "");
        builder.UseSetting("Authentication:Google:ClientId", "test-client.apps.googleusercontent.com");

        builder.ConfigureServices(services =>
        {
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
            // Đồng hồ điều chỉnh được để test các nghiệp vụ theo ngày (điểm danh liên tục)
            services.RemoveAll<OnlineLearning.Application.Common.Interfaces.IDateTimeProvider>();
            services.AddSingleton<OnlineLearning.Application.Common.Interfaces.IDateTimeProvider>(Clock);
            // Không gọi MoMo thật: trả về payUrl giả lập
            services.AddHttpClient<OnlineLearning.Infrastructure.Payments.MoMoGateway>()
                .ConfigurePrimaryHttpMessageHandler(() => new FakeMoMoHandler());
            // Ghi lại email thay vì gửi thật
            services.RemoveAll<OnlineLearning.Application.Common.Interfaces.IGoogleTokenValidator>();
            services.AddSingleton<OnlineLearning.Application.Common.Interfaces.IGoogleTokenValidator, FakeGoogleTokenValidator>();
            services.RemoveAll<OnlineLearning.Application.Common.Interfaces.IEmailSender>();
            services.AddSingleton<OnlineLearning.Application.Common.Interfaces.IEmailSender>(Emails);
        });
    }

    public const string ReturnUrl = "http://localhost/thanh-toan/ket-qua";
    public const string VnPayTmn = "TESTTMN1";
    public const string VnPaySecret = "VNPAYTESTSECRETKEY0123456789ABCD";
    public const string MoMoPartner = "MOMOTEST";
    public const string MoMoAccess = "momoaccesskey";
    public const string MoMoSecret = "momosecretkey0123456789";

    private sealed class FakeMoMoHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { resultCode = 0, message = "Thành công.", payUrl = "https://test-payment.momo.vn/v2/gateway/pay?t=fake" }),
            });
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await DbSeeder.SeedAsync(Services, migrate: false);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _connection.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>Lệch giờ hệ thống trong test. Nhớ đặt lại TimeSpan.Zero sau khi dùng.</summary>
    public TestClock Clock { get; } = new();

    /// <summary>Email hệ thống đã "gửi" trong phiên kiểm thử.</summary>
    public FakeEmailSender Emails { get; } = new();

    public HttpClient Anonymous() => CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    public async Task<HttpClient> LoginAsync(string email, string password = Password)
    {
        var client = Anonymous();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    public T Db<T>(Func<AppDbContext, T> query)
    {
        using var scope = Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}

public sealed class TestClock : OnlineLearning.Application.Common.Interfaces.IDateTimeProvider
{
    public TimeSpan Offset { get; set; }
    public DateTime UtcNow => DateTime.UtcNow + Offset;
}

public sealed class FakeEmailSender : OnlineLearning.Application.Common.Interfaces.IEmailSender
{
    public sealed record Sent(string To, string Subject, string Body);
    private readonly System.Collections.Concurrent.ConcurrentQueue<Sent> _sent = new();
    public IReadOnlyList<Sent> All => [.. _sent];

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        _sent.Enqueue(new Sent(to, subject, htmlBody));
        return Task.CompletedTask;
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<TestApp>;

public static class HttpExtensions
{
    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage res) =>
        await res.Content.ReadFromJsonAsync<JsonElement>(TestApp.Json);

    public static async Task<JsonElement> OkJsonAsync(this HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            throw new Xunit.Sdk.XunitException($"{(int)res.StatusCode} {res.RequestMessage?.Method} {res.RequestMessage?.RequestUri}: {await res.Content.ReadAsStringAsync()}");
        return await res.JsonAsync();
    }
}

/// <summary>Giả lập Google: token dạng "google:{sub}:{email}:{tên}[:unverified]".</summary>
public sealed class FakeGoogleTokenValidator : OnlineLearning.Application.Common.Interfaces.IGoogleTokenValidator
{
    public bool IsConfigured => true;
    public Task<OnlineLearning.Application.Common.Interfaces.ExternalUserInfo?> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        var p = idToken.Split(':');
        if (p.Length < 4 || p[0] != "google") return Task.FromResult<OnlineLearning.Application.Common.Interfaces.ExternalUserInfo?>(null);
        return Task.FromResult<OnlineLearning.Application.Common.Interfaces.ExternalUserInfo?>(
            new(p[1], p[2], !(p.Length > 4 && p[4] == "unverified"), p[3], null));
    }
}
