using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace QLyCuaHangSuaXe.Models;

// Người 1: dữ liệu form đăng nhập.
public sealed class ThongTinDangNhap
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập."), StringLength(50)]
    [JsonPropertyName("username")]
    public string TenDangNhap { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu."), StringLength(200)]
    [JsonPropertyName("password")]
    public string MatKhau { get; set; } = "";
}
