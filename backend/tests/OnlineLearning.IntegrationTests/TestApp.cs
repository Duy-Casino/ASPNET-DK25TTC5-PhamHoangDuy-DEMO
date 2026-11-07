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

        builder.ConfigureServices(services =>
        {
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
            // Đồng hồ điều chỉnh được để test các nghiệp vụ theo ngày (điểm danh liên tục)
            services.RemoveAll<OnlineLearning.Application.Common.Interfaces.IDateTimeProvider>();
            services.AddSingleton<OnlineLearning.Application.Common.Interfaces.IDateTimeProvider>(Clock);
            // Không gọi MoMo thật: trả về payUrl giả lập
            services.AddHttpClient<OnlineLearning.Infrastructure.Payments.MoMoGateway>()
                .ConfigurePrimaryHttpMessageHandler(() => new FakeMoMoHandler());
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
