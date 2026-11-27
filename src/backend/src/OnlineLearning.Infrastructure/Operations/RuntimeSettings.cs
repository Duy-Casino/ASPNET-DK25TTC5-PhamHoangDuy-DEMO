using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OnlineLearning.Application.Features.Admin;
using OnlineLearning.Infrastructure.Persistence;

namespace OnlineLearning.Infrastructure.Operations;

/// <summary>
/// Nguồn cấu hình chứa cài đặt Admin chỉnh trên giao diện (bảng SystemSettings). Được thêm SAU appsettings nên ghi đè giá trị trong tệp;
/// khi Admin lưu, nguồn được nạp lại và IOptionsSnapshot ở các request sau đọc ngay giá trị mới (không cần khởi động lại).
/// </summary>
public sealed class SystemSettingsConfigurationSource : IConfigurationSource
{
    internal static readonly ConcurrentDictionary<string, string?> Values = new(StringComparer.OrdinalIgnoreCase);
    internal static SystemSettingsConfigurationProvider? Provider;

    public IConfigurationProvider Build(IConfigurationBuilder builder) => Provider = new SystemSettingsConfigurationProvider();
}

public sealed class SystemSettingsConfigurationProvider : ConfigurationProvider
{
    public override void Load() => Data = new Dictionary<string, string?>(SystemSettingsConfigurationSource.Values, StringComparer.OrdinalIgnoreCase);
    public void Refresh() { Load(); OnReload(); }
}

public sealed class RuntimeSettings(IConfiguration configuration) : IRuntimeSettings
{
    public string? Get(string key) => configuration[key];

    public void Apply(IReadOnlyDictionary<string, string?> overrides)
    {
        SystemSettingsConfigurationSource.Values.Clear();
        foreach (var (k, v) in overrides) SystemSettingsConfigurationSource.Values[k] = v;
        SystemSettingsConfigurationSource.Provider?.Refresh();
    }

    /// <summary>Nạp cài đặt đã lưu khi khởi động (bảng chưa có thì bỏ qua: DB demo cũ sẽ được bổ sung bảng ở bước seed).</summary>
    public static async Task LoadAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = await db.SystemSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => (string?)s.Value);
            scope.ServiceProvider.GetRequiredService<IRuntimeSettings>().Apply(rows);
        }
        catch (Exception ex)
        {
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SystemSettings").LogInformation(ex, "Chưa nạp được cài đặt hệ thống");
        }
    }
}

public static class SystemSettingsConfigurationExtensions
{
    public static IConfigurationBuilder AddSystemSettings(this IConfigurationBuilder builder) => builder.Add(new SystemSettingsConfigurationSource());
}
