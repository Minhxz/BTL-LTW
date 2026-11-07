using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QLyCuaHangSuaXe.Models;
using QLyCuaHangSuaXe.LopDungChung;

namespace QLyCuaHangSuaXe.Controllers;

// Người 2: đặt lịch, tra cứu, hủy và tiếp nhận lịch; gọi nghiệp vụ sửa xe dùng chung.
[ApiController]
public sealed class LichHenController(XuLyDuLieu db, NghiepVuSuaXe workflow) : ControllerBase
{
    public static string ChuanHoaDienThoai(string text)
    {
        var phone = Regex.Replace(text ?? "", @"[\s().-]", "");
        if (phone.StartsWith("+84"))
            phone = "0" + phone[3..];
        if (!Regex.IsMatch(phone, @"^0[0-9]{8,10}$"))
            throw new ArgumentException("Số điện thoại không hợp lệ.");
        return phone;
    }

    [HttpPost("/api/appointments"), EnableRateLimiting("public-api")]
    public async Task<IActionResult> DatLich(YeuCauDatLich input)
    {
        var phone = ChuanHoaDienThoai(input.SoDienThoai);
        if (string.IsNullOrWhiteSpace(input.BienSo) || input.HangXe.Length > 50)
            throw new ArgumentException("Nhập biển số và hãng xe tối đa 50 ký tự.");
        var plateKey = BienSoXe.ChuanHoa(input.BienSo);
        if (!DateTime.TryParseExact(input.Ngay + " " + input.Gio, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date <= XuLyDuLieu.ThoiGianHienTai || date > XuLyDuLieu.ThoiGianHienTai.AddDays(90) || date.Hour < 8 || date.TimeOfDay > new TimeSpan(17, 30, 0) || date.Minute % 30 != 0)
            throw new ArgumentException("Chọn lịch trong 90 ngày tới, từ 08:00 đến 17:30 theo mốc 30 phút.");
        var code = "LH-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        await db.TrongGiaoDichAsync(async s =>
        {
            var service = (await s.DocBangAsync("SELECT MaDichVu,TenDichVu FROM DichVu WITH(HOLDLOCK) WHERE MaDichVu=@name AND TrangThai=1", new { name = input.MaDichVu })).SingleOrDefault()
                ?? throw new ArgumentException("Dịch vụ không còn hoạt động: " + input.MaDichVu);
            PhanLoaiXeMay.KiemTraDichVu(input.LoaiXe.Trim(), service.Chuoi("TenDichVu"));
            if (await s.DocGiaTriAsync<int>($"SELECT COUNT(*) FROM WebLichHen WITH(UPDLOCK,HOLDLOCK) WHERE {BienSoXe.KhoaSql("BienXe")}=@plate AND TrangThai=N'Chờ xác nhận'", new { plate = plateKey }) > 0)
                throw new InvalidOperationException("Xe đã có lịch đang chờ xác nhận. Hãy tra cứu hoặc liên hệ cửa hàng.");
            await s.ThucThiAsync("INSERT WebLichHen(MaLichHen,TenKhachHang,SoDienThoai,BienXe,LoaiXe,HangSanXuat,MauSac,MaDichVu,NgayHen,GhiChu,TrangThai,NgayTao) VALUES(@code,@name,@phone,@plate,@type,@brand,N'Chưa cập nhật',@service,@date,@note,N'Chờ xác nhận',@now)", new { code, name = input.TenKhachHang.Trim(), phone, plate = input.BienSo.Trim().ToUpperInvariant(), type = input.LoaiXe.Trim(), brand = input.HangXe.Trim(), service = service.Chuoi("MaDichVu"), date, note = input.GhiChu?.Trim() ?? "", now = XuLyDuLieu.ThoiGianHienTai });
            return true;
        });
        return StatusCode(201, new { reference = code, date = input.Ngay, time = input.Gio, service = input.MaDichVu });
    }

    [HttpGet("/api/repairs/lookup"), EnableRateLimiting("public-api")]
    public async Task<IActionResult> TraCuu(string reference, string phone)
    {
        if (reference.Length > 40)
            throw new ArgumentException("Mã tra cứu không hợp lệ.");
        var row = (await db.DocBangAsync("SELECT w.*,d.TenDichVu,p.TrangThai TienDo FROM WebLichHen w JOIN DichVu d ON d.MaDichVu=w.MaDichVu LEFT JOIN PhieuTiepNhan p ON p.MaPhieuTiepNhan=w.MaPhieuTiepNhan WHERE w.MaLichHen=@reference AND w.SoDienThoai=@phone", new { reference, phone = ChuanHoaDienThoai(phone) })).SingleOrDefault();
        if (row == null)
            throw new KeyNotFoundException("Không tìm thấy lịch hẹn khớp mã và số điện thoại.");
        var label = row.Chuoi("TienDo")is "" ? row.Chuoi("TrangThai") : row.Chuoi("TienDo");
        var status = label switch
        {
            "Chờ xác nhận" => "Pending",
            "Chờ sửa" => "Confirmed",
            "Đang sửa" => "InProgress",
            "Hoàn thành" => "Completed",
            _ => "Cancelled"
        };
        var stage = status switch
        {
            "Confirmed" => 1,
            "InProgress" => 2,
            "Completed" => 4,
            _ => 0
        };
        var date = (DateTime)row["NgayHen"]!;
        return Ok(new { reference, customerName = row.Chuoi("TenKhachHang"), vehicleType = row.Chuoi("LoaiXe"), brand = row.Chuoi("HangSanXuat"), licensePlate = row.Chuoi("BienXe"), service = row.Chuoi("TenDichVu"), date = date.ToString("yyyy-MM-dd"), time = date.ToString("HH:mm"), status, statusLabel = label, canCancel = status == "Pending", stages = new[] { "Chờ xác nhận", "Tiếp nhận xe", "Đang sửa chữa", "Hoàn thành" }.Select((title, i) => new { title, description = "", state = i < stage ? "completed" : i == stage ? "current" : "upcoming" }) });
    }

