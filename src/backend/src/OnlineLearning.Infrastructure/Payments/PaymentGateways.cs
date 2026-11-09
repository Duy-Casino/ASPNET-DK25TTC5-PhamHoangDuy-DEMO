using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Infrastructure.Payments;

internal static class Crypto
{
    public static string HmacSha512(string key, string data) =>
        Convert.ToHexStringLower(HMACSHA512.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(data)));

    public static string HmacSha256(string key, string data) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(data)));

    /// <summary>So sánh chữ ký trong thời gian cố định (chống timing attack).</summary>
    public static bool SafeEquals(string? a, string? b) =>
        a is not null && b is not null &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a.ToLowerInvariant()), Encoding.ASCII.GetBytes(b.ToLowerInvariant()));

    public static string Raw(IReadOnlyDictionary<string, string> data) =>
        JsonSerializer.Serialize(data.Where(kv => !kv.Key.Contains("Hash", StringComparison.OrdinalIgnoreCase) && kv.Key != "signature")
            .ToDictionary(kv => kv.Key, kv => kv.Value));
}

// =====================================================================
// VNPay (sandbox: https://sandbox.vnpayment.vn) — chữ ký HMAC-SHA512
// =====================================================================

public sealed class VnPayOptions
{
    public const string SectionName = "VnPay";
    public string TmnCode { get; set; } = string.Empty;
    public string HashSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    public string IpnUrl { get; set; } = string.Empty;
}

public sealed class VnPayGateway(IOptions<VnPayOptions> options, IDateTimeProvider clock) : IPaymentGateway
{
    private readonly VnPayOptions _o = options.Value;

    public PaymentProvider Provider => PaymentProvider.VNPay;
    public string DisplayName => "VNPay (ATM, thẻ quốc tế, QR)";
    public bool IsEnabled => !string.IsNullOrWhiteSpace(_o.TmnCode) && !string.IsNullOrWhiteSpace(_o.HashSecret);
    public bool IsTestMode => _o.BaseUrl.Contains("sandbox", StringComparison.OrdinalIgnoreCase);

    // Việt Nam không có giờ mùa hè nên GMT+7 cố định
    private static DateTime ToVn(DateTime utc) => utc.AddHours(7);

    public Task<GatewayPaymentSession> CreatePaymentAsync(GatewayPaymentRequest r, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var expire = r.ExpiresAtUtc < now.AddMinutes(15) ? r.ExpiresAtUtc : now.AddMinutes(15);
        var p = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = _o.TmnCode,
            ["vnp_Amount"] = ((long)(r.Amount * 100)).ToString(CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = r.TransactionRef,
            ["vnp_OrderInfo"] = r.OrderInfo,
            ["vnp_OrderType"] = "other",
            ["vnp_Locale"] = "vn",
            ["vnp_ReturnUrl"] = r.ReturnUrl,
            ["vnp_IpAddr"] = string.IsNullOrWhiteSpace(r.ClientIp) ? "127.0.0.1" : r.ClientIp,
            ["vnp_CreateDate"] = ToVn(now).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_ExpireDate"] = ToVn(expire).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
        };
        var query = BuildQuery(p);
        var hash = Crypto.HmacSha512(_o.HashSecret, query);
        return Task.FromResult(new GatewayPaymentSession($"{_o.BaseUrl}?{query}&vnp_SecureHash={hash}"));
    }

    public GatewayCallback ParseCallback(IReadOnlyDictionary<string, string> data)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, v) in data)
        {
            if (k.StartsWith("vnp_", StringComparison.Ordinal) && k is not "vnp_SecureHash" and not "vnp_SecureHashType" && !string.IsNullOrEmpty(v))
                fields[k] = v;
        }
        data.TryGetValue("vnp_SecureHash", out var received);
        var valid = IsEnabled && Crypto.SafeEquals(Crypto.HmacSha512(_o.HashSecret, BuildQuery(fields)), received);

        fields.TryGetValue("vnp_Amount", out var amountRaw);
        var amount = long.TryParse(amountRaw, out var a) ? a / 100m : -1;
        fields.TryGetValue("vnp_ResponseCode", out var responseCode);
        fields.TryGetValue("vnp_TransactionStatus", out var status);
        fields.TryGetValue("vnp_TxnRef", out var txnRef);
        fields.TryGetValue("vnp_TransactionNo", out var txnNo);
        fields.TryGetValue("vnp_BankCode", out var bank);

        return new GatewayCallback(valid, txnRef, amount, responseCode == "00" && status == "00", txnNo, bank, responseCode, Crypto.Raw(fields));
    }

    /// <summary>VNPay ký trên chuỗi query đã URL-encode (khoảng trắng thành '+'), khóa sắp xếp tăng dần.</summary>
    private static string BuildQuery(IEnumerable<KeyValuePair<string, string>> p) =>
        string.Join('&', p.Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));
}

