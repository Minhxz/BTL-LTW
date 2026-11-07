using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QLyCuaHangSuaXe.LopDungChung;

// Người 1 (cookie, đăng nhập) và Người 5 (ghép bài): cấu hình chung của web.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ===== 1. MVC, chống giả mạo yêu cầu (CSRF) và thông báo lỗi nhập liệu =====
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");
builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = TaoLoiNhapLieu);

// ===== 2. Dịch vụ dùng chung =====
builder.Services.AddScoped<XuLyDuLieu>();
builder.Services.AddScoped<NghiepVuSuaXe>();

// ===== 3. Session (lưu bản nháp đặt lịch) =====
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.Cookie.Name = "QuanLyCuaHangSuaXe.Phien";
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.IdleTimeout = TimeSpan.FromMinutes(30);
});

// Khóa mã hóa cookie lưu trong App_Data để khởi động lại web không bị đăng xuất
string thuMucKhoa = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
IDataProtectionBuilder baoVeDuLieu = builder.Services.AddDataProtection()
    .SetApplicationName("QuanLyCuaHangSuaXe")
    .PersistKeysToFileSystem(new DirectoryInfo(thuMucKhoa));

if (OperatingSystem.IsWindows())
{
    baoVeDuLieu.ProtectKeysWithDpapi();
}

// ===== 4. Đăng nhập bằng cookie =====
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "QuanLyCuaHangSuaXe.DangNhap";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/Denied";
    o.ExpireTimeSpan = TimeSpan.FromHours(4);

    // Trang thường thì chuyển tới trang đăng nhập; API thì trả JSON 401/403 cho JavaScript xử lý
    o.Events.OnRedirectToLogin = c => ChuyenHuongHoacTraLoi(c.HttpContext, c.RedirectUri, 401, "Vui lòng đăng nhập.");
    o.Events.OnRedirectToAccessDenied = c => ChuyenHuongHoacTraLoi(c.HttpContext, c.RedirectUri, 403, "Tài khoản không có quyền thực hiện thao tác này.");

    // Mỗi yêu cầu kiểm tra lại tài khoản: bị khóa, đổi tên đăng nhập hoặc đổi nhóm quyền thì đăng xuất ngay
    o.Events.OnValidatePrincipal = async c =>
    {
        XuLyDuLieu db = c.HttpContext.RequestServices.GetRequiredService<XuLyDuLieu>();

        string sqlTaiKhoan = "Select n.TenDangNhap, c.TenChucVu " +
            "From NhanVien n Join ChucVu c On n.MaChucVu = c.MaChucVu " +
            "Where n.MaNhanVien = @id And n.TrangThai = 1";

        List<Dictionary<string, object?>> taiKhoan = await db.DocBangAsync(sqlTaiKhoan, new
        {
            id = c.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        });

        if (taiKhoan.Count != 1)
        {
            c.RejectPrincipal();
            return;
        }

        string vaiTroHienTai = PhanQuyenNguoiDung.XacDinhVaiTro(taiKhoan[0].Chuoi("TenChucVu"));
        bool doiVaiTro = vaiTroHienTai != c.Principal?.FindFirst(ClaimTypes.Role)?.Value;
        bool doiTenDangNhap = taiKhoan[0].Chuoi("TenDangNhap") != c.Principal?.Identity?.Name;

        if (doiVaiTro || doiTenDangNhap)
        {
            c.RejectPrincipal();
        }
    };
});
builder.Services.AddAuthorization();

// ===== 5. Giới hạn số yêu cầu: đăng nhập 15 lần/5 phút, API công khai 60 lần/phút (theo IP) =====
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.OnRejected = async (c, huy) => await c.HttpContext.Response.WriteAsJsonAsync(
        new { message = "Bạn gửi quá nhiều yêu cầu. Vui lòng thử lại sau ít phút." }, huy);

    o.AddPolicy("login", c => GioiHanTheoIp(c, 15, TimeSpan.FromMinutes(5)));
    o.AddPolicy("public-api", c => GioiHanTheoIp(c, 60, TimeSpan.FromMinutes(1)));
});

