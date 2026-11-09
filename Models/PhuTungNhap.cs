using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace QLyCuaHangSuaXe.Models;

public sealed class PhuTungNhap
{
    [Required(ErrorMessage = "Chọn phụ tùng cần nhập."), StringLength(10)]
    [JsonPropertyName("partId")]
    public string MaPhuTung { get; set; } = "";

    [Range(1, 100000, ErrorMessage = "Số lượng nhập từ 1 đến 100.000.")]
    [JsonPropertyName("quantity")]
    public int SoLuong { get; set; }
}
