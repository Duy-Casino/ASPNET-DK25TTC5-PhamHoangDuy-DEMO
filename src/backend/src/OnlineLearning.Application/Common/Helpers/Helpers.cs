using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ganss.Xss;

namespace OnlineLearning.Application.Common.Helpers;

public static partial class SlugHelper
{
    /// <summary>"Lập trình C# cơ bản" → "lap-trinh-c-co-ban".</summary>
    public static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var text = input.Trim().ToLowerInvariant().Replace('đ', 'd');
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var slug = NonAlphaNumeric().Replace(sb.ToString().Normalize(NormalizationForm.FormC), "-").Trim('-');
        return slug.Length > 200 ? slug[..200].TrimEnd('-') : slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphaNumeric();
}

public static class CodeGenerator
{
    // Bỏ các ký tự dễ nhầm: 0/O, 1/I/L
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public static string Random(int length)
    {
        Span<char> chars = stackalloc char[length];
        for (var i = 0; i < length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

    /// <summary>Mã đơn hàng: EDV + yyMMdd + 6 ký tự, ví dụ EDV261104K7M2QX.</summary>
    public static string OrderCode(DateTime utcNow) => $"EDV{utcNow:yyMMdd}{Random(6)}";

    /// <summary>Mã chứng chỉ: EDV-2026-7F3K9QX2.</summary>
    public static string CertificateCode(DateTime utcNow) => $"EDV-{utcNow:yyyy}-{Random(8)}";
}

public static class ContentSanitizer
{
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    private static HtmlSanitizer CreateSanitizer()
    {
        var s = new HtmlSanitizer();
        s.AllowedSchemes.Clear();
        s.AllowedSchemes.Add("https");
        s.AllowedSchemes.Add("http");
        s.AllowedSchemes.Add("mailto");
        s.AllowedAttributes.Remove("style");
        s.AllowedCssProperties.Clear();
        return s;
    }

    /// <summary>Lọc HTML do người dùng nhập (mô tả khóa học, bài viết) để chống XSS.</summary>
    public static string? SanitizeHtml(string? html) =>
        string.IsNullOrWhiteSpace(html) ? null : Sanitizer.Sanitize(html);

    public static string? ToJsonList(IEnumerable<string>? items)
    {
        var list = items?.Select(i => i.Trim()).Where(i => i.Length > 0).ToList();
        return list is { Count: > 0 } ? JsonSerializer.Serialize(list) : null;
    }

    public static List<string> FromJsonList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
