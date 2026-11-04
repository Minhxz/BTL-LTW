using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;
[Authorize]
public sealed class QuanLyController : Controller
{
    [ActionName("Index")]
    public IActionResult TrangDau() => RedirectToAction("Workflow");
    [ActionName("Workflow")]
    public IActionResult BangDieuKhien() => View("~/Views/QuanLy/BangDieuKhien.cshtml");
    [ActionName("Services")]
    public IActionResult DichVu() => Redirect("/Management/Workflow#services");
    [ActionName("Inventory")]
    public IActionResult Kho() => Redirect("/Management/Workflow#parts");
    [ActionName("Analytics")]
    public IActionResult ThongKe() => Redirect("/Management/Workflow#reports");
    [ActionName("JobCard")]
    public IActionResult PhieuSua() => RedirectToAction("Workflow");
    [ActionName("Flow")]
    public IActionResult LuongNghiepVu() => View("~/Views/QuanLy/LuongNghiepVu.cshtml");
}
