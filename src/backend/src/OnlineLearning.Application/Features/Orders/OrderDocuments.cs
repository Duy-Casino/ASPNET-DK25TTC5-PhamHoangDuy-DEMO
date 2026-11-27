using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Models;
using OnlineLearning.Application.Common.Options;
using OnlineLearning.Domain.Entities;
using OnlineLearning.Domain.Enums;

namespace OnlineLearning.Application.Features.Orders;

/// <summary>Thông tin bên bán in trên biên nhận (mục "Invoice" trong appsettings).</summary>
public sealed class InvoiceOptions
{
    public const string SectionName = "Invoice";

    public string SellerName { get; set; } = "EduViet";
    public string? SellerAddress { get; set; } = "TP. Hồ Chí Minh";
    public string? SellerTaxCode { get; set; }
    public string? SellerEmail { get; set; } = "hotro@eduviet.local";
    public string? SellerPhone { get; set; } = "1900 1234";
}

public sealed record InvoiceLine(string CourseTitle, string? InstructorName, decimal OriginalPrice, decimal Price, decimal Discount, decimal FinalPrice);

public sealed record InvoiceDocument(
    string OrderCode, DateTime CreatedAt, DateTime PaidAt, OrderStatus Status, DateTime? RefundedAt,
    string SellerName, string? SellerAddress, string? SellerTaxCode, string? SellerEmail, string? SellerPhone,
    string BuyerName, string BuyerEmail,
    IReadOnlyList<InvoiceLine> Lines,
    decimal SubTotal, decimal DiscountAmount, string? CouponCode, int PointsUsed, decimal PointsDiscountAmount, decimal TotalAmount,
    string PaymentMethod, string? TransactionRef, string? ProviderTransactionId, string OrderUrl);

public interface IInvoicePdfGenerator
{
    byte[] Generate(InvoiceDocument document);
}

public static class PaymentText
{
    public static string Provider(PaymentProvider p) => p switch
    {
        PaymentProvider.VNPay => "VNPay",
        PaymentProvider.MoMo => "Ví MoMo",
        PaymentProvider.Free => "Miễn phí (đơn 0đ)",
        _ => "Kênh thanh toán thử nghiệm",
    };

    public static string Money(decimal v) => v.ToString("#,0", CultureInfo.GetCultureInfo("vi-VN")) + "đ";
}

public interface IInvoiceService
{
    /// <summary>Biên nhận PDF của đơn đã thanh toán (học viên chủ đơn hoặc Admin).</summary>
    Task<Result<(byte[] Pdf, string FileName)>> GetPdfAsync(Guid orderId, Guid userId, bool isAdmin, CancellationToken ct = default);
}

