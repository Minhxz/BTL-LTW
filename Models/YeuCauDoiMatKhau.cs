using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace QLyCuaHangSuaXe.Models;

// Người 1: đổi mật khẩu của tài khoản đang đăng nhập.
public sealed class YeuCauDoiMatKhau
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
    [JsonPropertyName("oldPassword")]
    public string MatKhauCu { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới."), StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới từ 6 đến 100 ký tự.")]
    [JsonPropertyName("newPassword")]
    public string MatKhauMoi { get; set; } = "";
}
