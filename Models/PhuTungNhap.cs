using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace QLyCuaHangSuaXe.Models;

// Người 4: một dòng phụ tùng trong phiếu nhập kho.
// Không nhận đơn giá từ trình duyệt: máy chủ lấy giá nhập của lô phụ tùng.
public sealed class PhuTungNhap
{
    [Required(ErrorMessage = "Chọn phụ tùng cần nhập."), StringLength(10)]
    [JsonPropertyName("partId")]
    public string MaPhuTung { get; set; } = "";

    [Range(1, 100000, ErrorMessage = "Số lượng nhập từ 1 đến 100.000.")]
    [JsonPropertyName("quantity")]
    public int SoLuong { get; set; }
}
