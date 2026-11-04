using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Models;
public sealed class YeuCauDatLich
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên."), StringLength(100, MinimumLength = 2)]
    [JsonPropertyName("customerName")]
    public string TenKhachHang { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại."), StringLength(30)]
    [JsonPropertyName("phone")]
    public string SoDienThoai { get; set; } = "";

    [Required, StringLength(50)]
    [JsonPropertyName("vehicleType")]
    public string LoaiXe { get; set; } = "";

    [Required, StringLength(50)]
    [JsonPropertyName("brand")]
    public string HangXe { get; set; } = "";

    [StringLength(20)]
    [JsonPropertyName("licensePlate")]
    public string? BienSo { get; set; }

    [Required, StringLength(150)]
    [JsonPropertyName("service")]
    public string MaDichVu { get; set; } = "";

    [Required, StringLength(10)]
    [JsonPropertyName("date")]
    public string Ngay { get; set; } = "";

    [Required, StringLength(5)]
    [JsonPropertyName("time")]
    public string Gio { get; set; } = "";

    [StringLength(1000, ErrorMessage = "Ghi chú tối đa 1.000 ký tự.")]
    [JsonPropertyName("note")]
    public string? GhiChu { get; set; }
}
