using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OnlineLearning.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class PaymentTests(TestApp app)
{
    private async Task<(HttpClient Client, string Email)> NewUserAsync()
    {
        var email = $"pay{Guid.NewGuid():N}@test.local";
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var reg = await (await client.PostAsJsonAsync("/api/auth/register", new { fullName = "Người Thanh Toán", email, password = "Matkhau123" })).OkJsonAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reg.GetProperty("accessToken").GetString());
        return (client, email);
    }

    private async Task<(Guid OrderId, decimal Total)> CreateOrderAsync(HttpClient client, string slug)
    {
        var courseId = app.Db(db => db.Courses.First(c => c.Slug == slug).Id);
        var order = await (await client.PostAsJsonAsync("/api/orders", new { courseIds = new[] { courseId } })).OkJsonAsync();
        Assert.Equal("Pending", order.GetProperty("status").GetString());
        return (order.GetProperty("id").GetGuid(), order.GetProperty("totalAmount").GetDecimal());
    }

    private static Dictionary<string, string> ParseQuery(string url) =>
        url[(url.IndexOf('?') + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2)).ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1].Replace('+', ' ')));

    [Fact]
    public async Task Methods_list_enabled_gateways()
    {
        var methods = await (await app.Anonymous().GetAsync("/api/payments/methods")).OkJsonAsync();
        var providers = methods.EnumerateArray().Select(m => m.GetProperty("provider").GetString()).ToList();
        Assert.Contains("VNPay", providers);
        Assert.Contains("MoMo", providers);
        Assert.Contains("Test", providers);
        Assert.True(methods.EnumerateArray().First(m => m.GetProperty("provider").GetString() == "Test").GetProperty("isTestMode").GetBoolean());
    }

    [Fact]
    public async Task Test_gateway_auto_succeeds_through_signed_ipn_and_is_idempotent()
    {
        var (client, email) = await NewUserAsync();
        var (orderId, total) = await CreateOrderAsync(client, "sql-server-cho-lap-trinh-vien");
        var before = app.Db(db => db.Courses.First(c => c.Slug == "sql-server-cho-lap-trinh-vien").EnrollmentCount);

        var checkout = await (await client.PostAsJsonAsync($"/api/payments/orders/{orderId}/checkout", new { provider = "Test" })).OkJsonAsync();
        Assert.True(checkout.GetProperty("isTestMode").GetBoolean());
        var gatewayUrl = checkout.GetProperty("redirectUrl").GetString()!;
        Assert.StartsWith("/api/payments/test/gateway", gatewayUrl);

        // Người dùng được chuyển tới "cổng" test → cổng gửi IPN có chữ ký → chuyển về trang kết quả
        var gw = await client.GetAsync(gatewayUrl);
        Assert.Equal(HttpStatusCode.Redirect, gw.StatusCode);
        var back = gw.Headers.Location!.ToString();
        Assert.StartsWith(TestApp.ReturnUrl, back);

        Assert.Equal("Paid", app.Db(db => db.Orders.First(o => o.Id == orderId).Status.ToString()));
        Assert.Equal(1, app.Db(db => db.Enrollments.Count(e => e.User.Email == email)));
        Assert.Equal(before + 1, app.Db(db => db.Courses.First(c => c.Slug == "sql-server-cho-lap-trinh-vien").EnrollmentCount));
        Assert.Equal(total, app.Db(db => db.Payments.First(p => p.OrderId == orderId && p.Provider == Domain.Enums.PaymentProvider.Test).Amount));

        // Trang kết quả đọc trạng thái
        var result = await (await client.GetAsync($"/api/payments/return/Test?{back[(back.IndexOf('?') + 1)..]}")).OkJsonAsync();
        Assert.Equal("Paid", result.GetProperty("orderStatus").GetString());
        Assert.True(result.GetProperty("isTestMode").GetBoolean());
        Assert.True(result.GetProperty("signatureValid").GetBoolean());

        // Gọi lại cổng (refresh/replay) không cấp quyền lần hai
        await client.GetAsync(gatewayUrl);
        Assert.Equal(before + 1, app.Db(db => db.Courses.First(c => c.Slug == "sql-server-cho-lap-trinh-vien").EnrollmentCount));

        // Không thanh toán lại đơn đã trả
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/payments/orders/{orderId}/checkout", new { provider = "Test" })).StatusCode);

        // Cổng test từ chối URL bị sửa
        var tampered = gatewayUrl.Replace($"amount={(long)total}", "amount=1000");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(tampered)).StatusCode);
    }

    [Fact]
    public async Task VnPay_ipn_validates_signature_amount_and_idempotency_and_return_never_grants()
    {
        var (client, email) = await NewUserAsync();
        var (orderId, total) = await CreateOrderAsync(client, "reactjs-va-typescript-cho-nguoi-moi");

        var checkout = await (await client.PostAsJsonAsync($"/api/payments/orders/{orderId}/checkout", new { provider = "VNPay" })).OkJsonAsync();
        var url = checkout.GetProperty("redirectUrl").GetString()!;
        Assert.StartsWith("https://sandbox.vnpayment.vn", url);
        var sent = ParseQuery(url);
        Assert.Equal(((long)(total * 100)).ToString(), sent["vnp_Amount"]);
        Assert.Equal(TestApp.VnPayTmn, sent["vnp_TmnCode"]);
        // Chữ ký gửi đi đúng chuẩn VNPay
        Assert.Equal(VnPaySign(sent.Where(kv => kv.Key != "vnp_SecureHash")), sent["vnp_SecureHash"]);

        var txnRef = sent["vnp_TxnRef"];
        var ipn = new Dictionary<string, string>
        {
            ["vnp_Amount"] = sent["vnp_Amount"], ["vnp_BankCode"] = "NCB", ["vnp_OrderInfo"] = sent["vnp_OrderInfo"],
            ["vnp_PayDate"] = "20261105101010", ["vnp_ResponseCode"] = "00", ["vnp_TmnCode"] = TestApp.VnPayTmn,
            ["vnp_TransactionNo"] = "14123456", ["vnp_TransactionStatus"] = "00", ["vnp_TxnRef"] = txnRef,
        };

        // Trang return (dù báo thành công và đúng chữ ký) KHÔNG cấp quyền
        var ret = await (await client.GetAsync($"/api/payments/return/VNPay?{Signed(ipn)}")).OkJsonAsync();
        Assert.True(ret.GetProperty("gatewayReportedSuccess").GetBoolean());
        Assert.Equal("Pending", ret.GetProperty("orderStatus").GetString());
        Assert.Equal(0, app.Db(db => db.Enrollments.Count(e => e.User.Email == email)));

        var anon = app.Anonymous();
        // Sai chữ ký → 97
        var bad = await (await anon.GetAsync($"/api/payments/vnpay/ipn?{Signed(ipn)}".Replace("vnp_TransactionNo=14123456", "vnp_TransactionNo=99999999"))).OkJsonAsync();
        Assert.Equal("97", bad.GetProperty("RspCode").GetString());

        // Sai số tiền → 04
        var wrongAmount = new Dictionary<string, string>(ipn) { ["vnp_Amount"] = "100000" };
        Assert.Equal("04", (await (await anon.GetAsync($"/api/payments/vnpay/ipn?{Signed(wrongAmount)}")).OkJsonAsync()).GetProperty("RspCode").GetString());

        // Không tồn tại → 01
        var unknown = new Dictionary<string, string>(ipn) { ["vnp_TxnRef"] = "KHONGTONTAI" };
        Assert.Equal("01", (await (await anon.GetAsync($"/api/payments/vnpay/ipn?{Signed(unknown)}")).OkJsonAsync()).GetProperty("RspCode").GetString());

        // Hợp lệ → 00, cấp quyền
        var ok = await (await anon.GetAsync($"/api/payments/vnpay/ipn?{Signed(ipn)}")).OkJsonAsync();
        Assert.Equal("00", ok.GetProperty("RspCode").GetString());
        Assert.Equal("Paid", app.Db(db => db.Orders.First(o => o.Id == orderId).Status.ToString()));
        Assert.Equal(1, app.Db(db => db.Enrollments.Count(e => e.User.Email == email)));
        Assert.Equal("14123456", app.Db(db => db.Payments.First(p => p.TransactionRef == txnRef).ProviderTransactionId));

        // IPN lặp → 02, không ghi danh thêm
        var again = await (await anon.GetAsync($"/api/payments/vnpay/ipn?{Signed(ipn)}")).OkJsonAsync();
        Assert.Equal("02", again.GetProperty("RspCode").GetString());
        Assert.Equal(1, app.Db(db => db.Enrollments.Count(e => e.User.Email == email)));
    }

    [Fact]
    public async Task VnPay_failed_payment_does_not_grant()
    {
        var (client, email) = await NewUserAsync();
        var (orderId, _) = await CreateOrderAsync(client, "power-bi-phan-tich-du-lieu-kinh-doanh");
        var checkout = await (await client.PostAsJsonAsync($"/api/payments/orders/{orderId}/checkout", new { provider = "VNPay" })).OkJsonAsync();
        var sent = ParseQuery(checkout.GetProperty("redirectUrl").GetString()!);
        var ipn = new Dictionary<string, string>
        {
            ["vnp_Amount"] = sent["vnp_Amount"], ["vnp_ResponseCode"] = "24", ["vnp_TmnCode"] = TestApp.VnPayTmn,
            ["vnp_TransactionNo"] = "0", ["vnp_TransactionStatus"] = "02", ["vnp_TxnRef"] = sent["vnp_TxnRef"],
        };
        var res = await (await app.Anonymous().GetAsync($"/api/payments/vnpay/ipn?{Signed(ipn)}")).OkJsonAsync();
        Assert.Equal("00", res.GetProperty("RspCode").GetString());
        Assert.Equal("Pending", app.Db(db => db.Orders.First(o => o.Id == orderId).Status.ToString()));
        Assert.Equal("Failed", app.Db(db => db.Payments.First(p => p.TransactionRef == sent["vnp_TxnRef"]).Status.ToString()));
        Assert.Equal(0, app.Db(db => db.Enrollments.Count(e => e.User.Email == email)));
    }

    [Fact]
    public async Task MoMo_ipn_with_valid_signature_grants_access()
    {
        var (client, email) = await NewUserAsync();
        var (orderId, total) = await CreateOrderAsync(client, "thiet-ke-ui-ux-voi-figma");
        var checkout = await (await client.PostAsJsonAsync($"/api/payments/orders/{orderId}/checkout", new { provider = "MoMo" })).OkJsonAsync();
        Assert.StartsWith("https://test-payment.momo.vn", checkout.GetProperty("redirectUrl").GetString());
        var txnRef = app.Db(db => db.Payments.First(p => p.OrderId == orderId && p.Provider == Domain.Enums.PaymentProvider.MoMo).TransactionRef);

        var body = new Dictionary<string, object>
        {
            ["partnerCode"] = TestApp.MoMoPartner, ["orderId"] = txnRef, ["requestId"] = "req-1", ["amount"] = (long)total,
            ["orderInfo"] = "Thanh toan", ["orderType"] = "momo_wallet", ["transId"] = 3012345678, ["resultCode"] = 0,
            ["message"] = "Thành công.", ["payType"] = "qr", ["responseTime"] = 1762300000000, ["extraData"] = "",
        };
        string V(string k) => Convert.ToString(body[k], System.Globalization.CultureInfo.InvariantCulture)!;
        var raw = $"accessKey={TestApp.MoMoAccess}&amount={V("amount")}&extraData={V("extraData")}&message={V("message")}&orderId={V("orderId")}" +
                  $"&orderInfo={V("orderInfo")}&orderType={V("orderType")}&partnerCode={V("partnerCode")}&payType={V("payType")}" +
                  $"&requestId={V("requestId")}&responseTime={V("responseTime")}&resultCode={V("resultCode")}&transId={V("transId")}";

        var forged = new Dictionary<string, object>(body) { ["signature"] = "deadbeef" };
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Anonymous().PostAsJsonAsync("/api/payments/momo/ipn", forged)).StatusCode);
        Assert.Equal("Pending", app.Db(db => db.Orders.First(o => o.Id == orderId).Status.ToString()));

        body["signature"] = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(TestApp.MoMoSecret), Encoding.UTF8.GetBytes(raw)));
        Assert.Equal(HttpStatusCode.NoContent, (await app.Anonymous().PostAsJsonAsync("/api/payments/momo/ipn", body)).StatusCode);
        Assert.Equal("Paid", app.Db(db => db.Orders.First(o => o.Id == orderId).Status.ToString()));
        Assert.Equal(1, app.Db(db => db.Enrollments.Count(e => e.User.Email == email)));
    }

    [Fact]
    public async Task Admin_lists_transactions()
    {
        var admin = await app.LoginAsync("admin@eduviet.local");
        var list = await (await admin.GetAsync("/api/admin/payments?pageSize=50")).OkJsonAsync();
        Assert.True(list.GetProperty("totalCount").GetInt32() > 0);
        var student = await app.LoginAsync("hoangnam@eduviet.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/admin/payments")).StatusCode);
    }

    // ---------- helpers: ký như VNPay ----------
    private static string VnPaySign(IEnumerable<KeyValuePair<string, string>> fields)
    {
        var data = string.Join('&', fields.Where(kv => kv.Key.StartsWith("vnp_") && kv.Key != "vnp_SecureHashType")
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));
        return Convert.ToHexStringLower(HMACSHA512.HashData(Encoding.UTF8.GetBytes(TestApp.VnPaySecret), Encoding.UTF8.GetBytes(data)));
    }

    private static string Signed(Dictionary<string, string> fields)
    {
        var q = string.Join('&', fields.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));
        return $"{q}&vnp_SecureHash={VnPaySign(fields)}";
    }
}

