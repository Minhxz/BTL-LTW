using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace QLyCuaHangSuaXe.Models;

public sealed class NoiDungDichVu
{
    [StringLength(8000, ErrorMessage = "Nội dung tối đa 8.000 ký tự.")]
    [JsonPropertyName("description")]
    public string? MoTa { get; set; }

    [StringLength(250)]
    [JsonPropertyName("imageUrl")]
    public string? DuongDanAnh { get; set; }
}
