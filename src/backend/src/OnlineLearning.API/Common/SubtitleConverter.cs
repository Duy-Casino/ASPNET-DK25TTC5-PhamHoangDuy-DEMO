using System.Text;
using System.Text.RegularExpressions;

namespace OnlineLearning.API.Common;

/// <summary>Chuẩn hóa phụ đề về WebVTT: nhận .vtt (kiểm tra) hoặc .srt (đổi dấu phẩy mili giây thành dấu chấm, bỏ số thứ tự).</summary>
public static partial class SubtitleConverter
{
    [GeneratedRegex(@"^\s*(\d{1,2}:)?\d{2}:\d{2}[.,]\d{3}\s*-->\s*(\d{1,2}:)?\d{2}:\d{2}[.,]\d{3}")]
    private static partial Regex CueTiming();

    public static string? ToWebVtt(string text, string ext)
    {
        text = text.Replace("\r\n", "\n").Replace('\r', '\n').TrimStart('\uFEFF');
        if (text.Contains('\0')) return null;
        var lines = text.Split('\n');
        if (!lines.Any(l => CueTiming().IsMatch(l))) return null;

        if (ext == ".vtt")
            return text.TrimStart().StartsWith("WEBVTT", StringComparison.Ordinal) ? text : null;

        var sb = new StringBuilder("WEBVTT\n\n");
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            // Bỏ dòng số thứ tự đứng ngay trước dòng thời gian
            if (int.TryParse(line.Trim(), out _) && i + 1 < lines.Length && CueTiming().IsMatch(lines[i + 1])) continue;
            if (CueTiming().IsMatch(line))
            {
                var parts = line.Split("-->");
                line = $"{Fix(parts[0])} --> {Fix(parts[1])}";
            }
            sb.Append(line).Append('\n');
        }
        return sb.ToString();

        static string Fix(string t)
        {
            t = t.Trim().Split(' ')[0].Replace(',', '.');
            return t.Count(c => c == ':') == 1 ? "00:" + t : t;
        }
    }
}