[Collection(nameof(ApiCollection))]
public sealed class UploadTests(TestApp app)
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
    // Đầu tệp MP4 tối thiểu: hộp 'ftyp'
    private static readonly byte[] Mp4 = [0, 0, 0, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m', 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    private static MultipartFormDataContent Form(byte[] bytes, string name)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", name);
        return form;
    }

    [Fact]
    public async Task Image_upload_validates_type_and_serves_publicly()
    {
        var student = await app.LoginAsync("minhtuan@eduviet.local");
        var res = await (await student.PostAsync("/api/uploads/image", Form(Png, "avatar.png"))).OkJsonAsync();
        var url = res.GetProperty("url").GetString()!;
        Assert.StartsWith("/uploads/images/", url);
        Assert.Equal(HttpStatusCode.OK, (await app.Anonymous().GetAsync(url)).StatusCode);

        // Đổi đuôi .png nhưng nội dung không phải ảnh → 400
        Assert.Equal(HttpStatusCode.BadRequest, (await student.PostAsync("/api/uploads/image", Form(Encoding.UTF8.GetBytes("<script>alert(1)</script>"), "x.png"))).StatusCode);
        // Định dạng không cho phép
        Assert.Equal(HttpStatusCode.BadRequest, (await student.PostAsync("/api/uploads/image", Form(Png, "x.exe"))).StatusCode);
        // Học viên không được tải video
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsync("/api/uploads/video", Form(Mp4, "bai.mp4"))).StatusCode);

        // Dùng ảnh đã tải làm ảnh đại diện
        var profile = await (await student.PutAsJsonAsync("/api/account/profile", new { fullName = "Vũ Minh Tuấn", avatarUrl = url })).OkJsonAsync();
        Assert.Equal(url, profile.GetProperty("avatarUrl").GetString());
    }

    [Fact]
    public async Task Private_video_is_only_reachable_through_signed_url_for_enrolled_users()
    {
        var instructor = await app.LoginAsync("giangvien@eduviet.local");
        var up = await (await instructor.PostAsync("/api/uploads/video", Form(Mp4, "bai-hoc.mp4"))).OkJsonAsync();
        var publicId = up.GetProperty("publicId").GetString()!;
        Assert.Equal(JsonValueKind.Null, up.GetProperty("url").ValueKind); // video không có URL công khai

        // Gắn video vào một bài học (khóa nháp mới)
        var categoryId = app.Db(db => db.Categories.First().Id);
        var course = await (await instructor.PostAsJsonAsync("/api/instructor/courses", new { title = $"Video riêng {Guid.NewGuid():N}"[..20], categoryId, level = "Beginner", price = 0 })).OkJsonAsync();
        var courseId = course.GetProperty("id").GetGuid();
        var section = await (await instructor.PostAsJsonAsync($"/api/instructor/courses/{courseId}/sections", new { title = "Chương 1" })).OkJsonAsync();
        var lesson = await (await instructor.PostAsJsonAsync($"/api/instructor/sections/{section.GetProperty("id").GetGuid()}/lessons",
            new { title = "Bài video", type = "Video", videoPublicId = publicId, durationSeconds = 60, isPreview = false })).OkJsonAsync();
        var lessonId = lesson.GetProperty("id").GetGuid();

        // Người chưa ghi danh không lấy được bài học
        var outsider = await app.LoginAsync("ducthinh@eduviet.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/learning/lessons/{lessonId}")).StatusCode);

        // Giảng viên (xem trước) nhận URL có chữ ký và phát được
        var content = await (await instructor.GetAsync($"/api/learning/lessons/{lessonId}")).OkJsonAsync();
        var videoUrl = content.GetProperty("videoUrl").GetString()!;
        Assert.StartsWith("/api/media/", videoUrl);
        Assert.Contains("sig=", videoUrl);
        var media = await app.Anonymous().GetAsync(videoUrl);
        Assert.Equal(HttpStatusCode.OK, media.StatusCode);
        Assert.Equal(Mp4.Length, (await media.Content.ReadAsByteArrayAsync()).Length);

        // Sửa chữ ký / hết hạn / truy cập thẳng đều bị chặn
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Anonymous().GetAsync(videoUrl[..^4] + "0000")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Anonymous().GetAsync($"/api/media/{publicId}?exp=1&sig=abc")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Anonymous().GetAsync($"/uploads/{publicId}")).StatusCode);
    }
}
