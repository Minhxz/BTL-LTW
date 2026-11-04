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

// Người 1: lọc và tính tổng giờ công của nhân viên đang đăng nhập.
[ApiController, Authorize, Route("api/workshop")]
public sealed class ChamCongController(XuLyDuLieu db) : ControllerBase
{
    [HttpGet("my-attendance")]
    public async Task<IActionResult> CongCaNhan(string? from = null, string? to = null)
    {
        var dates = PhanTrang.KhoangNgay(from, to);
        var items = await db.DocBangAsync("SELECT a.NgayLam date,a.MaCaLam shift,c.ThoiGianBatDau start,c.ThoiGianKetThuc [end],a.TongThoiGianLamThucTe hours FROM ChiTietCaLam a JOIN CaLam c ON c.MaCaLam=a.MaCaLam WHERE a.MaNhanVien=@employee AND (@from IS NULL OR a.NgayLam>=@from) AND (@to IS NULL OR a.NgayLam<=@to) ORDER BY a.NgayLam DESC", new { employee = PhanQuyenNguoiDung.MaNhanVien(User), from = dates.From, to = dates.To });
        return Ok(new { items, totalHours = items.Sum(r => r.SoTien("hours")) });
    }
}
