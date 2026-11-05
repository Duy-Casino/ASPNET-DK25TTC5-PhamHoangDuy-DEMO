using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Options;

namespace OnlineLearning.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    /// <summary>Thư mục lưu tệp khi chưa cấu hình Cloudinary (tương đối so với thư mục ứng dụng).</summary>
    public string LocalRoot { get; set; } = "App_Data/storage";
    /// <summary>Khóa ký URL tệp riêng tư; để trống thì suy ra từ Jwt:SecretKey.</summary>
    public string? SigningKey { get; set; }
    public int SignedUrlMinutes { get; set; } = 120;
}

public sealed class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";
    public string CloudName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string Folder { get; set; } = "eduviet";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(CloudName) && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret);
}

/// <summary>Ký và kiểm tra URL tạm thời cho tệp riêng tư lưu cục bộ: /api/media/{publicId}?exp=..&amp;sig=..</summary>
public sealed class MediaUrlSigner(IOptions<StorageOptions> storage, IOptions<JwtOptions> jwt, IDateTimeProvider clock)
{
    private byte[] Key => Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(storage.Value.SigningKey)
        ? "media-signing:" + jwt.Value.SecretKey
        : storage.Value.SigningKey);

    public string CreateUrl(string publicId)
    {
        var exp = new DateTimeOffset(clock.UtcNow.AddMinutes(storage.Value.SignedUrlMinutes)).ToUnixTimeSeconds();
        return $"/api/media/{publicId}?exp={exp}&sig={Sign(publicId, exp)}";
    }

    public bool Verify(string publicId, long exp, string? sig) =>
        exp >= new DateTimeOffset(clock.UtcNow).ToUnixTimeSeconds() && sig is not null &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign(publicId, exp)), Encoding.ASCII.GetBytes(sig));

    private string Sign(string publicId, long exp) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes($"{publicId}|{exp.ToString(CultureInfo.InvariantCulture)}")));
}

/// <summary>
/// Lưu cục bộ (khi chưa có Cloudinary): ảnh ở wwwroot/uploads (công khai);
/// video/tài liệu ở thư mục riêng, chỉ phát qua /api/media có chữ ký và hạn dùng.
/// </summary>
public sealed class LocalFileStorage(IOptions<StorageOptions> options, IHostEnvironment env, MediaUrlSigner signer) : IFileStorage
{
    public string ProviderName => "Local";

    private string PrivateRoot => Path.GetFullPath(Path.Combine(env.ContentRootPath, options.Value.LocalRoot));
    private string PublicRoot => Path.GetFullPath(Path.Combine(env.ContentRootPath, "wwwroot", "uploads"));

    public async Task<StoredFile> UploadAsync(Stream content, string fileName, string contentType, StoredFileKind kind, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var folder = kind switch { StoredFileKind.Image => "images", StoredFileKind.Video => "videos", _ => "documents" };
        var name = $"{Guid.NewGuid():N}{ext}";
        var root = kind == StoredFileKind.Image ? PublicRoot : PrivateRoot;
        var dir = Path.Combine(root, folder);
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, name);
        await using (var fs = File.Create(path))
            await content.CopyToAsync(fs, ct);

