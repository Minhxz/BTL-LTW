using System.Security.Claims;

namespace QLyCuaHangSuaXe.LopDungChung;

// Người 1: ánh xạ chức vụ trong SQL sang nhóm quyền của web.
public static class PhanQuyenNguoiDung
{
    // Tên chức vụ → vai trò: Manager (quản lý/chủ), Cashier (thu ngân), Mechanic (thợ), Staff (còn lại).
    public static string XacDinhVaiTro(string tenChucVu)
    {
        if (tenChucVu.Contains("Quản lý", StringComparison.OrdinalIgnoreCase) || tenChucVu.Contains("Chủ", StringComparison.OrdinalIgnoreCase))
        {
            return "Manager";
        }

        if (tenChucVu.Contains("Thu ngân", StringComparison.OrdinalIgnoreCase))
        {
            return "Cashier";
        }

        if (tenChucVu.Contains("Thợ", StringComparison.OrdinalIgnoreCase))
        {
            return "Mechanic";
        }

        return "Staff";
    }

    // Mã nhân viên của tài khoản đang đăng nhập (lưu trong cookie lúc đăng nhập).
    public static string MaNhanVien(ClaimsPrincipal nguoiDung)
    {
        return nguoiDung.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Vui lòng đăng nhập.");
    }
}
