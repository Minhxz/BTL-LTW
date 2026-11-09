using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers
{
    public class TrangChuController : Controller
    {
        [ActionName("Index")]
        public IActionResult TrangChu()
        {
            return View("TrangChu");
        }

        [ActionName("Privacy")]
        public IActionResult QuyenRiengTu()
        {
            return View("QuyenRiengTu");
        }

        [ActionName("Services")]
        public IActionResult DichVu() => Redirect("/#dich-vu");

        [ActionName("Vehicles")]
        public IActionResult PhuongTien() => Redirect("/#quy-trinh");

        [ActionName("Process")]
        public IActionResult QuyTrinh() => Redirect("/#quy-trinh");

        [ActionName("Booking")]
        public IActionResult DatLich() => View("DatLich");

        [ActionName("Tracking")]
        public IActionResult TraCuu() => View("TraCuu");

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [ActionName("Error")]
        public IActionResult Loi()
        {
            return View(
                "Loi",
                new ThongTinLoi { MaYeuCau = Activity.Current?.Id ?? HttpContext.TraceIdentifier }
            );
        }
    }
}