        var publicId = $"{folder}/{name}";
        var size = new FileInfo(path).Length;
        return new StoredFile(publicId, kind == StoredFileKind.Image ? $"/uploads/{publicId}" : null, fileName, contentType, size, null, kind);
    }

    public string GetSignedUrl(string publicId, StoredFileKind kind) =>
        kind == StoredFileKind.Image ? $"/uploads/{publicId}" : signer.CreateUrl(publicId);

    public Task DeleteAsync(string publicId, StoredFileKind kind, CancellationToken ct = default)
    {
        var path = ResolvePrivatePath(publicId, kind == StoredFileKind.Image ? PublicRoot : PrivateRoot);
        if (path is not null && File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    /// <summary>Đường dẫn vật lý của tệp riêng tư; null nếu publicId không hợp lệ (chống path traversal).</summary>
    public string? ResolvePrivatePath(string publicId) => ResolvePrivatePath(publicId, PrivateRoot);

    private static string? ResolvePrivatePath(string publicId, string root)
    {
        if (string.IsNullOrWhiteSpace(publicId) || publicId.Contains("..", StringComparison.Ordinal)) return null;
        var full = Path.GetFullPath(Path.Combine(root, publicId));
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? full : null;
    }
}

/// <summary>
/// Cloudinary: ảnh dạng "upload" (công khai, CDN); video và tài liệu dạng "authenticated" —
/// chỉ truy cập được bằng URL có chữ ký do Backend tạo cho người có quyền.
/// </summary>
public sealed class CloudinaryFileStorage : IFileStorage
{
    private readonly Cloudinary _cloudinary;
    private readonly CloudinaryOptions _o;
    private readonly ILogger<CloudinaryFileStorage> _logger;

    public CloudinaryFileStorage(IOptions<CloudinaryOptions> options, ILogger<CloudinaryFileStorage> logger)
    {
        _o = options.Value;
        _logger = logger;
        _cloudinary = new Cloudinary(new Account(_o.CloudName, _o.ApiKey, _o.ApiSecret)) { Api = { Secure = true } };
    }

    public string ProviderName => "Cloudinary";

    public async Task<StoredFile> UploadAsync(Stream content, string fileName, string contentType, StoredFileKind kind, CancellationToken ct = default)
    {
        var file = new FileDescription(fileName, content);
        switch (kind)
        {
            case StoredFileKind.Image:
            {
                var r = await _cloudinary.UploadAsync(new ImageUploadParams
                {
                    File = file, Folder = $"{_o.Folder}/images", Type = "upload", UseFilename = false, UniqueFilename = true,
                }, ct);
                EnsureOk(r);
                return new StoredFile(r.PublicId, r.SecureUrl.ToString(), fileName, contentType, r.Bytes, null, kind);
            }
            case StoredFileKind.Video:
            {
                var r = await _cloudinary.UploadAsync(new VideoUploadParams
                {
                    File = file, Folder = $"{_o.Folder}/videos", Type = "authenticated", UniqueFilename = true,
                }, ct);
                EnsureOk(r);
                return new StoredFile(r.PublicId, null, fileName, contentType, r.Bytes, (int)Math.Round(r.Duration), kind);
            }
            default:
            {
                var r = await _cloudinary.UploadAsync(new RawUploadParams
                {
                    File = file, Folder = $"{_o.Folder}/documents", Type = "authenticated", UseFilename = true, UniqueFilename = true,
                }, "raw", ct);
                EnsureOk(r);
                return new StoredFile(r.PublicId, null, fileName, contentType, r.Bytes, null, kind);
            }
        }
    }

    public string GetSignedUrl(string publicId, StoredFileKind kind) => kind switch
    {
        StoredFileKind.Image => _cloudinary.Api.UrlImgUp.Secure(true).BuildUrl(publicId),
        StoredFileKind.Video => _cloudinary.Api.UrlVideoUp.Type("authenticated").Signed(true).Secure(true).BuildUrl(publicId),
        _ => _cloudinary.Api.Url.ResourceType("raw").Type("authenticated").Signed(true).Secure(true).BuildUrl(publicId),
    };

    public async Task DeleteAsync(string publicId, StoredFileKind kind, CancellationToken ct = default)
    {
        var p = new DeletionParams(publicId)
        {
            ResourceType = kind switch { StoredFileKind.Image => ResourceType.Image, StoredFileKind.Video => ResourceType.Video, _ => ResourceType.Raw },
            Type = kind == StoredFileKind.Image ? "upload" : "authenticated",
        };
        var r = await _cloudinary.DestroyAsync(p);
        if (r.Error is not null) _logger.LogWarning("Không xóa được {PublicId} trên Cloudinary: {Error}", publicId, r.Error.Message);
    }

    private void EnsureOk(UploadResult r)
    {
        if (r.Error is null) return;
        _logger.LogError("Cloudinary upload lỗi: {Error}", r.Error.Message);
        throw new InvalidOperationException("Tải tệp lên Cloudinary thất bại.");
    }
}
