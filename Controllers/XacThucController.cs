using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;

// Người 1: đăng nhập, cookie, đổi mật khẩu và mã chống giả mạo.
[ApiController, Route("api/auth")]
public sealed class XacThucController(XuLyDuLieu db) : ControllerBase
{
    [HttpGet("csrf")]
    public IActionResult LayMaChongGiaMao([FromServices] IAntiforgery antiforgery) => Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
    [HttpPost("login"), EnableRateLimiting("login")]
    public async Task<IActionResult> DangNhap(ThongTinDangNhap input)
    {
        var rows = await db.DocBangAsync("SELECT n.MaNhanVien,n.TenDangNhap,n.HoTen,n.MatKhau,c.TenChucVu FROM NhanVien n JOIN ChucVu c ON n.MaChucVu=c.MaChucVu WHERE n.TenDangNhap=@name AND n.TrangThai=1", new { name = input.TenDangNhap.Trim() });
        var row = rows.FirstOrDefault();
        var hash = row?.Chuoi("MatKhau") ?? "";
        var expected = XuLyDuLieu.BamMatKhau(input.MatKhau);
        if (hash.Length != 64 || !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(hash.ToUpperInvariant()), System.Text.Encoding.ASCII.GetBytes(expected)))
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu chưa đúng, hoặc tài khoản đã ngừng hoạt động." });
        if (row is null)
            return Unauthorized();
        var role = PhanQuyenNguoiDung.XacDinhVaiTro(row.Chuoi("TenChucVu"));
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, row.Chuoi("MaNhanVien")), new Claim(ClaimTypes.Name, row.Chuoi("TenDangNhap")), new Claim(ClaimTypes.Role, role), new Claim("displayName", row.Chuoi("HoTen")) }, CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.Session.Clear();
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return Ok(new { authenticated = true, role, redirectUrl = "/Management/Workflow" });
    }

    [HttpGet("me")]
    public IActionResult ThongTinPhien() => Ok(new { authenticated = User.Identity?.IsAuthenticated == true, username = User.Identity?.Name, role = User.FindFirstValue(ClaimTypes.Role), employeeId = User.FindFirstValue(ClaimTypes.NameIdentifier) });
    [HttpPost("logout")]
    public async Task<IActionResult> DangXuat()
    {
        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync();
        return Ok(new { authenticated = false });
    }

    [HttpPut("password"), Authorize]
    public async Task<IActionResult> DoiMatKhau(YeuCauDoiMatKhau input)
    {
        if (input.MatKhauCu == input.MatKhauMoi)
            return BadRequest(new { message = "Mật khẩu mới phải khác mật khẩu cũ." });
        var changed = await db.ThucThiAsync("UPDATE NhanVien SET MatKhau=@next WHERE MaNhanVien=@id AND MatKhau=@old AND TrangThai=1", new { id = PhanQuyenNguoiDung.MaNhanVien(User), old = XuLyDuLieu.BamMatKhau(input.MatKhauCu), next = XuLyDuLieu.BamMatKhau(input.MatKhauMoi) });
        return changed == 1 ? Ok(new { message = "Đã đổi mật khẩu." }) : BadRequest(new { message = "Mật khẩu hiện tại chưa đúng." });
    }
}
