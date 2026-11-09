using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace QLyCuaHangSuaXe.Models;

public sealed class YeuCauDieuChinhTon
{
    [Required(ErrorMessage = "Chọn phụ tùng cần kiểm kê."), StringLength(10)]
    [JsonPropertyName("partId")]
    public string MaPhuTung { get; set; } = "";

    [Range(0, 1000000, ErrorMessage = "Tồn thực tế từ 0 đến 1.000.000.")]
    [JsonPropertyName("stock")]
    public int TonKho { get; set; }

    [
        Required(ErrorMessage = "Nhập lý do điều chỉnh."),
        StringLength(255, MinimumLength = 5, ErrorMessage = "Lý do điều chỉnh từ 5 đến 255 ký tự.")
    ]
    [JsonPropertyName("reason")]
    public string LyDo { get; set; } = "";
}
