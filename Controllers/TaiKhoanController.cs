using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;

public class TaiKhoanController : Controller
{
    [AllowAnonymous]
    [ActionName("Login")]
    public IActionResult DangNhap([FromQuery(Name = "ReturnUrl")] string? duongDanQuayLai = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Workflow", "QuanLy");
        ViewData["ReturnUrl"] = Url.IsLocalUrl(duongDanQuayLai)
            ? duongDanQuayLai
            : "/Management/Workflow";
        return View("DangNhap");
    }

    [ActionName("Denied")]
    public IActionResult TuChoiTruyCap()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View("TuChoiTruyCap");
    }
}
