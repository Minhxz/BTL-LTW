using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;

// Người 5: tổng quan, báo cáo doanh thu và xuất CSV.
[ApiController, Authorize, Route("api/workshop")]
public sealed class BaoCaoController(XuLyDuLieu db) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> TongQuan()
    {
        var employee = PhanQuyenNguoiDung.MaNhanVien(User);
        var mechanic = User.IsInRole("Mechanic");
        var counts = await db.DocBangAsync("SELECT TrangThai status,COUNT(*) count FROM PhieuTiepNhan p WHERE @mine=0 OR EXISTS(SELECT 1 FROM ChiTietPhanCong c WHERE c.MaXe=p.MaXe AND c.MaNhanVien=@employee) GROUP BY TrangThai", new { mine = mechanic, employee });
        var summary = new Dictionary<string, object?>
        {
            {
                "jobs",
                counts
            },
            {
                "database",
                new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(db.ChuoiKetNoi).InitialCatalog
            }
        };
        if (User.IsInRole("Manager") || User.IsInRole("Cashier"))
        {
            summary["pendingBookings"] = await db.DocGiaTriAsync<int>("SELECT COUNT(*) FROM WebLichHen WHERE TrangThai=N'Chờ xác nhận'");
            summary["todayRevenue"] = await db.DocGiaTriAsync<decimal>("SELECT COALESCE(SUM(TienThanhToan),0) FROM HoaDon WHERE NgayThanhToan=@today", new { today = XuLyDuLieu.HomNay });
        }

        if (User.IsInRole("Manager"))
            summary["stockAlerts"] = await db.DocBangAsync("SELECT TOP(8) MaPhuTung id,TenPhuTung name,SoLuongTon stock,HanSuDung expiry FROM PhuTung WHERE TrangThai=1 AND (SoLuongTon<=5 OR HanSuDung<@soon) ORDER BY HanSuDung,SoLuongTon", new { soon = XuLyDuLieu.HomNay.AddDays(30) });
        return Ok(summary);
    }

    [HttpGet("reports"), Authorize(Roles = "Manager")]
    public async Task<IActionResult> BaoCao(string? from = null, string? to = null)
    {
        var dates = PhanTrang.KhoangNgay(from, to);
        var start = dates.From ?? new DateTime(XuLyDuLieu.HomNay.Year, XuLyDuLieu.HomNay.Month, 1);
        var end = dates.To ?? XuLyDuLieu.HomNay;
        if (start > end)
            throw new ArgumentException("Khoảng ngày không hợp lệ.");
        var args = new
        {
            from = start,
            to = end
        };
        var paid = await db.DocGiaTriAsync<decimal>("SELECT COALESCE(SUM(TienThanhToan),0) FROM HoaDon WHERE NgayThanhToan BETWEEN @from AND @to", args);
        var purchases = await db.DocGiaTriAsync<decimal>("SELECT COALESCE(SUM(TongTienNhap),0) FROM PhieuNhap WHERE NgayNhap BETWEEN @from AND @to", args);
        var unpaid = await db.DocGiaTriAsync<decimal>("SELECT COALESCE(SUM(c.ThanhTien),0) FROM PhieuTiepNhan p JOIN ChiTietPhieuTiepNhan c ON c.MaPhieuTiepNhan=p.MaPhieuTiepNhan WHERE p.TrangThai=N'Hoàn thành' AND NOT EXISTS(SELECT 1 FROM HoaDon h WHERE h.MaPhieuTiepNhan=p.MaPhieuTiepNhan)");
        var daily = await db.DocBangAsync("SELECT NgayThanhToan date,SUM(TienThanhToan) amount,COUNT(*) count FROM HoaDon WHERE NgayThanhToan BETWEEN @from AND @to GROUP BY NgayThanhToan ORDER BY NgayThanhToan", args);
        var services = await db.DocBangAsync("SELECT TOP(8) d.TenDichVu name,SUM(c.SoLuong) quantity,SUM(c.ThanhTien) amount FROM ChiTietPhieuTiepNhan c JOIN DichVu d ON d.MaDichVu=c.MaDichVu JOIN HoaDon h ON h.MaPhieuTiepNhan=c.MaPhieuTiepNhan WHERE h.NgayThanhToan BETWEEN @from AND @to GROUP BY d.TenDichVu ORDER BY SUM(c.ThanhTien) DESC", args);
        return Ok(new { from = start, to = end, paid, purchases, unpaid, cashDifference = paid - purchases, daily, services });
    }

    [HttpGet("reports/export"), Authorize(Roles = "Manager")]
    public async Task<IActionResult> XuatCsv(string? from = null, string? to = null)
    {
        var dates = PhanTrang.KhoangNgay(from, to);
        var rows = await db.DocBangAsync("SELECT MaHoaDon,MaPhieuTiepNhan,NgayThanhToan,TienThanhToan FROM HoaDon WHERE (@from IS NULL OR NgayThanhToan>=@from) AND (@to IS NULL OR NgayThanhToan<=@to) ORDER BY NgayThanhToan", new { from = dates.From ?? new DateTime(XuLyDuLieu.HomNay.Year, XuLyDuLieu.HomNay.Month, 1), to = dates.To ?? XuLyDuLieu.HomNay });
        string Cell(object? value)
        {
            var text = value is DateTime date ? date.ToString("yyyy-MM-dd") : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
            if (text.Length > 0 && "=+-@".Contains(text[0]))
                text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        var csv = "Mã hóa đơn,Mã tiếp nhận,Ngày thanh toán,Số tiền\r\n" + string.Join("\r\n", rows.Select(r => string.Join(',', r.Values.Select(Cell))));
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(), "text/csv; charset=utf-8", "doanh-thu.csv");
    }
}
