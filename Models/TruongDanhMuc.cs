using System.Text.Json.Serialization;

namespace QLyCuaHangSuaXe.Models;

// Một trường nhập của danh mục.
public sealed record TruongDanhMuc(
    // Tên cột SQL.
    [property: JsonPropertyName("name")]
    string Ten,

    // Nhãn hiển thị trên form.
    [property: JsonPropertyName("label")]
    string Nhan,

    // Kiểu nhập: text, code, phone, money, int, date, time, bool, select, password...
    [property: JsonPropertyName("type")]
    string Kieu = "text",

    [property: JsonPropertyName("max")]
    int DoDaiToiDa = 150,

    // Danh mục nguồn cho ô chọn (Kieu = "select").
    [property: JsonPropertyName("source")]
    string? Nguon = null,

    // Chỉ hiển thị, không cho nhập (ví dụ tồn kho, số giờ tự tính).
    [property: JsonPropertyName("readOnly")]
    bool ChiDoc = false,

    [property: JsonPropertyName("optional")]
    bool KhongBatBuoc = false);