    [HttpPost("/api/appointments/{reference}/cancel"), EnableRateLimiting("public-api")]
    public async Task<IActionResult> HuyLich(string reference, YeuCauHuyLich input)
    {
        var phone = ChuanHoaDienThoai(input.SoDienThoai);
        if (await db.ThucThiAsync("UPDATE WebLichHen SET TrangThai=N'Đã hủy' WHERE MaLichHen=@reference AND SoDienThoai=@phone AND TrangThai=N'Chờ xác nhận' AND MaPhieuTiepNhan IS NULL", new { reference, phone }) != 1)
            throw new InvalidOperationException("Lịch không tồn tại hoặc đã tiếp nhận. Liên hệ cửa hàng để thay đổi.");
        return await TraCuu(reference, phone);
    }

    [HttpGet("/api/workshop/bookings"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> DanhSachLichHen(string? q = null, int page = 1, int pageSize = 12)
    {
        PhanTrang.KiemTra(page, pageSize, q);
        var args = new
        {
            q = "%" + q + "%",
            skip = (page - 1) * pageSize,
            size = pageSize
        };
        var where = " WHERE (w.TenKhachHang LIKE @q OR w.BienXe LIKE @q OR w.SoDienThoai LIKE @q OR w.MaLichHen LIKE @q)";
        var total = await db.DocGiaTriAsync<int>("SELECT COUNT(*) FROM WebLichHen w" + where, args);
        var items = await db.DocBangAsync("SELECT w.MaLichHen id,w.TenKhachHang customerName,w.SoDienThoai phone,w.BienXe licensePlate,w.HangSanXuat brand,w.LoaiXe vehicleType,w.NgayHen date,w.TrangThai status,w.GhiChu note,w.MaPhieuTiepNhan jobId,w.MaDichVu serviceId,d.TenDichVu service FROM WebLichHen w JOIN DichVu d ON d.MaDichVu=w.MaDichVu" + where + " ORDER BY CASE WHEN w.TrangThai=N'Chờ xác nhận' THEN 0 ELSE 1 END,w.NgayTao DESC OFFSET @skip ROWS FETCH NEXT @size ROWS ONLY", args);
        return Ok(new { items, total, page, totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)) });
    }

    [HttpPost("/api/workshop/bookings/{id}/accept"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> TiepNhanLich(string id, YeuCauNhanLich input)
    {
        var code = await db.TrongGiaoDichAsync(async s =>
        {
            var row = (await s.DocBangAsync("SELECT * FROM WebLichHen WITH(UPDLOCK,HOLDLOCK) WHERE MaLichHen=@id AND TrangThai=N'Chờ xác nhận' AND MaPhieuTiepNhan IS NULL", new { id })).SingleOrDefault() ?? throw new InvalidOperationException("Lịch đã xử lý hoặc không tồn tại.");
            var intake = new ThongTinTiepNhan(row.Chuoi("TenKhachHang"), row.Chuoi("SoDienThoai"), row.Chuoi("BienXe"), row.Chuoi("LoaiXe"), row.Chuoi("HangSanXuat"), input.MauSac, input.NgayHenTra, [new(row.Chuoi("MaDichVu"), 1)], input.MaTho);
            var receipt = await workflow.TaoPhieu(s, intake, PhanQuyenNguoiDung.MaNhanVien(User));
            await s.ThucThiAsync("UPDATE WebLichHen SET TrangThai=N'Đã tiếp nhận',MaPhieuTiepNhan=@receipt WHERE MaLichHen=@id", new { id, receipt });
            return receipt;
        });
        return Ok(new { id = code, message = "Đã tiếp nhận lịch hẹn thành phiếu sửa chữa." });
    }

    [HttpPost("/api/workshop/bookings/{id}/reject"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> TuChoiLich(string id)
    {
        if (await db.ThucThiAsync("UPDATE WebLichHen SET TrangThai=N'Đã hủy' WHERE MaLichHen=@id AND TrangThai=N'Chờ xác nhận'", new { id }) != 1)
            throw new InvalidOperationException("Lịch đã được xử lý.");
        return Ok(new { message = "Đã hủy yêu cầu đặt lịch." });
    }
}
