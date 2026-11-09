using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;

[ApiController, Route("api/appointments/draft")]
public sealed class BanNhapController : ControllerBase
{
    private const string KhoaPhienBanNhap = "BanNhapDatLich";

    [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult DocBanNhap()
    {
        var chuoiJson = HttpContext.Session.GetString(KhoaPhienBanNhap);
        return Ok(
            new
            {
                draft = chuoiJson is null
                    ? null
                    : JsonSerializer.Deserialize<BanNhapDatLich>(chuoiJson),
            }
        );
    }

    [HttpPut]
    public IActionResult LuuBanNhap(BanNhapDatLich banNhap)
    {
        HttpContext.Session.SetString(KhoaPhienBanNhap, JsonSerializer.Serialize(banNhap));
        return Ok(new { message = "Đã lưu bản nháp trong phiên làm việc này." });
    }

    [HttpDelete]
    public IActionResult XoaBanNhap()
    {
        HttpContext.Session.Remove(KhoaPhienBanNhap);
        return NoContent();
    }
}