// ===== 6. Ngôn ngữ mặc định tiếng Việt =====
builder.Services.Configure<RequestLocalizationOptions>(o =>
{
    CultureInfo[] ngonNgu = [new CultureInfo("vi-VN"), new CultureInfo("en-US")];

    o.DefaultRequestCulture = new("vi-VN");
    o.SupportedCultures = ngonNgu;
    o.SupportedUICultures = ngonNgu;
});

WebApplication app = builder.Build();

// Kiểm tra CSDL của nhóm và tạo bảng mở rộng của web trước khi nhận yêu cầu
using (IServiceScope scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<XuLyDuLieu>().KhoiTaoAsync(app.Environment.ContentRootPath);
}

// ===== 7. Đổi lỗi của API thành mã HTTP và thông báo tiếng Việt =====
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception loi) when (context.Request.Path.StartsWithSegments("/api") && !context.Response.HasStarted)
    {
        (int maTrangThai, string thongBao) = PhanLoaiLoi(loi);

        if (maTrangThai == 500)
        {
            app.Logger.LogError(loi, "Xử lý yêu cầu quản lý sửa xe thất bại");
        }

        context.Response.StatusCode = maTrangThai;
        await context.Response.WriteAsJsonAsync(new { message = thongBao });
    }
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

// ===== 8. Thứ tự middleware và đường dẫn =====
app.UseStaticFiles();
app.UseRequestLocalization();
app.UseRouting();
app.UseRateLimiter();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute("trang-cu", "Home/{action=Index}/{id?}", new { controller = "TrangChu" });
app.MapControllerRoute("tai-khoan-cu", "Account/{action=Login}/{id?}", new { controller = "TaiKhoan" });
app.MapControllerRoute("quan-ly-cu", "Management/{action=Index}/{id?}", new { controller = "QuanLy" });
app.MapControllerRoute("default", "{controller=TrangChu}/{action=Index}/{id?}");

app.Run();

// ===== Hàm phụ =====

// Lỗi nhập liệu (annotation trên model) → 400 kèm danh sách lỗi từng trường.
static IActionResult TaoLoiNhapLieu(ActionContext context)
{
    Dictionary<string, IEnumerable<string>> loiTheoTruong = context.ModelState
        .Where(x => x.Value?.Errors.Count > 0)
        .ToDictionary(
            x => x.Key,
            x => x.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Thông tin không hợp lệ." : e.ErrorMessage));

    return new BadRequestObjectResult(new { message = "Vui lòng kiểm tra thông tin đã nhập.", errors = loiTheoTruong });
}

static Task ChuyenHuongHoacTraLoi(HttpContext context, string duongDan, int maTrangThai, string thongBao)
{
    if (!context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Redirect(duongDan);
        return Task.CompletedTask;
    }

    context.Response.StatusCode = maTrangThai;
    return context.Response.WriteAsJsonAsync(new { message = thongBao });
}

static RateLimitPartition<string> GioiHanTheoIp(HttpContext context, int soLanToiDa, TimeSpan khoangThoiGian)
{
    string diaChiIp = context.Connection.RemoteIpAddress?.ToString() ?? "local";

    return RateLimitPartition.GetFixedWindowLimiter(diaChiIp, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = soLanToiDa,
        Window = khoangThoiGian,
        QueueLimit = 0
    });
}

// Lỗi nghiệp vụ → hiện nguyên thông báo; lỗi SQL quen thuộc → thông báo dễ hiểu; còn lại → 500.
static (int MaTrangThai, string ThongBao) PhanLoaiLoi(Exception loi)
{
    return loi switch
    {
        ArgumentException => (400, loi.Message),
        KeyNotFoundException => (404, loi.Message),
        UnauthorizedAccessException => (403, loi.Message),
        InvalidOperationException => (409, loi.Message),
        SqlException s when s.Number is 2601 or 2627 => (409, "Mã, tên đăng nhập hoặc số điện thoại đã tồn tại."),
        SqlException s when s.Number == 547 => (409, "Bản ghi đang được sử dụng hoặc vi phạm ràng buộc dữ liệu. Hãy kiểm tra thông tin liên quan."),
        SqlException s when s.Number == 1205 => (409, "Dữ liệu vừa thay đổi. Vui lòng thử lại."),
        _ => (500, "Không thể xử lý yêu cầu. Vui lòng thử lại hoặc kiểm tra kết nối SQL Server.")
    };
}
