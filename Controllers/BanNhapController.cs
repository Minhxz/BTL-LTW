using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;
// Bản nháp thuộc phiên trình duyệt hiện tại và hết hạn sau 30 phút không hoạt động.
[ApiController, Route("api/appointments/draft")]
public sealed class BanNhapController : ControllerBase
{
    private const string SessionKey = "BanNhapDatLich";
    [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult DocBanNhap()
    {
        var json = HttpContext.Session.GetString(SessionKey);
        return Ok(new { draft = json is null ? null : JsonSerializer.Deserialize<BanNhapDatLich>(json) });
    }

    [HttpPut]
    public IActionResult LuuBanNhap(BanNhapDatLich draft)
    {
        HttpContext.Session.SetString(SessionKey, JsonSerializer.Serialize(draft));
        return Ok(new { message = "Đã lưu bản nháp trong phiên làm việc này." });
    }

    [HttpDelete]
    public IActionResult XoaBanNhap()
    {
        HttpContext.Session.Remove(SessionKey);
        return NoContent();
    }
}
