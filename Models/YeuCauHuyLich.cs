using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace QLyCuaHangSuaXe.Models;

public sealed class YeuCauHuyLich
{
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại."), StringLength(30)]
    [JsonPropertyName("phone")]
    public string SoDienThoai { get; set; } = "";
}
