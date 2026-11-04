using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Models;
public sealed record DichVuChon([Required, StringLength(10)][property: JsonPropertyName("serviceId")] string MaDichVu, [Range(1, 1000)][property: JsonPropertyName("quantity")] int SoLuong);
public sealed record ThongTinTiepNhan([Required, StringLength(100, MinimumLength = 2)][property: JsonPropertyName("customerName")] string TenKhachHang, [Required, RegularExpression(@"^0[0-9]{8,10}$")][property: JsonPropertyName("phone")] string SoDienThoai, [Required, StringLength(20)][property: JsonPropertyName("licensePlate")] string BienSo, [Required, StringLength(50)][property: JsonPropertyName("vehicleType")] string LoaiXe, [Required, StringLength(50)][property: JsonPropertyName("brand")] string HangXe, [Required, StringLength(30)][property: JsonPropertyName("color")] string MauSac, [property: JsonPropertyName("dueDate")] DateTime NgayHenTra, [Required, MinLength(1), MaxLength(30)][property: JsonPropertyName("services")] List<DichVuChon> DichVu, [StringLength(10)][property: JsonPropertyName("mechanicId")] string? MaTho = null);
public sealed record YeuCauTrangThai([Required][property: JsonPropertyName("status")] string TrangThai);
public sealed record YeuCauPhanCong([Required, StringLength(10)][property: JsonPropertyName("employeeId")] string MaNhanVien, [property: JsonPropertyName("start")] DateTime BatDau, [property: JsonPropertyName("end")] DateTime KetThuc);
public sealed record YeuCauThanhToan([property: JsonPropertyName("paidOn")] DateTime NgayThanhToan);
public sealed record YeuCauNgayHen([property: JsonPropertyName("dueDate")] DateTime NgayHenTra);
