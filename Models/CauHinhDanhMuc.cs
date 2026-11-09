using System.Text.Json.Serialization;

namespace QLyCuaHangSuaXe.Models;

public sealed record CauHinhDanhMuc(
    [property: JsonPropertyName("table")] string Bang,
    [property: JsonPropertyName("title")] string TieuDe,
    [property: JsonPropertyName("keys")] string[] Khoa,
    [property: JsonPropertyName("fields")] TruongDanhMuc[] CacTruong,
    [property: JsonPropertyName("roles")] string VaiTro = "Manager"
);
