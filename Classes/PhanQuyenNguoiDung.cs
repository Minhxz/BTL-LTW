using System.Security.Claims;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.LopDungChung;
public static class PhanQuyenNguoiDung
{
    public static string XacDinhVaiTro(string title) => title.Contains("Quản lý", StringComparison.OrdinalIgnoreCase) || title.Contains("Chủ", StringComparison.OrdinalIgnoreCase) ? "Manager" : title.Contains("Thu ngân", StringComparison.OrdinalIgnoreCase) ? "Cashier" : title.Contains("Thợ", StringComparison.OrdinalIgnoreCase) ? "Mechanic" : "Staff";
    public static string MaNhanVien(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException("Vui lòng đăng nhập.");
}
