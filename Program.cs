using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QLyCuaHangSuaXe.LopDungChung;

WebApplicationBuilder boCauHinh = WebApplication.CreateBuilder(args);
boCauHinh.Services.AddControllersWithViews(tuyChon =>
    tuyChon.Filters.Add(new AutoValidateAntiforgeryTokenAttribute())
);
boCauHinh.Services.AddAntiforgery(tuyChon => tuyChon.HeaderName = "X-CSRF-TOKEN");
boCauHinh.Services.Configure<ApiBehaviorOptions>(tuyChon =>
    tuyChon.InvalidModelStateResponseFactory = TaoLoiNhapLieu
);
boCauHinh.Services.AddScoped<XuLyDuLieu>();
boCauHinh.Services.AddScoped<NghiepVuSuaXe>();
boCauHinh.Services.AddDistributedMemoryCache();
boCauHinh.Services.AddSession(tuyChon =>
{
    tuyChon.Cookie.Name = "QuanLyCuaHangSuaXe.Phien";
    tuyChon.Cookie.HttpOnly = true;
    tuyChon.Cookie.IsEssential = true;
    tuyChon.Cookie.SameSite = SameSiteMode.Strict;
    tuyChon.IdleTimeout = TimeSpan.FromMinutes(30);
});
string thuMucKhoa = Path.Combine(boCauHinh.Environment.ContentRootPath, "App_Data", "keys");
IDataProtectionBuilder baoVeDuLieu = boCauHinh
    .Services.AddDataProtection()
    .SetApplicationName("QuanLyCuaHangSuaXe")
    .PersistKeysToFileSystem(new DirectoryInfo(thuMucKhoa));
if (OperatingSystem.IsWindows())
{
    baoVeDuLieu.ProtectKeysWithDpapi();
}

boCauHinh
    .Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(tuyChon =>
    {
        tuyChon.Cookie.Name = "QuanLyCuaHangSuaXe.DangNhap";
        tuyChon.Cookie.HttpOnly = true;
        tuyChon.Cookie.SameSite = SameSiteMode.Strict;
        tuyChon.LoginPath = "/Account/Login";
        tuyChon.AccessDeniedPath = "/Account/Denied";
        tuyChon.ExpireTimeSpan = TimeSpan.FromHours(4);
        tuyChon.Events.OnRedirectToLogin = doiTuong =>
            ChuyenHuongHoacTraLoi(
                doiTuong.HttpContext,
                doiTuong.RedirectUri,
                401,
                "Vui lòng đăng nhập."
            );
        tuyChon.Events.OnRedirectToAccessDenied = doiTuong =>
            ChuyenHuongHoacTraLoi(
                doiTuong.HttpContext,
                doiTuong.RedirectUri,
                403,
                "Tài khoản không có quyền thực hiện thao tác này."
            );
        tuyChon.Events.OnValidatePrincipal = async doiTuong =>
        {
            XuLyDuLieu duLieu =
                doiTuong.HttpContext.RequestServices.GetRequiredService<XuLyDuLieu>();
            string sqlTaiKhoan =
                "Select n.TenDangNhap, c.TenChucVu "
                + "From NhanVien n Join ChucVu c On n.MaChucVu = c.MaChucVu "
                + "Where n.MaNhanVien = @id And n.TrangThai = 1";
            List<Dictionary<string, object?>> taiKhoan = await duLieu.DocBangAsync(
                sqlTaiKhoan,
                new { id = doiTuong.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value }
            );
            if (taiKhoan.Count != 1)
            {
                doiTuong.RejectPrincipal();
                return;
            }

            string vaiTroHienTai = PhanQuyenNguoiDung.XacDinhVaiTro(taiKhoan[0].Chuoi("TenChucVu"));
            bool doiVaiTro = vaiTroHienTai != doiTuong.Principal?.FindFirst(ClaimTypes.Role)?.Value;
            bool doiTenDangNhap =
                taiKhoan[0].Chuoi("TenDangNhap") != doiTuong.Principal?.Identity?.Name;
            if (doiVaiTro || doiTenDangNhap)
            {
                doiTuong.RejectPrincipal();
            }
        };
    });
boCauHinh.Services.AddAuthorization();
boCauHinh.Services.AddRateLimiter(tuyChon =>
{
    tuyChon.RejectionStatusCode = 429;
    tuyChon.OnRejected = async (doiTuong, huy) =>
        await doiTuong.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Bạn gửi quá nhiều yêu cầu. Vui lòng thử lại sau ít phút." },
            huy
        );
    tuyChon.AddPolicy("login", doiTuong => GioiHanTheoIp(doiTuong, 15, TimeSpan.FromMinutes(5)));
    tuyChon.AddPolicy(
        "public-api",
        doiTuong => GioiHanTheoIp(doiTuong, 60, TimeSpan.FromMinutes(1))
    );
});
boCauHinh.Services.Configure<RequestLocalizationOptions>(tuyChon =>
{
    CultureInfo[] ngonNgu = [new CultureInfo("vi-VN"), new CultureInfo("en-US")];
    tuyChon.DefaultRequestCulture = new("vi-VN");
    tuyChon.SupportedCultures = ngonNgu;
    tuyChon.SupportedUICultures = ngonNgu;
});
WebApplication ungDung = boCauHinh.Build();
using (IServiceScope phamViDichVu = ungDung.Services.CreateScope())
{
    await phamViDichVu
        .ServiceProvider.GetRequiredService<XuLyDuLieu>()
        .KhoiTaoAsync(ungDung.Environment.ContentRootPath);
}

