using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace QLyCuaHangSuaXe.Models;

// Người 3 và Người 5: dữ liệu gửi lên cho tiếp nhận, sửa chữa và thanh toán.

// Một dịch vụ được chọn trên phiếu tiếp nhận.
public sealed record DichVuChon(
    [Required, StringLength(10)]
    [property: JsonPropertyName("serviceId")]
    string MaDichVu,

    [Range(1, 1000)]
    [property: JsonPropertyName("quantity")]
    int SoLuong);

// Thông tin tiếp nhận xe: khách hàng, xe, ngày hẹn trả, dịch vụ và thợ (không bắt buộc).
public sealed record ThongTinTiepNhan(
    [Required, StringLength(100, MinimumLength = 2)]
    [property: JsonPropertyName("customerName")]
    string TenKhachHang,

    [Required, RegularExpression(@"^0[0-9]{8,10}$")]
    [property: JsonPropertyName("phone")]
    string SoDienThoai,

    [Required, StringLength(20)]
    [property: JsonPropertyName("licensePlate")]
    string BienSo,

    [Required, StringLength(50)]
    [property: JsonPropertyName("vehicleType")]
    string LoaiXe,

    [Required, StringLength(50)]
    [property: JsonPropertyName("brand")]
    string HangXe,

    [Required, StringLength(30)]
    [property: JsonPropertyName("color")]
    string MauSac,

    [property: JsonPropertyName("dueDate")]
    DateTime NgayHenTra,

    [Required, MinLength(1), MaxLength(30)]
    [property: JsonPropertyName("services")]
    List<DichVuChon> DichVu,

    [StringLength(10)]
    [property: JsonPropertyName("mechanicId")]
    string? MaTho = null);

// Xem trước tổng tiền và phụ tùng trước khi tiếp nhận.
public sealed record YeuCauXemTruocPhieu(
    [Required, StringLength(50)]
    [property: JsonPropertyName("vehicleType")]
    string LoaiXe,

    [Required, MinLength(1), MaxLength(30)]
    [property: JsonPropertyName("services")]
    List<DichVuChon> DichVu);

public sealed record YeuCauTrangThai(
    [Required]
    [property: JsonPropertyName("status")]
    string TrangThai);

public sealed record YeuCauPhanCong(
    [Required, StringLength(10)]
    [property: JsonPropertyName("employeeId")]
    string MaNhanVien,

    [property: JsonPropertyName("start")]
    DateTime BatDau,

    [property: JsonPropertyName("end")]
    DateTime KetThuc);

// Người 5: ngày thu tiền khi lập hóa đơn.
public sealed record YeuCauThanhToan(
    [property: JsonPropertyName("paidOn")]
    DateTime NgayThanhToan);

public sealed record YeuCauNgayHen(
    [property: JsonPropertyName("dueDate")]
    DateTime NgayHenTra);
