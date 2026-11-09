using System.Globalization;
using System.Security;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OnlineLearning.Application.Common.Options;
using OnlineLearning.Application.Features.Courses;

namespace OnlineLearning.API.Controllers;

/// <summary>
/// sitemap.xml cho công cụ tìm kiếm: trang tĩnh công khai + khóa học đã xuất bản + trang giảng viên.
/// URL tuyệt đối lấy từ App:ClientUrl. Chỉ đọc, không cần đăng nhập.
/// </summary>
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class SitemapController(ICatalogService catalog, IOptions<AppOptions> app) : ControllerBase
{
    private static readonly string[] StaticPaths = ["/", "/khoa-hoc", "/xac-minh", "/gioi-thieu", "/lien-he",
        "/chinh-sach/dieu-khoan", "/chinh-sach/hoan-tien", "/chinh-sach/bao-mat"];

    [HttpGet("/sitemap.xml")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var baseUrl = app.Value.ClientUrl.TrimEnd('/');
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""");
        foreach (var path in StaticPaths) Append(sb, baseUrl + path, null);
        foreach (var e in await catalog.GetSitemapEntriesAsync(ct)) Append(sb, baseUrl + e.Path, e.LastModified);
        sb.AppendLine("</urlset>");
        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }

    private static void Append(StringBuilder sb, string loc, DateTime? lastModified)
    {
        sb.Append("  <url><loc>").Append(SecurityElement.Escape(loc)).Append("</loc>");
        if (lastModified is { } d)
            sb.Append("<lastmod>").Append(d.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append("</lastmod>");
        sb.AppendLine("</url>");
    }
}
