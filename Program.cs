using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Threading.RateLimiting;
using QLyCuaHangSuaXe.LopDungChung;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");
builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = c => new BadRequestObjectResult(new { message = "Vui lòng kiểm tra thông tin đã nhập.", errors = c.ModelState.Where(x => x.Value?.Errors.Count > 0).ToDictionary(x => x.Key, x => x.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Thông tin không hợp lệ." : e.ErrorMessage)) }));
builder.Services.AddScoped<XuLyDuLieu>();
builder.Services.AddScoped<NghiepVuSuaXe>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.Cookie.Name = "QuanLyCuaHangSuaXe.Phien";
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.IdleTimeout = TimeSpan.FromMinutes(30);
});
var protection = builder.Services.AddDataProtection().SetApplicationName("QuanLyCuaHangSuaXe").PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));
if (OperatingSystem.IsWindows())
    protection.ProtectKeysWithDpapi();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "QuanLyCuaHangSuaXe.DangNhap";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/Denied";
    o.ExpireTimeSpan = TimeSpan.FromHours(4);
    o.Events.OnRedirectToLogin = c =>
    {
        if (!c.Request.Path.StartsWithSegments("/api"))
        {
            c.Response.Redirect(c.RedirectUri);
            return Task.CompletedTask;
        }

        c.Response.StatusCode = 401;
        return c.Response.WriteAsJsonAsync(new { message = "Vui lòng đăng nhập." });
    };
    o.Events.OnRedirectToAccessDenied = c =>
    {
        if (!c.Request.Path.StartsWithSegments("/api"))
        {
            c.Response.Redirect(c.RedirectUri);
            return Task.CompletedTask;
        }

        c.Response.StatusCode = 403;
        return c.Response.WriteAsJsonAsync(new { message = "Tài khoản không có quyền thực hiện thao tác này." });
    };
    o.Events.OnValidatePrincipal = async c =>
    {
        var db = c.HttpContext.RequestServices.GetRequiredService<XuLyDuLieu>();
        var rows = await db.DocBangAsync("SELECT n.TenDangNhap,c.TenChucVu FROM NhanVien n JOIN ChucVu c ON n.MaChucVu=c.MaChucVu WHERE n.MaNhanVien=@id AND n.TrangThai=1", new { id = c.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value });
        if (rows.Count != 1 || PhanQuyenNguoiDung.XacDinhVaiTro(rows[0].Chuoi("TenChucVu")) != c.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value || rows[0].Chuoi("TenDangNhap") != c.Principal?.Identity?.Name)
            c.RejectPrincipal();
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.OnRejected = async (c, t) => await c.HttpContext.Response.WriteAsJsonAsync(new { message = "Bạn gửi quá nhiều yêu cầu. Vui lòng thử lại sau ít phút." }, t);
    o.AddPolicy("login", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
    o.AddPolicy("public-api", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.Configure<RequestLocalizationOptions>(o =>
{
    var cultures = new[]
    {
        new CultureInfo("vi-VN"),
        new CultureInfo("en-US")
    };
    o.DefaultRequestCulture = new("vi-VN");
    o.SupportedCultures = cultures;
    o.SupportedUICultures = cultures;
});
var app = builder.Build();
using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<XuLyDuLieu>().KhoiTaoAsync(app.Environment.ContentRootPath);
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception e)when (context.Request.Path.StartsWithSegments("/api") && !context.Response.HasStarted)
    {
        var status = e switch
        {
            ArgumentException => 400,
            KeyNotFoundException => 404,
            UnauthorizedAccessException => 403,
            InvalidOperationException => 409,
            SqlException s when s.Number is 2601 or 2627 or 547 or 1205 => 409,
            _ => 500
        };
        var message = e switch
        {
            ArgumentException or KeyNotFoundException or UnauthorizedAccessException or InvalidOperationException => e.Message,
            SqlException s when s.Number is 2601 or 2627 => "Mã, tên đăng nhập hoặc số điện thoại đã tồn tại.",
            SqlException s when s.Number == 547 => "Bản ghi đang được sử dụng hoặc vi phạm ràng buộc dữ liệu. Hãy kiểm tra thông tin liên quan.",
            SqlException s when s.Number == 1205 => "Dữ liệu vừa thay đổi. Vui lòng thử lại.",
            _ => "Không thể xử lý yêu cầu. Vui lòng thử lại hoặc kiểm tra kết nối SQL Server."
        };
        if (status == 500)
            app.Logger.LogError(e, "Xử lý yêu cầu quản lý sửa xe thất bại");
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { message });
    }
});
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

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
