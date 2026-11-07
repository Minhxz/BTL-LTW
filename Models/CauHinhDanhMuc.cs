using System.Text.Json.Serialization;

namespace QLyCuaHangSuaXe.Models;

// Cấu hình một danh mục dùng bộ thêm/sửa/xóa chung. Khai báo cụ thể ở DinhNghiaDanhMuc.
public sealed record CauHinhDanhMuc(
    [property: JsonPropertyName("table")]
    string Bang,

    [property: JsonPropertyName("title")]
    string TieuDe,

    // Cột khóa chính (có thể nhiều cột).
    [property: JsonPropertyName("keys")]
    string[] Khoa,

    [property: JsonPropertyName("fields")]
    TruongDanhMuc[] CacTruong,

    // Nhóm quyền được xem/sửa, cách nhau bằng dấu phẩy.
    [property: JsonPropertyName("roles")]
    string VaiTro = "Manager");