public sealed class InvoiceService(IAppDbContext db, IInvoicePdfGenerator generator, IOptionsSnapshot<InvoiceOptions> invoice, IOptions<AppOptions> app)
    : IInvoiceService
{
    public async Task<Result<(byte[] Pdf, string FileName)>> GetPdfAsync(Guid orderId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var doc = await BuildAsync(db, orderId, invoice.Value, app.Value, ct);
        if (doc is null || (!isAdmin && doc.Value.UserId != userId)) return Error.NotFound("Không tìm thấy đơn hàng.");
        if (doc.Value.Doc is not { } d) return Error.Conflict("Chỉ xuất biên nhận cho đơn đã thanh toán.");
        return (generator.Generate(d), $"bien-nhan-{d.OrderCode}.pdf");
    }

    internal static async Task<(Guid UserId, InvoiceDocument? Doc)?> BuildAsync(
        IAppDbContext db, Guid orderId, InvoiceOptions seller, AppOptions app, CancellationToken ct)
    {
        var o = await db.Orders.AsNoTracking()
            .Include(x => x.Items).Include(x => x.Payments).Include(x => x.User)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (o is null) return null;
        if (o.Status is not (OrderStatus.Paid or OrderStatus.Refunded) || o.PaidAt is null) return (o.UserId, null);

        var instructorIds = o.Items.Select(i => i.InstructorId).Distinct().ToList();
        var instructors = await db.Users.AsNoTracking().Where(u => instructorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var payment = o.Payments.Where(p => p.Status == PaymentStatus.Success).OrderBy(p => p.PaidAt).FirstOrDefault();

        var doc = new InvoiceDocument(o.OrderCode, o.CreatedAt, o.PaidAt.Value, o.Status, o.RefundedAt,
            seller.SellerName, seller.SellerAddress, seller.SellerTaxCode, seller.SellerEmail, seller.SellerPhone,
            o.User.FullName, o.User.Email ?? "",
            o.Items.OrderBy(i => i.CourseTitle).Select(i => new InvoiceLine(i.CourseTitle, instructors.GetValueOrDefault(i.InstructorId),
                i.OriginalPrice, i.Price, i.DiscountAllocated, i.FinalPrice)).ToList(),
            o.SubTotal, o.DiscountAmount, o.CouponCode, o.PointsUsed, o.PointsDiscountAmount, o.TotalAmount,
            PaymentText.Provider(payment?.Provider ?? (o.TotalAmount == 0 ? PaymentProvider.Free : PaymentProvider.Test)),
            payment?.TransactionRef, payment?.ProviderTransactionId,
            $"{app.ClientUrl.TrimEnd('/')}/don-hang/{o.Id}");
        return (o.UserId, doc);
    }
}

/// <summary>Email gửi học viên khi thanh toán thành công và khi đơn được hoàn tiền. Lỗi gửi mail không làm hỏng giao dịch.</summary>
public interface IOrderEmailService
{
    Task SendReceiptAsync(Guid orderId, CancellationToken ct = default);
    Task SendRefundedAsync(Guid orderId, CancellationToken ct = default);
    /// <summary>Email cho người nhận quà: lời nhắn, danh sách khóa học, liên kết nhận quà.</summary>
    Task SendGiftAsync(Guid orderId, CancellationToken ct = default);
}

public sealed class OrderEmailService(
    IAppDbContext db, IEmailSender sender, IOptionsSnapshot<InvoiceOptions> invoice, IOptions<AppOptions> app, ILogger<OrderEmailService> logger)
    : IOrderEmailService
{
    public async Task SendReceiptAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            var built = await InvoiceService.BuildAsync(db, orderId, invoice.Value, app.Value, ct);
            if (built?.Doc is not { } d) return;
            var client = app.Value.ClientUrl.TrimEnd('/');
            var sb = new StringBuilder();
            sb.Append($"<p>Chào {E(d.BuyerName)},</p>");
            sb.Append($"<p>Cảm ơn bạn đã mua khóa học tại {E(d.SellerName)}. Đơn hàng <b>{E(d.OrderCode)}</b> đã được thanh toán thành công " +
                      $"lúc {d.PaidAt.AddHours(7):HH:mm dd/MM/yyyy}.</p>");
            sb.Append("<table cellpadding=\"6\" cellspacing=\"0\" style=\"border-collapse:collapse;border:1px solid #e2e8f0;font-size:14px\">");
            sb.Append("<tr style=\"background:#f1f5f9\"><th align=\"left\">Khóa học</th><th align=\"right\">Đơn giá</th></tr>");
            foreach (var l in d.Lines)
                sb.Append($"<tr><td style=\"border-top:1px solid #e2e8f0\">{E(l.CourseTitle)}</td><td align=\"right\" style=\"border-top:1px solid #e2e8f0\">{PaymentText.Money(l.Price)}</td></tr>");
            if (d.DiscountAmount > 0)
                sb.Append($"<tr><td>Mã giảm giá {E(d.CouponCode)}</td><td align=\"right\">-{PaymentText.Money(d.DiscountAmount)}</td></tr>");
            if (d.PointsDiscountAmount > 0)
                sb.Append($"<tr><td>Giảm bằng {d.PointsUsed:N0} điểm thưởng</td><td align=\"right\">-{PaymentText.Money(d.PointsDiscountAmount)}</td></tr>");
            sb.Append($"<tr><td><b>Tổng thanh toán</b></td><td align=\"right\"><b>{PaymentText.Money(d.TotalAmount)}</b></td></tr></table>");
            sb.Append($"<p>Phương thức: {E(d.PaymentMethod)}{(d.TransactionRef is null ? "" : $" · Mã giao dịch: {E(d.TransactionRef)}")}</p>");
            sb.Append($"<p><a href=\"{client}/hoc-vien/khoa-hoc\">Bắt đầu học ngay</a> · <a href=\"{E(d.OrderUrl)}\">Xem đơn hàng và tải biên nhận</a></p>");
            sb.Append("<p style=\"color:#64748b;font-size:12px\">Bạn có thể yêu cầu hoàn tiền theo <a href=\"" + client +
                      "/chinh-sach/hoan-tien\">Chính sách hoàn tiền</a>.</p>");
            await sender.SendAsync(d.BuyerEmail, $"Xác nhận thanh toán đơn hàng {d.OrderCode}", sb.ToString(), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không gửi được email biên nhận cho đơn {OrderId}", orderId);
        }
    }

    public async Task SendRefundedAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            var o = await db.Orders.AsNoTracking().Include(x => x.User).FirstOrDefaultAsync(x => x.Id == orderId, ct);
            if (o is null || o.Status != OrderStatus.Refunded || string.IsNullOrEmpty(o.User.Email)) return;
            var body = $"<p>Chào {E(o.User.FullName)},</p>" +
                       $"<p>Đơn hàng <b>{E(o.OrderCode)}</b> đã được hoàn tiền {PaymentText.Money(o.TotalAmount)}. " +
                       "Tiền được hoàn về phương thức thanh toán ban đầu; thời gian tiền về tài khoản phụ thuộc ngân hàng hoặc ví.</p>" +
                       $"<p>Lý do: {E(o.RefundReason)}</p>" +
                       "<p>Quyền học các khóa trong đơn đã được thu hồi.</p>";
            await sender.SendAsync(o.User.Email, $"Đơn hàng {o.OrderCode} đã được hoàn tiền", body, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không gửi được email hoàn tiền cho đơn {OrderId}", orderId);
        }
    }

    public async Task SendGiftAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            var g = await db.CourseGifts.AsNoTracking().Include(x => x.Sender).Include(x => x.Order).ThenInclude(o => o.Items)
                .FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
            if (g is null || g.Status != GiftStatus.Sent) return;
            var client = app.Value.ClientUrl.TrimEnd('/');
            var sb = new StringBuilder();
            sb.Append($"<p>Chào {E(g.RecipientName)},</p>");
            sb.Append($"<p><b>{E(g.Sender.FullName)}</b> đã tặng bạn {(g.Order.Items.Count > 1 ? $"{g.Order.Items.Count} khóa học" : "một khóa học")} trên {E(invoice.Value.SellerName)}:</p><ul>");
            foreach (var i in g.Order.Items.OrderBy(i => i.CourseTitle)) sb.Append($"<li>{E(i.CourseTitle)}</li>");
            sb.Append("</ul>");
            if (!string.IsNullOrWhiteSpace(g.Message))
                sb.Append($"<blockquote style=\"border-left:3px solid #ef8000;margin:0;padding:8px 12px;color:#334155\">{E(g.Message)}</blockquote>");
            sb.Append($"<p><a href=\"{client}/qua-tang/{g.Code}\">Nhận quà tặng</a> (đăng nhập hoặc đăng ký bằng email <b>{E(g.RecipientEmail)}</b>).</p>");
            sb.Append($"<p style=\"color:#64748b;font-size:12px\">Mã quà tặng: {E(g.Code)}</p>");
            await sender.SendAsync(g.RecipientEmail, $"{g.Sender.FullName} tặng bạn khóa học trên {invoice.Value.SellerName}", sb.ToString(), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không gửi được email quà tặng cho đơn {OrderId}", orderId);
        }
    }

    private static string E(string? s) => System.Text.Encodings.Web.HtmlEncoder.Create(System.Text.Unicode.UnicodeRanges.All).Encode(s ?? "");
}
