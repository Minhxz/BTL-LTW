using System.Text.RegularExpressions;
using SkiaSharp;
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
    public async Task<IActionResult> DanhSachDichVu()
    {
        var services = await db.DocBangAsync("SELECT d.MaDichVu id,d.TenDichVu name,d.ThanhTienDichVu price,COALESCE(NULLIF(w.MoTaHtml,N''),d.MoTa) description,w.Anh imageUrl FROM DichVu d LEFT JOIN WebDichVu w ON w.MaDichVu=d.MaDichVu WHERE d.TrangThai=1 ORDER BY d.TenDichVu");
        foreach (var service in services)
            service["imageUrl"] = AnhHienThi(service.Chuoi("id"), service.Chuoi("imageUrl"));
        return Ok(new { services });
    }

    private string AnhHienThi(string id, string anh)
    {
        var ma = id.ToUpperInvariant();
        // Các ảnh mặc định cũ được thay bằng ảnh mẫu hiện tại, giữ ảnh riêng đã tải lên.
        var anhMacDinhCu = (ma, anh.ToLowerInvariant()) is
            (_, "/images/workshop-detail.jpg") or
            ("DV04", "/images/gemini-dv04.jpg") or
            ("DV05", "/images/gemini-dv04.jpg" or "/images/gemini-dv05.jpg");
        if (!anhMacDinhCu && AnhCoSan(anh))
            return anh;

        const string anhDuPhong = "/images/vehicle-motorcycle.jpg";
        var anhMau = ma switch
        {
            "DV04" => "/images/gemini-dv05.jpg",
            "DV05" => "/images/bao-duong-xe-so-con-tay.png",
            _ when Regex.IsMatch(ma, @"^DV(?:0[1-9]|1[0-9]|2[0-2])$") => $"/images/gemini-dv{ma[2..]}.jpg",
            _ => anhDuPhong
        };
        return AnhCoSan(anhMau) ? anhMau : anhDuPhong;
    }

    private bool AnhCoSan(string anh) =>
        Regex.IsMatch(anh, @"^/(?:uploads/services/[a-f0-9]{32}|images/[a-zA-Z0-9_-]+)\.(?:jpg|png|webp)\z") &&
        environment.WebRootFileProvider.GetFileInfo(anh[1..]).Exists;

    [HttpGet("/api/workshop/services/{id}/content"), Authorize(Roles = "Manager")]
    public async Task<IActionResult> DocNoiDungDichVu(string id) => Ok((await db.DocBangAsync("SELECT d.MaDichVu id,COALESCE(w.MoTaHtml,d.MoTa) description,COALESCE(w.Anh,'') imageUrl FROM DichVu d LEFT JOIN WebDichVu w ON w.MaDichVu=d.MaDichVu WHERE d.MaDichVu=@id", new { id })).SingleOrDefault() ?? throw new KeyNotFoundException("Không tìm thấy dịch vụ."));
    [HttpPut("/api/workshop/services/{id}/content"), Authorize(Roles = "Manager")]
    public async Task<IActionResult> LuuNoiDungDichVu(string id, NoiDungDichVu input)
    {
        var html = LamSachHtml.LocNoiDung(input.MoTa);
        if (html.Length > 8000)
            throw new ArgumentException("Nội dung tối đa 8.000 ký tự sau khi định dạng.");
        var image = input.DuongDanAnh ?? "";
        if (image != "" && !AnhCoSan(image))
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
        var cancellation = HttpContext.RequestAborted;
        using var content = new MemoryStream((int)image.Length);
        await source.CopyToAsync(content, cancellation);
        using var encoded = SKData.CreateCopy(content.GetBuffer().AsSpan(0, (int)content.Length));
        using var codec = SKCodec.Create(encoded);
        if (codec == null)
            throw new ArgumentException("Không đọc được ảnh JPG/PNG/WebP. Hãy chọn ảnh hợp lệ.");
        var extension = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Jpeg => ".jpg",
            SKEncodedImageFormat.Png => ".png",
            SKEncodedImageFormat.Webp => ".webp",
            _ => throw new ArgumentException("Nội dung tệp không phải ảnh JPG/PNG/WebP.")
        };
        var frames = Math.Max(1, codec.FrameCount);
        var size = codec.Info;
        if ((long)size.Width * size.Height > 24_000_000 / frames)
            throw new ArgumentException("Tổng số điểm ảnh của tệp không được vượt quá 24 triệu.");
        var info = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        for (var frame = 0; frame < frames; frame++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (codec.GetPixels(info, bitmap.GetPixels(), bitmap.RowBytes, new SKCodecOptions(frame)) != SKCodecResult.Success)
                throw new ArgumentException("Ảnh bị hỏng hoặc chưa đầy đủ. Hãy chọn ảnh hợp lệ.");
        }
        content.Position = 0;
        var folder = Path.Combine(environment.WebRootPath, "uploads", "services");
        Directory.CreateDirectory(folder);
        var name = Guid.NewGuid().ToString("N") + extension;
        var path = Path.Combine(folder, name);
        try
        {
            await using var target = System.IO.File.Create(path);
            await content.CopyToAsync(target, cancellation);
        }
        catch
        {
            System.IO.File.Delete(path);
            throw;
        }

        return Ok(new { imageUrl = "/uploads/services/" + name });
    }
}
