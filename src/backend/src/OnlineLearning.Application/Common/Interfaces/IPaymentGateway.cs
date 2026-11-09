using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Common.Interfaces;

/// <summary>Yêu cầu tạo phiên thanh toán gửi sang cổng.</summary>
public sealed record GatewayPaymentRequest(
    string TransactionRef, decimal Amount, string OrderInfo, string ReturnUrl, string? ClientIp, DateTime ExpiresAtUtc);

public sealed record GatewayPaymentSession(string RedirectUrl);

/// <summary>Dữ liệu callback (IPN/return) sau khi cổng đã xác thực chữ ký.</summary>
public sealed record GatewayCallback(
    bool SignatureValid,
    string? TransactionRef,
    decimal Amount,
    bool IsSuccess,
    string? ProviderTransactionId,
    string? BankCode,
    string? ResponseCode,
    string RawData);

public interface IPaymentGateway
{
    PaymentProvider Provider { get; }
    /// <summary>Tên hiển thị cho người dùng.</summary>
    string DisplayName { get; }
    /// <summary>Đã cấu hình khóa (hoặc được bật trong môi trường test).</summary>
    bool IsEnabled { get; }
    /// <summary>true = kênh thử nghiệm, không thu tiền thật.</summary>
    bool IsTestMode { get; }

    Task<GatewayPaymentSession> CreatePaymentAsync(GatewayPaymentRequest request, CancellationToken ct = default);

    /// <summary>Xác thực chữ ký và trích xuất dữ liệu từ tham số callback của cổng.</summary>
    GatewayCallback ParseCallback(IReadOnlyDictionary<string, string> data);
}

public enum StoredFileKind { Image, Video, Document }

public sealed record StoredFile(string PublicId, string? PublicUrl, string FileName, string ContentType, long Size, int? DurationSeconds, StoredFileKind Kind);

/// <summary>
/// Lưu trữ tệp: ảnh công khai; video và tài liệu là riêng tư, chỉ truy cập qua URL có chữ ký do Backend cấp
/// cho người có quyền (học viên đã ghi danh, giảng viên sở hữu, admin).
/// </summary>
public interface IFileStorage
{
    string ProviderName { get; }
    Task<StoredFile> UploadAsync(Stream content, string fileName, string contentType, StoredFileKind kind, CancellationToken ct = default);
    /// <summary>URL tạm thời có chữ ký cho tệp riêng tư.</summary>
    string GetSignedUrl(string publicId, StoredFileKind kind);
    Task DeleteAsync(string publicId, StoredFileKind kind, CancellationToken ct = default);
}
