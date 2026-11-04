using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;

// Người 3: dịch vụ công khai, mô tả editor và tải ảnh.
[ApiController]
public sealed class DichVuController(XuLyDuLieu db, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("/api/services")]
    public async Task<IActionResult> DanhSachDichVu() => Ok(new { services = await db.DocBangAsync("SELECT d.MaDichVu id,d.TenDichVu name,d.ThanhTienDichVu price,COALESCE(NULLIF(w.MoTaHtml,N''),d.MoTa) description,COALESCE(NULLIF(w.Anh,N''),'/images/workshop-detail.jpg') imageUrl FROM DichVu d LEFT JOIN WebDichVu w ON w.MaDichVu=d.MaDichVu WHERE d.TrangThai=1 ORDER BY d.TenDichVu") });
    [HttpGet("/api/workshop/services/{id}/content"), Authorize(Roles = "Manager")]
    public async Task<IActionResult> DocNoiDungDichVu(string id) => Ok((await db.DocBangAsync("SELECT d.MaDichVu id,COALESCE(w.MoTaHtml,d.MoTa) description,COALESCE(w.Anh,'') imageUrl FROM DichVu d LEFT JOIN WebDichVu w ON w.MaDichVu=d.MaDichVu WHERE d.MaDichVu=@id", new { id })).SingleOrDefault() ?? throw new KeyNotFoundException("Không tìm thấy dịch vụ."));
    [HttpPut("/api/workshop/services/{id}/content"), Authorize(Roles = "Manager")]
    public async Task<IActionResult> LuuNoiDungDichVu(string id, NoiDungDichVu input)
    {
        var html = LamSachHtml.LocNoiDung(input.MoTa);
        if (html.Length > 8000)
            throw new ArgumentException("Nội dung tối đa 8.000 ký tự sau khi định dạng.");
        var image = input.DuongDanAnh ?? "";
        if (image != "" && (!Regex.IsMatch(image, @"^/(?:uploads/services/[a-f0-9]{32}|images/[a-zA-Z0-9_-]+)\.(?:jpg|png|webp)$") || !System.IO.File.Exists(Path.Combine(environment.WebRootPath, image.TrimStart('/')))))
            throw new ArgumentException("Vui lòng tải ảnh hợp lệ lên trước.");
        await db.TrongGiaoDichAsync(async s => await s.ThucThiAsync("IF EXISTS(SELECT 1 FROM WebDichVu WITH(UPDLOCK,HOLDLOCK) WHERE MaDichVu=@id) UPDATE WebDichVu SET MoTaHtml=@html,Anh=@image WHERE MaDichVu=@id ELSE INSERT WebDichVu(MaDichVu,MoTaHtml,Anh) VALUES(@id,@html,@image)", new { id, html, image }));
        return Ok(new { message = "Đã cập nhật nội dung dịch vụ." });
    }

    [HttpPost("/api/services/image"), Authorize(Roles = "Manager"), RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> TaiAnhLen([FromForm] IFormFile image)
    {
        if (image.Length is < 12 or > 2 * 1024 * 1024)
            throw new ArgumentException("Ảnh JPG/PNG/WebP tối đa 2 MB.");
        await using var source = image.OpenReadStream();
        var header = new byte[12];
        await source.ReadExactlyAsync(header);
        var extension = header[0] == 255 && header[1] == 216 && header[2] == 255 ? ".jpg" : header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? ".png" : header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8) ? ".webp" : null;
        if (extension == null)
            throw new ArgumentException("Nội dung tệp không phải ảnh JPG/PNG/WebP.");
        var folder = Path.Combine(environment.WebRootPath, "uploads", "services");
        Directory.CreateDirectory(folder);
        var name = Guid.NewGuid().ToString("N") + extension;
        await using (var target = System.IO.File.Create(Path.Combine(folder, name)))
        {
            await target.WriteAsync(header);
            await source.CopyToAsync(target);
        }

        return Ok(new { imageUrl = "/uploads/services/" + name });
    }
}