// =====================================================================
// MoMo (sandbox: https://test-payment.momo.vn) — chữ ký HMAC-SHA256
// =====================================================================

public sealed class MoMoOptions
{
    public const string SectionName = "MoMo";
    public string PartnerCode { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "https://test-payment.momo.vn/v2/gateway/api/create";
    public string IpnUrl { get; set; } = string.Empty;
}

public sealed class MoMoGateway(HttpClient http, IOptions<MoMoOptions> options, ILogger<MoMoGateway> logger) : IPaymentGateway
{
    private readonly MoMoOptions _o = options.Value;

    public PaymentProvider Provider => PaymentProvider.MoMo;
    public string DisplayName => "Ví MoMo";
    public bool IsEnabled => !string.IsNullOrWhiteSpace(_o.PartnerCode) && !string.IsNullOrWhiteSpace(_o.AccessKey) && !string.IsNullOrWhiteSpace(_o.SecretKey);
    public bool IsTestMode => _o.Endpoint.Contains("test-payment", StringComparison.OrdinalIgnoreCase);

    public async Task<GatewayPaymentSession> CreatePaymentAsync(GatewayPaymentRequest r, CancellationToken ct = default)
    {
        var amount = ((long)r.Amount).ToString(CultureInfo.InvariantCulture);
        var requestId = $"{r.TransactionRef}-{Guid.NewGuid():N}"[..40];
        const string requestType = "captureWallet";
        const string extraData = "";
        var raw = $"accessKey={_o.AccessKey}&amount={amount}&extraData={extraData}&ipnUrl={_o.IpnUrl}&orderId={r.TransactionRef}" +
                  $"&orderInfo={r.OrderInfo}&partnerCode={_o.PartnerCode}&redirectUrl={r.ReturnUrl}&requestId={requestId}&requestType={requestType}";

        var body = new
        {
            partnerCode = _o.PartnerCode, requestId, amount = long.Parse(amount, CultureInfo.InvariantCulture), orderId = r.TransactionRef,
            orderInfo = r.OrderInfo, redirectUrl = r.ReturnUrl, ipnUrl = _o.IpnUrl, requestType, extraData, lang = "vi",
            signature = Crypto.HmacSha256(_o.SecretKey, raw),
        };

        using var res = await http.PostAsJsonAsync(_o.Endpoint, body, ct);
        var json = await res.Content.ReadFromJsonAsync<MoMoCreateResponse>(cancellationToken: ct);
        if (json is null || json.ResultCode != 0 || string.IsNullOrEmpty(json.PayUrl))
        {
            logger.LogError("MoMo tạo giao dịch thất bại: {Code} {Message}", json?.ResultCode, json?.Message);
            throw new InvalidOperationException($"MoMo: {json?.Message ?? "không tạo được giao dịch"}");
        }
        return new GatewayPaymentSession(json.PayUrl);
    }

