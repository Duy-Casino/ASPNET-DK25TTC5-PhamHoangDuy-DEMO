using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using OnlineLearning.API.Common;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Domain.Constants;
using OnlineLearning.Infrastructure.Storage;

namespace OnlineLearning.API.Controllers;

public sealed record UploadResultDto(string PublicId, string? Url, string FileName, string ContentType, long Size, int? DurationSeconds, string Kind, string Storage);

/// <summary>
/// Tải tệp lên (Cloudinary hoặc lưu cục bộ). Ảnh: mọi người dùng đã đăng nhập (ảnh đại diện).
/// Video và tài liệu: chỉ giảng viên/admin. Kiểm tra phần mở rộng, kích thước và chữ ký tệp (magic bytes).
/// </summary>
[Route("api/uploads")]
[Authorize]
public sealed class UploadsController(IFileStorage storage, ILogger<UploadsController> logger) : ApiControllerBase
{
    private const long MaxImage = 5L * 1024 * 1024;
    private const long MaxDocument = 50L * 1024 * 1024;
    private const long MaxVideo = 500L * 1024 * 1024;

    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        ["image"] = [".jpg", ".jpeg", ".png", ".webp", ".gif"],
        ["video"] = [".mp4", ".webm", ".mov", ".m4v"],
        ["document"] = [".pdf", ".zip", ".docx", ".xlsx", ".pptx", ".txt", ".md", ".rar"],
    };

    [HttpPost("{kind}")]
    [RequestSizeLimit(MaxVideo + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxVideo + 1024 * 1024)]
    public async Task<IActionResult> Upload(string kind, IFormFile? file, CancellationToken ct)
    {
        kind = kind.ToLowerInvariant();
        if (!Allowed.TryGetValue(kind, out var extensions))
            return Problem(new Application.Common.Models.Error("invalid_kind", "Loại tệp không hợp lệ.", Application.Common.Models.ErrorType.Validation));
        if (kind != "image" && !User.IsInRole(Roles.Instructor) && !User.IsInRole(Roles.Admin))
            return Problem(Application.Common.Models.Error.Forbidden("Chỉ giảng viên được tải video và tài liệu."));
        if (file is null || file.Length == 0)
            return Problem(Application.Common.Models.Error.Validation("Chưa chọn tệp."));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!extensions.Contains(ext))
            return Problem(Application.Common.Models.Error.Validation($"Định dạng không được hỗ trợ. Cho phép: {string.Join(", ", extensions)}."));

        var max = kind switch { "image" => MaxImage, "video" => MaxVideo, _ => MaxDocument };
        if (file.Length > max)
            return Problem(Application.Common.Models.Error.Validation($"Tệp vượt quá {max / 1024 / 1024} MB."));

        await using var stream = file.OpenReadStream();
        if (!await HasValidSignatureAsync(stream, ext, ct))
            return Problem(Application.Common.Models.Error.Validation("Nội dung tệp không khớp với định dạng."));
        stream.Position = 0;

        var fileKind = kind switch { "image" => StoredFileKind.Image, "video" => StoredFileKind.Video, _ => StoredFileKind.Document };
        new FileExtensionContentTypeProvider().TryGetContentType(file.FileName, out var contentType);
        var safeName = Path.GetFileName(file.FileName).Replace("\"", "").Trim();
        if (safeName.Length > 200) safeName = safeName[^200..];

        var stored = await storage.UploadAsync(stream, safeName, contentType ?? "application/octet-stream", fileKind, ct);
        logger.LogInformation("Người dùng {User} tải lên {Kind} {PublicId} ({Size} bytes) qua {Storage}", User.GetUserId(), kind, stored.PublicId, stored.Size, storage.ProviderName);

        return Ok(new UploadResultDto(stored.PublicId, stored.PublicUrl, stored.FileName, stored.ContentType, stored.Size, stored.DurationSeconds, kind, storage.ProviderName));
    }

    /// <summary>Kiểm tra vài byte đầu của tệp để chặn tệp giả phần mở rộng.</summary>
    private static async Task<bool> HasValidSignatureAsync(Stream s, string ext, CancellationToken ct)
    {
        var head = new byte[12];
        var read = await s.ReadAsync(head, ct);
        bool Starts(params byte[] sig) => read >= sig.Length && head.AsSpan(0, sig.Length).SequenceEqual(sig);
        return ext switch
        {
            ".jpg" or ".jpeg" => Starts(0xFF, 0xD8, 0xFF),
            ".png" => Starts(0x89, 0x50, 0x4E, 0x47),
            ".gif" => Starts(0x47, 0x49, 0x46, 0x38),
            ".webp" => Starts(0x52, 0x49, 0x46, 0x46) && read >= 12 && head[8] == 0x57 && head[9] == 0x45,
            ".pdf" => Starts(0x25, 0x50, 0x44, 0x46),
            ".zip" or ".docx" or ".xlsx" or ".pptx" => Starts(0x50, 0x4B),
            ".rar" => Starts(0x52, 0x61, 0x72, 0x21),
            ".mp4" or ".mov" or ".m4v" => read >= 8 && head[4] == 0x66 && head[5] == 0x74 && head[6] == 0x79 && head[7] == 0x70, // 'ftyp'
            ".webm" => Starts(0x1A, 0x45, 0xDF, 0xA3),
            _ => true, // .txt, .md
        };
    }
}

/// <summary>Phát tệp riêng tư lưu cục bộ qua URL có chữ ký + hạn dùng (Cloudinary tự phát qua CDN).</summary>
[Route("api/media")]
[AllowAnonymous]
public sealed class MediaController(MediaUrlSigner signer, LocalFileStorage local) : ControllerBase
{
    [HttpGet("{**publicId}")]
    public IActionResult Get(string publicId, [FromQuery] long exp, [FromQuery] string? sig)
    {
        if (!signer.Verify(publicId, exp, sig)) return StatusCode(StatusCodes.Status403Forbidden);
        var path = local.ResolvePrivatePath(publicId);
        if (path is null || !System.IO.File.Exists(path)) return NotFound();

        new FileExtensionContentTypeProvider().TryGetContentType(path, out var contentType);
        Response.Headers.CacheControl = "private, max-age=3600";
        // enableRangeProcessing: cho phép tua video
        return PhysicalFile(path, contentType ?? "application/octet-stream", enableRangeProcessing: true);
    }
}
