using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.LopDungChung;
public static class LamSachHtml
{
    private static readonly HashSet<string> Tags = ["p", "br", "strong", "em", "ul", "ol", "li"];
    // Mã hóa từng đoạn văn bản và chỉ dựng lại các thẻ được cho phép, không giữ thuộc tính.
    public static string LocNoiDung(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return "";
        html = Regex.Replace(html, @"<(\/?)b\s*>", "<$1strong>", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<(\/?)i\s*>", "<$1em>", RegexOptions.IgnoreCase);
        var result = new StringBuilder();
        var offset = 0;
        foreach (Match token in Regex.Matches(html, "<[^>]*>"))
        {
            result.Append(WebUtility.HtmlEncode(WebUtility.HtmlDecode(html[offset..token.Index])));
            var tag = Regex.Match(token.Value, @"^<\s*(/?)\s*([a-zA-Z][a-zA-Z0-9]*)\b[^>]*>$");
            if (tag.Success && Tags.Contains(tag.Groups[2].Value.ToLowerInvariant()))
                result.Append('<').Append(tag.Groups[1].Value).Append(tag.Groups[2].Value.ToLowerInvariant()).Append('>');
            offset = token.Index + token.Length;
        }

        result.Append(WebUtility.HtmlEncode(WebUtility.HtmlDecode(html[offset..])));
        return result.ToString();
    }
}
