using System.Globalization;
using OnlineLearning.Application.Features.Orders;
using OnlineLearning.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OnlineLearning.Infrastructure.Certificates;

/// <summary>
/// Biên nhận thanh toán khổ A4 dọc: bên bán, người mua, bảng khóa học, giảm giá, tổng tiền, phương thức thanh toán.
/// Đây là chứng từ xác nhận giao dịch, không thay thế hóa đơn điện tử theo quy định thuế.
/// Đơn đã hoàn tiền có dấu "ĐÃ HOÀN TIỀN".
/// </summary>
public sealed class QuestPdfInvoiceGenerator : IInvoicePdfGenerator
{
    private const string Brand = "#005CF8";
    private const string Ink = "#0B1B33";
    private const string Muted = "#5B6B82";
    private const string Line = "#E2E8F0";
    private static readonly string[] Fonts = ["Arial", "Segoe UI", "DejaVu Sans", "Noto Sans", "Liberation Sans"];
    private static readonly CultureInfo Vi = new("vi-VN");

    static QuestPdfInvoiceGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
    }

    private static string M(decimal v) => v.ToString("#,0", Vi) + "đ";
    private static string D(DateTime utc) => utc.AddHours(7).ToString("HH:mm dd/MM/yyyy", Vi);

    public byte[] Generate(InvoiceDocument d) =>
        Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(t => t.FontFamily(Fonts).FontSize(10).FontColor(Ink));

            page.Header().Column(col =>
            {
                col.Item().Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text(d.SellerName).FontSize(20).Bold().FontColor(Brand);
                        if (d.SellerAddress is { Length: > 0 }) c.Item().Text(d.SellerAddress).FontColor(Muted);
                        var contact = string.Join(" · ", new[] { d.SellerEmail, d.SellerPhone }.Where(x => !string.IsNullOrWhiteSpace(x)));
                        if (contact.Length > 0) c.Item().Text(contact).FontColor(Muted);
                        if (d.SellerTaxCode is { Length: > 0 }) c.Item().Text($"Mã số thuế: {d.SellerTaxCode}").FontColor(Muted);
                    });
                    r.AutoItem().AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text("BIÊN NHẬN THANH TOÁN").FontSize(15).Bold();
                        c.Item().AlignRight().Text($"Số: {d.OrderCode}").SemiBold();
                        c.Item().AlignRight().Text($"Ngày thanh toán: {D(d.PaidAt)}").FontColor(Muted);
                    });
                });
                col.Item().PaddingVertical(12).LineHorizontal(1).LineColor(Line);
            });

            page.Content().Layers(layers =>
            {
                layers.PrimaryLayer().Column(col =>
                {
                    col.Spacing(14);
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Người mua").FontColor(Muted);
                            c.Item().Text(d.BuyerName).FontSize(12).SemiBold();
                            c.Item().Text(d.BuyerEmail);
                        });
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Thanh toán").FontColor(Muted);
                            c.Item().Text(d.PaymentMethod).SemiBold();
                            if (d.TransactionRef is { } tr) c.Item().Text($"Mã giao dịch: {tr}");
                            if (d.ProviderTransactionId is { } pt) c.Item().Text($"Mã giao dịch cổng: {pt}");
                        });
                    });

                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(24);
                            c.RelativeColumn(5);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                        });
                        t.Header(h =>
                        {
                            static IContainer Head(IContainer c) => c.Background("#F1F5F9").PaddingVertical(6).PaddingHorizontal(4);
                            h.Cell().Element(Head).Text("#").SemiBold();
                            h.Cell().Element(Head).Text("Khóa học").SemiBold();
                            h.Cell().Element(Head).AlignRight().Text("Đơn giá").SemiBold();
                            h.Cell().Element(Head).AlignRight().Text("Giảm giá").SemiBold();
                            h.Cell().Element(Head).AlignRight().Text("Thành tiền").SemiBold();
                        });
                        var i = 0;
                        foreach (var l in d.Lines)
                        {
                            i++;
                            IContainer Cell(IContainer c) => c.BorderBottom(1).BorderColor(Line).PaddingVertical(6).PaddingHorizontal(4);
                            t.Cell().Element(Cell).Text(i.ToString(Vi));
                            t.Cell().Element(Cell).Column(c =>
                            {
                                c.Item().Text(l.CourseTitle).SemiBold();
                                if (l.InstructorName is { } n) c.Item().Text($"Giảng viên: {n}").FontSize(9).FontColor(Muted);
                            });
                            t.Cell().Element(Cell).AlignRight().Column(c =>
                            {
                                if (l.OriginalPrice > l.Price) c.Item().AlignRight().Text(M(l.OriginalPrice)).FontSize(9).FontColor(Muted).Strikethrough();
                                c.Item().AlignRight().Text(M(l.Price));
                            });
                            t.Cell().Element(Cell).AlignRight().Text(l.Discount > 0 ? "-" + M(l.Discount) : "—");
                            t.Cell().Element(Cell).AlignRight().Text(M(l.FinalPrice));
                        }
                    });

                    col.Item().AlignRight().Width(260).Column(c =>
                    {
                        void Row(string label, string value, bool bold = false)
                        {
                            c.Item().PaddingVertical(2).Row(r =>
                            {
                                var left = r.RelativeItem().Text(label);
                                var right = r.AutoItem().Text(value);
                                if (bold) { left.Bold().FontSize(12); right.Bold().FontSize(12).FontColor(Brand); }
                            });
                        }
                        Row("Tạm tính", M(d.SubTotal));
                        if (d.DiscountAmount > 0) Row($"Mã giảm giá{(d.CouponCode is { } cc ? $" ({cc})" : "")}", "-" + M(d.DiscountAmount));
                        if (d.PointsDiscountAmount > 0) Row($"Điểm thưởng ({d.PointsUsed.ToString("#,0", Vi)} điểm)", "-" + M(d.PointsDiscountAmount));
                        c.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Line);
                        Row("Tổng thanh toán", M(d.TotalAmount), bold: true);
                    });

                    if (d.Status == OrderStatus.Refunded && d.RefundedAt is { } rf)
                        col.Item().Text($"Đơn hàng đã được hoàn tiền lúc {D(rf)}.").FontColor("#D92D20").SemiBold();
                });

                if (d.Status == OrderStatus.Refunded)
                {
                    layers.Layer().AlignCenter().AlignMiddle().Rotate(-20)
                        .Border(3).BorderColor("#D92D20").PaddingHorizontal(20).PaddingVertical(6)
                        .Text("ĐÃ HOÀN TIỀN").FontSize(40).Bold().FontColor("#D92D20");
                }
            });

            page.Footer().Column(c =>
            {
                c.Item().LineHorizontal(1).LineColor(Line);
                c.Item().PaddingTop(6).Text(t =>
                {
                    t.Span("Biên nhận xác nhận giao dịch trên EduViet, không thay thế hóa đơn giá trị gia tăng. Xem đơn hàng: ").FontSize(8).FontColor(Muted);
                    t.Hyperlink(d.OrderUrl, d.OrderUrl).FontSize(8).FontColor(Brand);
                });
            });
        })).GeneratePdf();
}
