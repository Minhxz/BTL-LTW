using System.Security.Claims;

namespace QLyCuaHangSuaXe.LopDungChung;

public static class PhanQuyenNguoiDung
{
    public static string XacDinhVaiTro(string tenChucVu)
    {
        if (
            tenChucVu.Contains("Quản lý", StringComparison.OrdinalIgnoreCase)
            || tenChucVu.Contains("Chủ", StringComparison.OrdinalIgnoreCase)
        )
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

    public static string MaNhanVien(ClaimsPrincipal nguoiDung)
    {
        return nguoiDung.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Vui lòng đăng nhập.");
    }
}
