using System.Text.Json.Serialization;

namespace QLyCuaHangSuaXe.Models;

public sealed record TruongDanhMuc(
    [property: JsonPropertyName("name")] string Ten,
    [property: JsonPropertyName("label")] string Nhan,
    [property: JsonPropertyName("type")] string Kieu = "text",
    [property: JsonPropertyName("max")] int DoDaiToiDa = 150,
    [property: JsonPropertyName("source")] string? Nguon = null,
    [property: JsonPropertyName("readOnly")] bool ChiDoc = false,
    [property: JsonPropertyName("optional")] bool KhongBatBuoc = false
);
