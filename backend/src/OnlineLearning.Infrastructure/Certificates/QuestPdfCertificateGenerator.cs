using System.Globalization;
using OnlineLearning.Application.Features.Certificates;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OnlineLearning.Infrastructure.Certificates;

/// <summary>
/// Chứng chỉ PDF khổ A4 ngang: dải thương hiệu bên trái, tên học viên lớn, thông tin khóa học,
/// mã chứng chỉ + QR dẫn tới trang xác minh công khai. Chứng chỉ đã thu hồi có dấu "ĐÃ THU HỒI".
/// Font: dùng font hệ thống có đủ dấu tiếng Việt (Arial/Segoe UI trên Windows; DejaVu Sans/Noto Sans trên Linux).
/// </summary>
public sealed class QuestPdfCertificateGenerator : ICertificatePdfGenerator
{
    private const string Brand = "#005CF8";
    private const string BrandDark = "#004DC1";
    private const string BrandLight = "#EFF6FF";
    private const string Accent = "#EF8000";
    private const string Ink = "#0B1B33";
    private const string Muted = "#3D4A5C";
    private static readonly string[] Fonts = ["Arial", "Segoe UI", "DejaVu Sans", "Noto Sans", "Liberation Sans"];
    private static readonly CultureInfo Vi = new("vi-VN");

    static QuestPdfCertificateGenerator()
    {
        // Giấy phép Community: miễn phí cho tổ chức doanh thu < 1 triệu USD/năm (xem questpdf.com/license)
        QuestPDF.Settings.License = LicenseType.Community;
        // Dùng font cài trên máy chủ (Windows: Arial/Segoe UI; Linux: cài gói fonts-dejavu-core hoặc fonts-noto-core)
        QuestPDF.Settings.UseEnvironmentFonts = true;
        // Không dừng nếu thiếu một font trong danh sách — dùng font khả dụng đầu tiên
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;

        // Font đóng gói kèm ứng dụng (nếu có) để PDF giống nhau trên mọi máy chủ: đặt file .ttf vào thư mục Fonts/
        var fontDir = Path.Combine(AppContext.BaseDirectory, "Fonts");
        if (Directory.Exists(fontDir))
        {
            foreach (var file in Directory.EnumerateFiles(fontDir, "*.ttf"))
            {
                using var stream = File.OpenRead(file);
                QuestPDF.Drawing.FontManager.RegisterFont(stream);
            }
        }
    }

    public byte[] Generate(CertificateDocument d)
    {
        var qr = PngByteQRCodeHelper.GetQRCode(d.VerifyUrl, QRCodeGenerator.ECCLevel.Q, 12);
        var issued = d.IssuedAt.AddHours(7); // giờ Việt Nam
        var hours = d.TotalDurationSeconds / 3600.0;

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(0);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(t => t.FontFamily(Fonts).FontSize(12).FontColor(Ink));

            page.Content().Row(row =>
            {
                // Dải thương hiệu
                row.ConstantItem(70).Background(Brand).PaddingVertical(36).AlignCenter().Column(col =>
                {
                    col.Item().Width(34).Height(34).Background(Colors.White).AlignCenter().AlignMiddle()
                        .Text("E").FontSize(22).Bold().FontColor(Brand);
                    col.Item().PaddingTop(6).Width(10).Height(10).Background(Accent);
                });

                row.RelativeItem().Layers(layers =>
                {
                    layers.PrimaryLayer().Padding(48).Column(col =>
                    {
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Text("EduViet").FontSize(20).Bold().FontColor(Brand);
                            r.AutoItem().Text("Chứng chỉ hoàn thành khóa học").FontSize(14).SemiBold().FontColor(Muted);
                        });

                        col.Item().PaddingTop(56).Text("Chứng nhận").FontSize(14).FontColor(Muted);
                        col.Item().PaddingTop(6).Text(d.StudentName).FontSize(40).Bold().FontColor(BrandDark);
                        col.Item().PaddingTop(18).Text("đã hoàn thành khóa học").FontSize(14).FontColor(Muted);
                        col.Item().PaddingTop(6).Text(d.CourseTitle).FontSize(22).Bold();

                        col.Item().PaddingTop(36).Background(BrandLight).Padding(18).Row(r =>
                        {
                            Fact(r, "Giảng viên", d.InstructorName);
                            Fact(r, "Ngày cấp", issued.ToString("dd/MM/yyyy", Vi));
                            Fact(r, "Điểm kiểm tra", d.FinalScore is { } s ? $"{Math.Round(s):0}%" : "—");
                            Fact(r, "Thời lượng", $"{d.TotalLessons} bài · {hours.ToString("0.#", Vi)} giờ");
                        });

                        col.Item().ExtendVertical().AlignBottom().Row(r =>
                        {
                            r.RelativeItem().AlignBottom().Column(c =>
                            {
                                c.Item().Text("Mã chứng chỉ").FontSize(11).FontColor(Muted);
                                c.Item().Text(d.Code).FontSize(18).Bold().LetterSpacing(0.04f);
                                c.Item().PaddingTop(6).Text(t =>
                                {
                                    t.Span("Xác minh tại ").FontSize(10).FontColor(Muted);
                                    t.Hyperlink(d.VerifyUrl, d.VerifyUrl).FontSize(10).FontColor(Brand);
                                });
                            });
                            r.ConstantItem(96).Height(96).Image(qr);
                        });
                    });

                    if (d.IsRevoked)
                    {
                        layers.Layer().AlignCenter().AlignMiddle().Rotate(-20)
                            .Border(4).BorderColor("#D92D20").PaddingHorizontal(24).PaddingVertical(8)
                            .Text("ĐÃ THU HỒI").FontSize(56).Bold().FontColor("#D92D20");
                    }
                });
            });
        })).GeneratePdf();
    }

    private static void Fact(RowDescriptor row, string label, string value) =>
        row.RelativeItem().Column(c =>
        {
            c.Item().Text(label).FontSize(10).FontColor(Muted);
            c.Item().PaddingTop(2).Text(value).FontSize(13).SemiBold();
        });
}
