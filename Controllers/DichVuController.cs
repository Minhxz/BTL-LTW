using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;
using SkiaSharp;

namespace QLyCuaHangSuaXe.Controllers;

// Người 3: dịch vụ công khai, mô tả editor và tải ảnh.
[ApiController]
public sealed class DichVuController(XuLyDuLieu db, IWebHostEnvironment environment) : ControllerBase
{
    private const string AnhDuPhong = "/images/vehicle-motorcycle.jpg";
    private const int DungLuongAnhToiDa = 2 * 1024 * 1024;
    private const long SoDiemAnhToiDa = 24_000_000;

    // Danh sách dịch vụ đang hoạt động cho trang khách.
    [HttpGet("/api/services")]
    public async Task<IActionResult> DanhSachDichVu()
    {
        string sqlDichVu = "Select d.MaDichVu As id, d.TenDichVu As name, d.ThanhTienDichVu As price, " +
            "Coalesce(NullIf(w.MoTaHtml, N''), d.MoTa) As description, w.Anh As imageUrl " +
            "From DichVu d Left Join WebDichVu w On w.MaDichVu = d.MaDichVu " +
            "Where d.TrangThai = 1 " +
            "Order By d.TenDichVu";

        List<Dictionary<string, object?>> danhSachDichVu = await db.DocBangAsync(sqlDichVu);

        foreach (Dictionary<string, object?> dichVu in danhSachDichVu)
        {
            dichVu["imageUrl"] = AnhHienThi(dichVu.Chuoi("id"), dichVu.Chuoi("imageUrl"));
        }

        return Ok(new { services = danhSachDichVu });
    }

    // Chọn ảnh hiển thị: ảnh đã tải lên nếu còn file, không thì ảnh mẫu theo mã dịch vụ.
    private string AnhHienThi(string maDichVu, string anh)
    {
        string ma = maDichVu.ToUpperInvariant();

        // Các ảnh mặc định cũ được thay bằng ảnh mẫu hiện tại, giữ ảnh riêng đã tải lên.
        bool laAnhMacDinhCu = (ma, anh.ToLowerInvariant()) is
            (_, "/images/workshop-detail.jpg") or
            ("DV04", "/images/gemini-dv04.jpg") or
            ("DV05", "/images/gemini-dv04.jpg" or "/images/gemini-dv05.jpg");

        if (!laAnhMacDinhCu && AnhCoSan(anh))
        {
            return anh;
        }

        string anhMau = ma switch
        {
            "DV04" => "/images/gemini-dv05.jpg",
            "DV05" => "/images/bao-duong-xe-so-con-tay.png",
            _ when Regex.IsMatch(ma, @"^DV(?:0[1-9]|1[0-9]|2[0-2])$") => $"/images/gemini-dv{ma[2..]}.jpg",
            _ => AnhDuPhong
        };

        return AnhCoSan(anhMau) ? anhMau : AnhDuPhong;
    }

    // Đường dẫn ảnh hợp lệ (đúng thư mục, đúng đuôi) và file thật sự tồn tại.
    private bool AnhCoSan(string anh)
    {
        bool dungDinhDang = Regex.IsMatch(anh, @"^/(?:uploads/services/[a-f0-9]{32}|images/[a-zA-Z0-9_-]+)\.(?:jpg|png|webp)\z");

        return dungDinhDang && environment.WebRootFileProvider.GetFileInfo(anh[1..]).Exists;
    }

    [HttpGet("/api/workshop/services/{id}/content"), Authorize(Roles = "Manager")]
    public async Task<IActionResult> DocNoiDungDichVu(string id)
    {
        string sqlNoiDung = "Select d.MaDichVu As id, Coalesce(w.MoTaHtml, d.MoTa) As description, Coalesce(w.Anh, '') As imageUrl " +
            "From DichVu d Left Join WebDichVu w On w.MaDichVu = d.MaDichVu " +
            "Where d.MaDichVu = @id";

        Dictionary<string, object?> noiDung = (await db.DocBangAsync(sqlNoiDung, new { id })).SingleOrDefault()
            ?? throw new KeyNotFoundException("Không tìm thấy dịch vụ.");

        return Ok(noiDung);
    }

