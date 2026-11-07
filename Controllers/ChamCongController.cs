using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;

namespace QLyCuaHangSuaXe.Controllers;

// Người 1: lọc và tính tổng giờ công của nhân viên đang đăng nhập.
[ApiController, Authorize, Route("api/workshop")]
public sealed class ChamCongController(XuLyDuLieu db) : ControllerBase
{
    [HttpGet("my-attendance")]
    public async Task<IActionResult> CongCaNhan(string? from = null, string? to = null)
    {
        (DateTime? tuNgay, DateTime? denNgay) = PhanTrang.KhoangNgay(from, to);

        // Chỉ lấy công của chính người đang đăng nhập; bỏ trống ngày thì không lọc theo ngày đó
        string sqlCong = "Select a.NgayLam As date, a.MaCaLam As shift, c.ThoiGianBatDau As start, c.ThoiGianKetThuc As [end], a.TongThoiGianLamThucTe As hours " +
            "From ChiTietCaLam a Join CaLam c On c.MaCaLam = a.MaCaLam " +
            "Where a.MaNhanVien = @employee " +
            "And (@from Is Null Or a.NgayLam >= @from) " +
            "And (@to Is Null Or a.NgayLam <= @to) " +
            "Order By a.NgayLam Desc";

        List<Dictionary<string, object?>> danhSachCong = await db.DocBangAsync(sqlCong, new
        {
            employee = PhanQuyenNguoiDung.MaNhanVien(User),
            from = tuNgay,
            to = denNgay
        });

        decimal tongGio = danhSachCong.Sum(dong => dong.SoTien("hours"));

        return Ok(new { items = danhSachCong, totalHours = tongGio });
    }
}
