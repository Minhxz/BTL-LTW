using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.LopDungChung;

public static class DinhNghiaDanhMuc
{
    public static readonly Dictionary<string, CauHinhDanhMuc> TatCa = new()
    {
        ["customers"] = new(
            "KhachHang",
            "Khách hàng",
            ["MaKhachHang"],
            [
                new("MaKhachHang", "Mã khách hàng", "code", 10),
                new("TenKhachHang", "Tên khách hàng", "text", 100),
                new("SoDienThoai", "Số điện thoại", "phone", 15),
            ],
            "Manager,Cashier"
        ),
        ["vehicles"] = new(
            "Xe",
            "Xe khách hàng",
            ["MaXe"],
            [
                new("MaXe", "Mã xe", "code", 10),
                new("MaKhachHang", "Khách hàng", "select", 10, "customers"),
                new("BienXe", "Biển số", "text", 20),
                new("LoaiXe", "Mẫu xe", "text", 50),
                new("HangSanXuat", "Hãng xe", "text", 50),
                new("MauSac", "Màu sắc", "text", 30),
            ],
            "Manager,Cashier"
        ),
        ["services"] = new(
            "DichVu",
            "Dịch vụ",
            ["MaDichVu"],
            [
                new("MaDichVu", "Mã dịch vụ", "code", 10),
                new("TenDichVu", "Tên dịch vụ"),
                new("MoTa", "Mô tả ngắn", "textarea", 255),
                new("ThanhTienDichVu", "Giá trọn gói", "money"),
                new("TrangThai", "Đang hoạt động", "bool"),
            ]
        ),
        ["serviceParts"] = new(
            "ChiTietDichVu",
            "Định mức phụ tùng",
            ["MaDichVu", "MaPhuTung"],
            [
                new("MaDichVu", "Dịch vụ", "select", 10, "services"),
                new("MaPhuTung", "Phụ tùng", "select", 10, "parts"),
                new("SoLuong", "Số lượng", "positiveInt"),
            ]
        ),
        ["suppliers"] = new(
            "NhaCungCap",
            "Nhà cung cấp",
            ["MaNhaCungCap"],
            [
                new("MaNhaCungCap", "Mã NCC", "code", 10),
                new("TenNhaCungCap", "Tên nhà cung cấp"),
                new("SoDienThoai", "Số điện thoại", "phone", 15),
                new("DiaChi", "Địa chỉ", "textarea", 255),
            ]
        ),
        ["parts"] = new(
            "PhuTung",
            "Phụ tùng",
            ["MaPhuTung"],
            [
                new("MaPhuTung", "Mã phụ tùng", "code", 10),
                new("MaNhaCungCap", "Nhà cung cấp", "select", 10, "suppliers"),
                new("TenPhuTung", "Tên phụ tùng"),
                new("DonGiaNhap", "Đơn giá nhập", "money"),
                new("DonGiaBan", "Đơn giá bán", "money"),
                new("SoLuongTon", "Tồn kho", "int", ChiDoc: true),
                new("HanSuDung", "Hạn sử dụng", "date"),
                new("TrangThai", "Đang hoạt động", "bool"),
            ]
        ),
        ["employees"] = new(
            "NhanVien",
            "Nhân viên & tài khoản",
            ["MaNhanVien"],
            [
                new("MaNhanVien", "Mã nhân viên", "code", 10),
                new("MaChucVu", "Chức vụ", "select", 10, "roles"),
                new("HoTen", "Họ tên", "text", 100),
                new("SoDienThoai", "Số điện thoại", "phone", 15),
                new("Luong", "Lương cơ bản", "money"),
                new("TenDangNhap", "Tên đăng nhập", "text", 50),
                new("TrangThai", "Đang làm việc", "bool"),
                new(
                    "MatKhau",
                    "Mật khẩu mới (để trống khi giữ nguyên)",
                    "password",
                    100,
                    KhongBatBuoc: true
                ),
            ]
        ),
        ["roles"] = new(
            "ChucVu",
            "Chức vụ",
            ["MaChucVu"],
            [
                new("MaChucVu", "Mã chức vụ", "code", 10),
                new("TenChucVu", "Tên chức vụ", "text", 100),
                new("MoTa", "Mô tả", "textarea", 255),
                new("HeSoLuong", "Hệ số lương", "factor"),
                new("TienDieuChinh", "Điều chỉnh từ các khoản", "int", ChiDoc: true),
            ]
        ),
        ["adjustments"] = new(
            "KhoanDieuChinh",
            "Khoản thưởng / phạt",
            ["MaKhoanDieuChinh"],
            [
                new("MaKhoanDieuChinh", "Mã khoản", "code", 10),
                new("TenKhoanDieuChinh", "Tên khoản"),
                new("SoTien", "Số tiền (+ thưởng, − phạt)", "signedMoney"),
            ]
        ),
        ["roleAdjustments"] = new(
            "ChucVuDieuChinh",
            "Khoản theo chức vụ",
            ["MaChucVu", "MaKhoanDieuChinh"],
            [
                new("MaChucVu", "Chức vụ", "select", 10, "roles"),
                new("MaKhoanDieuChinh", "Khoản điều chỉnh", "select", 10, "adjustments"),
            ]
        ),
        ["shifts"] = new(
            "CaLam",
            "Ca làm",
            ["MaCaLam"],
            [
                new("MaCaLam", "Mã ca", "code", 10),
                new("ThoiGianBatDau", "Giờ bắt đầu", "time"),
                new("ThoiGianKetThuc", "Giờ kết thúc", "time"),
                new("ThoiGianCaLam", "Số giờ (tự tính)", "factor", ChiDoc: true),
            ]
        ),
        ["attendance"] = new(
            "ChiTietCaLam",
            "Chấm công",
            ["MaNhanVien", "MaCaLam", "NgayLam"],
            [
                new("MaNhanVien", "Nhân viên", "select", 10, "employees"),
                new("MaCaLam", "Ca làm", "select", 10, "shifts"),
                new("NgayLam", "Ngày làm", "date"),
                new("TongThoiGianLamThucTe", "Số giờ thực tế", "hours"),
            ]
        ),
    };
}
