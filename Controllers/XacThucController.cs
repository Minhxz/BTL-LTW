using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
    // Mã chống giả mạo (CSRF): trình duyệt gửi kèm header X-CSRF-TOKEN ở mọi yêu cầu thay đổi dữ liệu.
    [HttpGet("csrf")]
    public IActionResult LayMaChongGiaMao([FromServices] IAntiforgery antiforgery)
    {
        string? maChongGiaMao = antiforgery.GetAndStoreTokens(HttpContext).RequestToken;

        return Ok(new { token = maChongGiaMao });
    }

    [HttpPost("login"), EnableRateLimiting("login")]
    public async Task<IActionResult> DangNhap(ThongTinDangNhap input)
    {
        // 1. Tìm tài khoản đang hoạt động theo tên đăng nhập
        string sqlTaiKhoan = "Select n.MaNhanVien, n.TenDangNhap, n.HoTen, n.MatKhau, c.TenChucVu " +
            "From NhanVien n Join ChucVu c On n.MaChucVu = c.MaChucVu " +
            "Where n.TenDangNhap = @name And n.TrangThai = 1";

        List<Dictionary<string, object?>> ketQua = await db.DocBangAsync(sqlTaiKhoan, new { name = input.TenDangNhap.Trim() });
        Dictionary<string, object?>? taiKhoan = ketQua.FirstOrDefault();

        // 2. So mật khẩu đã băm. So sánh thời gian cố định để không đoán được mật khẩu qua thời gian phản hồi
        string matKhauDaLuu = taiKhoan?.Chuoi("MatKhau") ?? "";
        string matKhauNhap = XuLyDuLieu.BamMatKhau(input.MatKhau);

        bool dungMatKhau = matKhauDaLuu.Length == 64 && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(matKhauDaLuu.ToUpperInvariant()),
            Encoding.ASCII.GetBytes(matKhauNhap));

        if (!dungMatKhau || taiKhoan == null)
        {
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu chưa đúng, hoặc tài khoản đã ngừng hoạt động." });
        }

        // 3. Ghi thông tin người dùng vào cookie đăng nhập
        string vaiTro = PhanQuyenNguoiDung.XacDinhVaiTro(taiKhoan.Chuoi("TenChucVu"));

        Claim[] thongTinNguoiDung =
        [
            new Claim(ClaimTypes.NameIdentifier, taiKhoan.Chuoi("MaNhanVien")),
            new Claim(ClaimTypes.Name, taiKhoan.Chuoi("TenDangNhap")),
            new Claim(ClaimTypes.Role, vaiTro),
            new Claim("displayName", taiKhoan.Chuoi("HoTen"))
        ];

        ClaimsIdentity danhTinh = new ClaimsIdentity(thongTinNguoiDung, CookieAuthenticationDefaults.AuthenticationScheme);

        HttpContext.Session.Clear();
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(danhTinh));

        return Ok(new { authenticated = true, role = vaiTro, redirectUrl = "/Management/Workflow" });
    }

    [HttpGet("me")]
    public IActionResult ThongTinPhien()
    {
        return Ok(new
        {
            authenticated = User.Identity?.IsAuthenticated == true,
            username = User.Identity?.Name,
            role = User.FindFirstValue(ClaimTypes.Role),
            employeeId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        });
    }

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
        {
            return BadRequest(new { message = "Mật khẩu mới phải khác mật khẩu cũ." });
        }

        // Chỉ đổi khi mật khẩu cũ đúng và tài khoản còn hoạt động
        string sqlDoiMatKhau = "Update NhanVien " +
            "Set MatKhau = @next " +
            "Where MaNhanVien = @id And MatKhau = @old And TrangThai = 1";

        int soDongCapNhat = await db.ThucThiAsync(sqlDoiMatKhau, new
        {
            id = PhanQuyenNguoiDung.MaNhanVien(User),
            old = XuLyDuLieu.BamMatKhau(input.MatKhauCu),
            next = XuLyDuLieu.BamMatKhau(input.MatKhauMoi)
        });

        if (soDongCapNhat != 1)
        {
            return BadRequest(new { message = "Mật khẩu hiện tại chưa đúng." });
        }

        return Ok(new { message = "Đã đổi mật khẩu." });
    }
}