    public GatewayCallback ParseCallback(IReadOnlyDictionary<string, string> d)
    {
        string V(string k) => d.TryGetValue(k, out var v) ? v : string.Empty;
        var raw = $"accessKey={_o.AccessKey}&amount={V("amount")}&extraData={V("extraData")}&message={V("message")}&orderId={V("orderId")}" +
                  $"&orderInfo={V("orderInfo")}&orderType={V("orderType")}&partnerCode={V("partnerCode")}&payType={V("payType")}" +
                  $"&requestId={V("requestId")}&responseTime={V("responseTime")}&resultCode={V("resultCode")}&transId={V("transId")}";
        var valid = IsEnabled && V("partnerCode") == _o.PartnerCode && Crypto.SafeEquals(Crypto.HmacSha256(_o.SecretKey, raw), V("signature"));
        var amount = decimal.TryParse(V("amount"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) ? a : -1;
        return new GatewayCallback(valid, V("orderId") is { Length: > 0 } o ? o : null, amount, V("resultCode") == "0",
            V("transId") is { Length: > 0 } t ? t : null, V("payType"), V("resultCode"), Crypto.Raw(d));
    }

    private sealed record MoMoCreateResponse(
        [property: JsonPropertyName("resultCode")] int ResultCode,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("payUrl")] string? PayUrl);
}

// =====================================================================
// Kênh thanh toán TEST — tự động thành công, KHÔNG thu tiền thật.
// Mô phỏng đúng luồng cổng thật: chuyển hướng → "cổng" xử lý → gửi IPN có chữ ký → hệ thống xác thực → cấp quyền.
// =====================================================================

public sealed class TestGatewayOptions
{
    public const string SectionName = "Payments:TestGateway";
    /// <summary>Chỉ bật ở môi trường phát triển/demo. Production phải là false.</summary>
    public bool Enabled { get; set; }
    /// <summary>Khóa ký; để trống thì sinh ngẫu nhiên mỗi lần khởi động (đủ cho mô phỏng trong cùng tiến trình).</summary>
    public string? Secret { get; set; }
}

public sealed class TestPaymentGateway(IOptions<TestGatewayOptions> options) : IPaymentGateway
{
    private static readonly string RandomKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private string Key => string.IsNullOrWhiteSpace(options.Value.Secret) ? RandomKey : options.Value.Secret!;

    public PaymentProvider Provider => PaymentProvider.Test;
    public string DisplayName => "Kênh thanh toán thử nghiệm (tự động thành công)";
    public bool IsEnabled => options.Value.Enabled;
    public bool IsTestMode => true;

    /// <summary>Chuyển tới "cổng" giả lập do chính API phục vụ: /api/payments/test/gateway.</summary>
    public Task<GatewayPaymentSession> CreatePaymentAsync(GatewayPaymentRequest r, CancellationToken ct = default)
    {
        var amount = ((long)r.Amount).ToString(CultureInfo.InvariantCulture);
        var sig = Crypto.HmacSha256(Key, $"amount={amount}&ref={r.TransactionRef}&returnUrl={r.ReturnUrl}");
        var url = $"/api/payments/test/gateway?ref={Uri.EscapeDataString(r.TransactionRef)}&amount={amount}" +
                  $"&returnUrl={Uri.EscapeDataString(r.ReturnUrl)}&sig={sig}";
        return Task.FromResult(new GatewayPaymentSession(url));
    }

    public bool VerifyGatewayRequest(string txnRef, string amount, string returnUrl, string sig) =>
        Crypto.SafeEquals(Crypto.HmacSha256(Key, $"amount={amount}&ref={txnRef}&returnUrl={returnUrl}"), sig);

    /// <summary>Tạo dữ liệu IPN có chữ ký như một cổng thật sẽ gửi.</summary>
    public Dictionary<string, string> BuildCallback(string txnRef, string amount)
    {
        var d = new Dictionary<string, string>
        {
            ["ref"] = txnRef,
            ["amount"] = amount,
            ["resultCode"] = "0",
            ["transId"] = $"TEST{DateTime.UtcNow:yyMMddHHmmssfff}",
        };
        d["signature"] = Sign(d);
        return d;
    }

    public GatewayCallback ParseCallback(IReadOnlyDictionary<string, string> d)
    {
        string V(string k) => d.TryGetValue(k, out var v) ? v : string.Empty;
        var valid = IsEnabled && Crypto.SafeEquals(Sign(d), V("signature"));
        var amount = decimal.TryParse(V("amount"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) ? a : -1;
        return new GatewayCallback(valid, V("ref") is { Length: > 0 } r ? r : null, amount, V("resultCode") == "0",
            V("transId") is { Length: > 0 } t ? t : null, "TEST", V("resultCode"), Crypto.Raw(d));
    }

    private string Sign(IReadOnlyDictionary<string, string> d)
    {
        string V(string k) => d.TryGetValue(k, out var v) ? v : string.Empty;
        return Crypto.HmacSha256(Key, $"amount={V("amount")}&ref={V("ref")}&resultCode={V("resultCode")}&transId={V("transId")}");
    }
}