ungDung.Use(
    async (nguCanh, xuLyTiep) =>
    {
        try
        {
            await xuLyTiep();
        }
        catch (Exception loi)
            when (nguCanh.Request.Path.StartsWithSegments("/api") && !nguCanh.Response.HasStarted)
        {
            (int maTrangThai, string thongBao) = PhanLoaiLoi(loi);
            if (maTrangThai == 500)
            {
                ungDung.Logger.LogError(loi, "Xử lý yêu cầu quản lý sửa xe thất bại");
            }

            nguCanh.Response.StatusCode = maTrangThai;
            await nguCanh.Response.WriteAsJsonAsync(new { message = thongBao });
        }
    }
);
if (!ungDung.Environment.IsDevelopment())
{
    ungDung.UseExceptionHandler("/Home/Error");
    ungDung.UseHsts();
    ungDung.UseHttpsRedirection();
}

ungDung.UseStaticFiles();
ungDung.UseRequestLocalization();
ungDung.UseRouting();
ungDung.UseRateLimiter();
ungDung.UseSession();
ungDung.UseAuthentication();
ungDung.UseAuthorization();
ungDung.MapControllers();
ungDung.MapControllerRoute(
    "trang-cu",
    "Home/{action=Index}/{id?}",
    new { controller = "TrangChu" }
);
ungDung.MapControllerRoute(
    "tai-khoan-cu",
    "Account/{action=Login}/{id?}",
    new { controller = "TaiKhoan" }
);
ungDung.MapControllerRoute(
    "quan-ly-cu",
    "Management/{action=Index}/{id?}",
    new { controller = "QuanLy" }
);
ungDung.MapControllerRoute("default", "{controller=TrangChu}/{action=Index}/{id?}");
ungDung.Run();
static IActionResult TaoLoiNhapLieu(ActionContext nguCanh)
{
    Dictionary<string, IEnumerable<string>> loiTheoTruong = nguCanh
        .ModelState.Where(phanTu => phanTu.Value?.Errors.Count > 0)
        .ToDictionary(
            phanTu => phanTu.Key,
            phanTu =>
                phanTu.Value!.Errors.Select(loi =>
                    string.IsNullOrEmpty(loi.ErrorMessage)
                        ? "Thông tin không hợp lệ."
                        : loi.ErrorMessage
                )
        );
    return new BadRequestObjectResult(
        new { message = "Vui lòng kiểm tra thông tin đã nhập.", errors = loiTheoTruong }
    );
}

static Task ChuyenHuongHoacTraLoi(
    HttpContext nguCanh,
    string duongDan,
    int maTrangThai,
    string thongBao
)
{
    if (!nguCanh.Request.Path.StartsWithSegments("/api"))
    {
        nguCanh.Response.Redirect(duongDan);
        return Task.CompletedTask;
    }

    nguCanh.Response.StatusCode = maTrangThai;
    return nguCanh.Response.WriteAsJsonAsync(new { message = thongBao });
}

static RateLimitPartition<string> GioiHanTheoIp(
    HttpContext nguCanh,
    int soLanToiDa,
    TimeSpan khoangThoiGian
)
{
    string diaChiIp = nguCanh.Connection.RemoteIpAddress?.ToString() ?? "local";
    return RateLimitPartition.GetFixedWindowLimiter(
        diaChiIp,
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = soLanToiDa,
            Window = khoangThoiGian,
            QueueLimit = 0,
        }
    );
}

static (int MaTrangThai, string ThongBao) PhanLoaiLoi(Exception loi)
{
    return loi switch
    {
        ArgumentException => (400, loi.Message),
        KeyNotFoundException => (404, loi.Message),
        UnauthorizedAccessException => (403, loi.Message),
        InvalidOperationException => (409, loi.Message),
        SqlException phien when phien.Number is 2601 or 2627 => (
            409,
            "Mã, tên đăng nhập hoặc số điện thoại đã tồn tại."
        ),
        SqlException phien when phien.Number == 547 => (
            409,
            "Bản ghi đang được sử dụng hoặc vi phạm ràng buộc dữ liệu. Hãy kiểm tra thông tin liên quan."
        ),
        SqlException phien when phien.Number == 1205 => (
            409,
            "Dữ liệu vừa thay đổi. Vui lòng thử lại."
        ),
        _ => (500, "Không thể xử lý yêu cầu. Vui lòng thử lại hoặc kiểm tra kết nối SQL Server."),
    };
}
