using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;

// Người 5: lập hóa đơn và lọc danh sách hóa đơn.
[ApiController, Authorize(Roles = "Manager,Cashier"), Route("api/workshop")]
public sealed class HoaDonController(XuLyDuLieu db) : ControllerBase
{
    [HttpPost("jobs/{id}/invoice"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> LapHoaDon(string id, YeuCauThanhToan input)
    {
        var invoice = await db.TrongGiaoDichAsync(async s =>
        {
            var job = (await s.DocBangAsync("SELECT NgayLap,TrangThai FROM PhieuTiepNhan WITH(UPDLOCK,HOLDLOCK) WHERE MaPhieuTiepNhan=@id", new { id })).SingleOrDefault() ?? throw new KeyNotFoundException("Không tìm thấy phiếu.");
            if (job.Chuoi("TrangThai") != "Hoàn thành")
                throw new InvalidOperationException("Chỉ thu tiền khi xe đã hoàn thành.");
            if (input.NgayThanhToan.Date > XuLyDuLieu.HomNay || input.NgayThanhToan.Date < (DateTime)job["NgayLap"]!)
                throw new ArgumentException("Ngày thanh toán không được trước ngày tiếp nhận hoặc sau hôm nay.");
            if (await s.DocGiaTriAsync<int>("SELECT COUNT(*) FROM HoaDon WITH(UPDLOCK,HOLDLOCK) WHERE MaPhieuTiepNhan=@id", new { id }) > 0)
                throw new InvalidOperationException("Phiếu đã có hóa đơn; không thu tiền hai lần.");
            var amount = await s.DocGiaTriAsync<decimal>("SELECT SUM(ThanhTien) FROM ChiTietPhieuTiepNhan WHERE MaPhieuTiepNhan=@id", new { id });
            if (amount <= 0)
                throw new InvalidOperationException("Phiếu chưa có chi tiết tính tiền hợp lệ.");
            var code = XuLyDuLieu.TaoMaMoi("HD");
            await s.ThucThiAsync("INSERT HoaDon(MaHoaDon,MaPhieuTiepNhan,TienThanhToan,NgayLap,NgayThanhToan) VALUES(@code,@id,@amount,@date,@date)", new { code, id, amount, date = input.NgayThanhToan.Date });
            return code;
        });
        return StatusCode(201, new { id = invoice, message = "Đã lập hóa đơn và ghi nhận doanh thu." });
    }

    [HttpGet("invoices"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> DanhSachHoaDon(string? q = null, string? from = null, string? to = null, int page = 1, int pageSize = 12)
    {
        PhanTrang.KiemTra(page, pageSize, q);
        var dates = PhanTrang.KhoangNgay(from, to);
        var args = new
        {
            q = "%" + q + "%",
            from = dates.From,
            to = dates.To,
            skip = (page - 1) * pageSize,
            size = pageSize
        };
        const string tables = " FROM HoaDon h JOIN PhieuTiepNhan p ON p.MaPhieuTiepNhan=h.MaPhieuTiepNhan JOIN Xe x ON x.MaXe=p.MaXe JOIN KhachHang k ON k.MaKhachHang=x.MaKhachHang ";
        const string where = " WHERE (h.MaHoaDon LIKE @q OR k.TenKhachHang LIKE @q OR x.BienXe LIKE @q) AND (@from IS NULL OR h.NgayThanhToan>=@from) AND (@to IS NULL OR h.NgayThanhToan<=@to) ";
        var total = await db.DocGiaTriAsync<int>("SELECT COUNT(*)" + tables + where, args);
        var items = await db.DocBangAsync("SELECT h.MaHoaDon id,h.MaPhieuTiepNhan jobId,k.TenKhachHang customerName,x.BienXe licensePlate,h.TienThanhToan amount,h.NgayLap createdOn,h.NgayThanhToan paidOn" + tables + where + "ORDER BY h.NgayThanhToan DESC,h.MaHoaDon DESC OFFSET @skip ROWS FETCH NEXT @size ROWS ONLY", args);
        var totalAmount = await db.DocGiaTriAsync<decimal>("SELECT COALESCE(SUM(h.TienThanhToan),0)" + tables + where, args);
        return Ok(new { items, total, page, totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)), totalAmount });
    }
}
