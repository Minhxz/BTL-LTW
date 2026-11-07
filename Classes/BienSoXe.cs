namespace QLyCuaHangSuaXe.LopDungChung;

public static class BienSoXe
{
    public static string ChuanHoa(string bienSo)
    {
        var key = bienSo.Trim().ToUpperInvariant().Replace(" ", "").Replace("\t", "").Replace("-", "").Replace(".", "");
        if (key.Length == 0 || !key.All(char.IsAsciiLetterOrDigit))
            throw new ArgumentException("Biển số xe chỉ được gồm chữ, số, dấu gạch và dấu chấm.");
        return key;
    }

    // So sánh cả biển số đã lưu trước đây, giữ nguyên cách hiển thị trên phiếu.
    public static string KhoaSql(string cot) => $"UPPER(REPLACE(REPLACE(REPLACE(REPLACE({cot}, ' ', ''), CHAR(9), ''), '-', ''), '.', ''))";
}
