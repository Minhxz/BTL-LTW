using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace QLyCuaHangSuaXe.Models;

// Người 4: dữ liệu gửi lên khi lập phiếu nhập kho (một phiếu nhập nhiều phụ tùng).
public sealed class ThongTinPhieuNhap
{
    [JsonPropertyName("date")]
    public DateTime Ngay { get; set; }

    [StringLength(255, ErrorMessage = "Ghi chú tối đa 255 ký tự.")]
    [JsonPropertyName("note")]
    public string? GhiChu { get; set; }

    [Required, MinLength(1, ErrorMessage = "Phiếu nhập phải có ít nhất 1 phụ tùng."), MaxLength(100, ErrorMessage = "Mỗi phiếu nhập tối đa 100 dòng.")]
    [JsonPropertyName("lines")]
    public List<PhuTungNhap> ChiTiet { get; set; } = [];
}
