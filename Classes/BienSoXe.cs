namespace QLyCuaHangSuaXe.LopDungChung;

public static class BienSoXe
{
    public static string ChuanHoa(string bienSo)
    {
        var khoaBanGhi = bienSo
            .Trim()
            .ToUpperInvariant()
            .Replace(" ", "")
            .Replace("\t", "")
            .Replace("-", "")
            .Replace(".", "");
        if (khoaBanGhi.Length == 0 || !khoaBanGhi.All(char.IsAsciiLetterOrDigit))
            throw new ArgumentException("Biển số xe chỉ được gồm chữ, số, dấu gạch và dấu chấm.");
        return khoaBanGhi;
    }

    public static string KhoaSql(string cot) =>
        $"UPPER(REPLACE(REPLACE(REPLACE(REPLACE({cot}, ' ', ''), CHAR(9), ''), '-', ''), '.', ''))";
}
