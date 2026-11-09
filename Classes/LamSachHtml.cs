using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.LopDungChung;

public static class LamSachHtml
{
    private static readonly HashSet<string> CacTheChoPhep =
    [
        "p",
        "br",
        "strong",
        "em",
        "ul",
        "ol",
        "li",
    ];

    public static string LocNoiDung(string? noiDungHtml)
    {
        if (string.IsNullOrEmpty(noiDungHtml))
            return "";
        noiDungHtml = Regex.Replace(
            noiDungHtml,
            @"<(\/?)b\s*>",
            "<$1strong>",
            RegexOptions.IgnoreCase
        );
        noiDungHtml = Regex.Replace(noiDungHtml, @"<(\/?)i\s*>", "<$1em>", RegexOptions.IgnoreCase);
        var ketQua = new StringBuilder();
        var viTri = 0;
        foreach (Match doanMa in Regex.Matches(noiDungHtml, "<[^>]*>"))
        {
            ketQua.Append(
                WebUtility.HtmlEncode(WebUtility.HtmlDecode(noiDungHtml[viTri..doanMa.Index]))
            );
            var theHtml = Regex.Match(doanMa.Value, @"^<\s*(/?)\s*([a-zA-Z][a-zA-Z0-9]*)\b[^>]*>$");
            if (
                theHtml.Success
                && CacTheChoPhep.Contains(theHtml.Groups[2].Value.ToLowerInvariant())
            )
                ketQua
                    .Append('<')
                    .Append(theHtml.Groups[1].Value)
                    .Append(theHtml.Groups[2].Value.ToLowerInvariant())
                    .Append('>');
            viTri = doanMa.Index + doanMa.Length;
        }

        ketQua.Append(WebUtility.HtmlEncode(WebUtility.HtmlDecode(noiDungHtml[viTri..])));
        return ketQua.ToString();
    }
}
