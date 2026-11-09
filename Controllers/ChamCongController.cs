using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;

namespace QLyCuaHangSuaXe.Controllers;

[ApiController, Authorize, Route("api/workshop")]
public sealed class ChamCongController(XuLyDuLieu duLieu) : ControllerBase
{
    [HttpGet("my-attendance")]
    public async Task<IActionResult> CongCaNhan(
        [FromQuery(Name = "from")] string? ngayBatDau = null,
        [FromQuery(Name = "to")] string? ngayKetThuc = null
    )
    {
        (DateTime? tuNgay, DateTime? denNgay) = PhanTrang.KhoangNgay(ngayBatDau, ngayKetThuc);
        string sqlCong =
            "Select a.NgayLam As date, a.MaCaLam As shift, c.ThoiGianBatDau As start, c.ThoiGianKetThuc As [end], a.TongThoiGianLamThucTe As hours "
            + "From ChiTietCaLam a Join CaLam c On c.MaCaLam = a.MaCaLam "
            + "Where a.MaNhanVien = @employee "
            + "And (@from Is Null Or a.NgayLam >= @from) "
            + "And (@to Is Null Or a.NgayLam <= @to) "
            + "Order By a.NgayLam Desc";
        List<Dictionary<string, object?>> danhSachCong = await duLieu.DocBangAsync(
            sqlCong,
            new
            {
                employee = PhanQuyenNguoiDung.MaNhanVien(User),
                from = tuNgay,
                to = denNgay,
            }
        );
        decimal tongGio = danhSachCong.Sum(dong => dong.SoTien("hours"));
        return Ok(new { items = danhSachCong, totalHours = tongGio });
    }
}