    // Lưu mô tả (đã lọc HTML, chỉ giữ thẻ an toàn) và ảnh đại diện của dịch vụ.
    [HttpPut("/api/workshop/services/{id}/content"), Authorize(Roles = "Manager")]
    public async Task<IActionResult> LuuNoiDungDichVu(string id, NoiDungDichVu input)
    {
        string html = LamSachHtml.LocNoiDung(input.MoTa);

        if (html.Length > 8000)
        {
            throw new ArgumentException("Nội dung tối đa 8.000 ký tự sau khi định dạng.");
        }

        string anh = input.DuongDanAnh ?? "";

        if (anh != "" && !AnhCoSan(anh))
        {
            throw new ArgumentException("Vui lòng tải ảnh hợp lệ lên trước.");
        }

        const string sqlLuuNoiDung = """
            IF EXISTS (SELECT 1 FROM WebDichVu WITH (UPDLOCK, HOLDLOCK) WHERE MaDichVu = @id)
                UPDATE WebDichVu SET MoTaHtml = @html, Anh = @image WHERE MaDichVu = @id
            ELSE
                INSERT WebDichVu (MaDichVu, MoTaHtml, Anh) VALUES (@id, @html, @image)
            """;

        await db.TrongGiaoDichAsync(s => s.ThucThiAsync(sqlLuuNoiDung, new { id, html, image = anh }));

        return Ok(new { message = "Đã cập nhật nội dung dịch vụ." });
    }

    // Tải ảnh dịch vụ: giải mã thử toàn bộ ảnh để chắc đúng là JPG/PNG/WebP rồi mới lưu.
    [HttpPost("/api/services/image"), Authorize(Roles = "Manager"), RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> TaiAnhLen([FromForm] IFormFile image)
    {
        if (image.Length is < 12 or > DungLuongAnhToiDa)
        {
            throw new ArgumentException("Ảnh JPG/PNG/WebP tối đa 2 MB.");
        }

        CancellationToken huyYeuCau = HttpContext.RequestAborted;

        // 1. Đọc file vào bộ nhớ
        await using Stream luongVao = image.OpenReadStream();
        using MemoryStream noiDungAnh = new MemoryStream((int)image.Length);
        await luongVao.CopyToAsync(noiDungAnh, huyYeuCau);

        // 2. Nhận dạng định dạng ảnh theo nội dung, không tin đuôi file
        using SKData duLieuAnh = SKData.CreateCopy(noiDungAnh.GetBuffer().AsSpan(0, (int)noiDungAnh.Length));
        using SKCodec? boGiaiMa = SKCodec.Create(duLieuAnh);

        if (boGiaiMa == null)
        {
            throw new ArgumentException("Không đọc được ảnh JPG/PNG/WebP. Hãy chọn ảnh hợp lệ.");
        }

        string duoiFile = boGiaiMa.EncodedFormat switch
        {
            SKEncodedImageFormat.Jpeg => ".jpg",
            SKEncodedImageFormat.Png => ".png",
            SKEncodedImageFormat.Webp => ".webp",
            _ => throw new ArgumentException("Nội dung tệp không phải ảnh JPG/PNG/WebP.")
        };

        // 3. Giới hạn kích thước điểm ảnh (tính cả ảnh động nhiều khung hình)
        int soKhungHinh = Math.Max(1, boGiaiMa.FrameCount);
        SKImageInfo kichThuoc = boGiaiMa.Info;

        if ((long)kichThuoc.Width * kichThuoc.Height > SoDiemAnhToiDa / soKhungHinh)
        {
            throw new ArgumentException("Tổng số điểm ảnh của tệp không được vượt quá 24 triệu.");
        }

        // 4. Giải mã từng khung hình để phát hiện ảnh hỏng
        SKImageInfo thongTinGiaiMa = new SKImageInfo(kichThuoc.Width, kichThuoc.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using SKBitmap anhBitmap = new SKBitmap(thongTinGiaiMa);

        for (int khung = 0; khung < soKhungHinh; khung++)
        {
            huyYeuCau.ThrowIfCancellationRequested();

            SKCodecResult ketQua = boGiaiMa.GetPixels(thongTinGiaiMa, anhBitmap.GetPixels(), anhBitmap.RowBytes, new SKCodecOptions(khung));

            if (ketQua != SKCodecResult.Success)
            {
                throw new ArgumentException("Ảnh bị hỏng hoặc chưa đầy đủ. Hãy chọn ảnh hợp lệ.");
            }
        }

        // 5. Lưu file với tên ngẫu nhiên; lỗi giữa chừng thì xóa file dở dang
        noiDungAnh.Position = 0;

        string thuMuc = Path.Combine(environment.WebRootPath, "uploads", "services");
        Directory.CreateDirectory(thuMuc);

        string tenFile = Guid.NewGuid().ToString("N") + duoiFile;
        string duongDan = Path.Combine(thuMuc, tenFile);

        try
        {
            await using FileStream fileDich = System.IO.File.Create(duongDan);
            await noiDungAnh.CopyToAsync(fileDich, huyYeuCau);
        }
        catch
        {
            System.IO.File.Delete(duongDan);
            throw;
        }

        return Ok(new { imageUrl = "/uploads/services/" + tenFile });
    }
}
