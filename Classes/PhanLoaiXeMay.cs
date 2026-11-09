namespace QLyCuaHangSuaXe.LopDungChung;

public static class PhanLoaiXeMay
{
    public static IReadOnlyDictionary<string, string> MauXe { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Vision"] = "tay ga",
            ["Air Blade 125"] = "tay ga",
            ["Lead 125"] = "tay ga",
            ["Wave Alpha 110"] = "số",
            ["Future 125"] = "số",
            ["SH Mode 125"] = "tay ga",
            ["Winner X"] = "côn tay",
            ["Janus"] = "tay ga",
            ["Sirius"] = "số",
            ["Grande"] = "tay ga",
            ["Exciter 155"] = "côn tay",
            ["Raider R150"] = "côn tay",
            ["Liberty 125"] = "tay ga",
            ["Attila 50"] = "tay ga",
        };

    public static void KiemTraDichVu(string mauXe, string dichVu)
    {
        var mauXeChuanHoa = mauXe.Replace(" ", "").Replace("-", "");
        var nhom = MauXe
            .FirstOrDefault(phanTu =>
            {
                var dongXe = new string(
                    phanTu.Key.TakeWhile(doiTuong => !char.IsAsciiDigit(doiTuong)).ToArray()
                ).Replace(" ", "");
                return mauXeChuanHoa.StartsWith(dongXe, StringComparison.OrdinalIgnoreCase);
            })
            .Value;
        if (nhom == null)
            return;
        var tayGa = dichVu.Contains("xe tay ga", StringComparison.OrdinalIgnoreCase);
        var xeSo = dichVu.Contains("xe số", StringComparison.OrdinalIgnoreCase);
        var conTay = dichVu.Contains("côn tay", StringComparison.OrdinalIgnoreCase);
        if (!(tayGa || xeSo || conTay))
            return;
        var phuHop = nhom switch
        {
            "tay ga" => tayGa,
            "số" => xeSo,
            _ => conTay,
        };
        if (!phuHop)
            throw new ArgumentException(
                $"Dịch vụ «{dichVu}» không phù hợp với mẫu {mauXe} (xe {nhom})."
            );
    }
}
