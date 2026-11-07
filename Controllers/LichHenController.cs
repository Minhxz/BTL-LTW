using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;

// Người 2: đặt lịch, tra cứu, hủy và tiếp nhận lịch; gọi nghiệp vụ sửa xe dùng chung của Người 3.
[ApiController]
public sealed class LichHenController(XuLyDuLieu db, NghiepVuSuaXe workflow) : ControllerBase
{
    // Các bước tiến độ hiển thị cho khách khi tra cứu.
    private static readonly string[] CacBuocTienDo = ["Chờ xác nhận", "Tiếp nhận xe", "Đang sửa chữa", "Hoàn thành"];

    // Bỏ khoảng trắng, dấu chấm, gạch, ngoặc; đổi +84 thành 0. Ví dụ: "+84 912.345.678" → "0912345678".
    public static string ChuanHoaDienThoai(string text)
    {
        string soDienThoai = Regex.Replace(text ?? "", @"[\s().-]", "");

        if (soDienThoai.StartsWith("+84"))
        {
            soDienThoai = "0" + soDienThoai[3..];
        }

        if (!Regex.IsMatch(soDienThoai, @"^0[0-9]{8,10}$"))
        {
            throw new ArgumentException("Số điện thoại không hợp lệ.");
        }

        return soDienThoai;
    }

    // Khách đặt lịch online. Trả về mã tra cứu LH-... để khách theo dõi.
    [HttpPost("/api/appointments"), EnableRateLimiting("public-api")]
    public async Task<IActionResult> DatLich(YeuCauDatLich input)
    {
        // 1. Kiểm tra số điện thoại, biển số, hãng xe
        string soDienThoai = ChuanHoaDienThoai(input.SoDienThoai);

        if (string.IsNullOrWhiteSpace(input.BienSo) || input.HangXe.Length > 50)
        {
            throw new ArgumentException("Nhập biển số và hãng xe tối đa 50 ký tự.");
        }

        string bienSoChuanHoa = BienSoXe.ChuanHoa(input.BienSo);

        // 2. Giờ hẹn: trong 90 ngày tới, 08:00–17:30, theo mốc 30 phút
        bool docDuocNgay = DateTime.TryParseExact(input.Ngay + " " + input.Gio, "yyyy-MM-dd HH:mm",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime ngayHen);

        if (!docDuocNgay || !LaGioHenHopLe(ngayHen))
        {
            throw new ArgumentException("Chọn lịch trong 90 ngày tới, từ 08:00 đến 17:30 theo mốc 30 phút.");
        }

        string maLichHen = "LH-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));

        await db.TrongGiaoDichAsync(async s =>
        {
            // 3. Dịch vụ còn hoạt động và phù hợp loại xe
            string sqlDichVu = "Select MaDichVu, TenDichVu " +
                "From DichVu With (HoldLock) " +
                "Where MaDichVu = @name And TrangThai = 1";

            Dictionary<string, object?> dichVu = (await s.DocBangAsync(sqlDichVu, new { name = input.MaDichVu })).SingleOrDefault()
                ?? throw new ArgumentException("Dịch vụ không còn hoạt động: " + input.MaDichVu);

            PhanLoaiXeMay.KiemTraDichVu(input.LoaiXe.Trim(), dichVu.Chuoi("TenDichVu"));

            // 4. Mỗi xe chỉ có một lịch đang chờ xác nhận
            string sqlLichDangCho = "Select Count(*) " +
                "From WebLichHen With (UpdLock, HoldLock) " +
                "Where " + BienSoXe.KhoaSql("BienXe") + " = @plate And TrangThai = N'Chờ xác nhận'";

            if (await s.DocGiaTriAsync<int>(sqlLichDangCho, new { plate = bienSoChuanHoa }) > 0)
            {
                throw new InvalidOperationException("Xe đã có lịch đang chờ xác nhận. Hãy tra cứu hoặc liên hệ cửa hàng.");
            }

            // 5. Lưu lịch hẹn
            string sqlThemLich = "Insert Into WebLichHen (MaLichHen, TenKhachHang, SoDienThoai, BienXe, LoaiXe, HangSanXuat, MauSac, MaDichVu, NgayHen, GhiChu, TrangThai, NgayTao) " +
                "Values (@code, @name, @phone, @plate, @type, @brand, N'Chưa cập nhật', @service, @date, @note, N'Chờ xác nhận', @now)";

            await s.ThucThiAsync(sqlThemLich, new
            {
                code = maLichHen,
                name = input.TenKhachHang.Trim(),
                phone = soDienThoai,
                plate = input.BienSo.Trim().ToUpperInvariant(),
                type = input.LoaiXe.Trim(),
                brand = input.HangXe.Trim(),
                service = dichVu.Chuoi("MaDichVu"),
                date = ngayHen,
                note = input.GhiChu?.Trim() ?? "",
                now = XuLyDuLieu.ThoiGianHienTai
            });

            return true;
        });

        return StatusCode(201, new { reference = maLichHen, date = input.Ngay, time = input.Gio, service = input.MaDichVu });
    }

    // Khách tra cứu lịch bằng mã + số điện thoại (phải khớp cả hai).
    [HttpGet("/api/repairs/lookup"), EnableRateLimiting("public-api")]
    public async Task<IActionResult> TraCuu(string reference, string phone)
    {
        if (reference.Length > 40)
        {
            throw new ArgumentException("Mã tra cứu không hợp lệ.");
        }

        string sqlTraCuu = "Select w.*, d.TenDichVu, p.TrangThai As TienDo " +
            "From WebLichHen w " +
            "Join DichVu d On d.MaDichVu = w.MaDichVu " +
            "Left Join PhieuTiepNhan p On p.MaPhieuTiepNhan = w.MaPhieuTiepNhan " +
            "Where w.MaLichHen = @reference And w.SoDienThoai = @phone";

        Dictionary<string, object?> lichHen = (await db.DocBangAsync(sqlTraCuu, new { reference, phone = ChuanHoaDienThoai(phone) })).SingleOrDefault()
            ?? throw new KeyNotFoundException("Không tìm thấy lịch hẹn khớp mã và số điện thoại.");

        // Đã có phiếu sửa thì lấy tiến độ của phiếu, chưa có thì lấy trạng thái của lịch
        string tienDo = lichHen.Chuoi("TienDo");
        string nhanTrangThai = tienDo == "" ? lichHen.Chuoi("TrangThai") : tienDo;

        string trangThai = nhanTrangThai switch
        {
            "Chờ xác nhận" => "Pending",
            "Chờ sửa" => "Confirmed",
            "Đang sửa" => "InProgress",
            "Hoàn thành" => "Completed",
            _ => "Cancelled"
        };

        int buocHienTai = trangThai switch
        {
            "Confirmed" => 1,
            "InProgress" => 2,
            "Completed" => 4,
            _ => 0
        };

        var cacBuoc = CacBuocTienDo.Select((tieuDe, i) => new
        {
            title = tieuDe,
            description = "",
            state = i < buocHienTai ? "completed" : i == buocHienTai ? "current" : "upcoming"
        });

        DateTime ngayHen = (DateTime)lichHen["NgayHen"]!;

        return Ok(new
        {
            reference,
            customerName = lichHen.Chuoi("TenKhachHang"),
            vehicleType = lichHen.Chuoi("LoaiXe"),
            brand = lichHen.Chuoi("HangSanXuat"),
            licensePlate = lichHen.Chuoi("BienXe"),
            service = lichHen.Chuoi("TenDichVu"),
            date = ngayHen.ToString("yyyy-MM-dd"),
            time = ngayHen.ToString("HH:mm"),
            status = trangThai,
            statusLabel = nhanTrangThai,
            canCancel = trangThai == "Pending",
            stages = cacBuoc
        });
    }

    // Khách tự hủy lịch: chỉ khi lịch còn chờ xác nhận và chưa tạo phiếu.
    [HttpPost("/api/appointments/{reference}/cancel"), EnableRateLimiting("public-api")]
    public async Task<IActionResult> HuyLich(string reference, YeuCauHuyLich input)
    {
        string soDienThoai = ChuanHoaDienThoai(input.SoDienThoai);

        string sqlHuyLich = "Update WebLichHen " +
            "Set TrangThai = N'Đã hủy' " +
            "Where MaLichHen = @reference And SoDienThoai = @phone And TrangThai = N'Chờ xác nhận' And MaPhieuTiepNhan Is Null";

        if (await db.ThucThiAsync(sqlHuyLich, new { reference, phone = soDienThoai }) != 1)
        {
            throw new InvalidOperationException("Lịch không tồn tại hoặc đã tiếp nhận. Liên hệ cửa hàng để thay đổi.");
        }

        return await TraCuu(reference, soDienThoai);
    }

    // Nhân viên xem danh sách lịch; lịch chờ xác nhận lên đầu.
    [HttpGet("/api/workshop/bookings"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> DanhSachLichHen(string? q = null, int page = 1, int pageSize = 12)
    {
        PhanTrang.KiemTra(page, pageSize, q);

        var thamSo = new
        {
            q = "%" + q + "%",
            skip = (page - 1) * pageSize,
            size = pageSize
        };

        const string dieuKienLoc = " Where (w.TenKhachHang Like @q Or w.BienXe Like @q Or w.SoDienThoai Like @q Or w.MaLichHen Like @q)";

        string sqlDem = "Select Count(*) From WebLichHen w" + dieuKienLoc;

        string sqlDanhSach = "Select w.MaLichHen As id, w.TenKhachHang As customerName, w.SoDienThoai As phone, w.BienXe As licensePlate, " +
            "w.HangSanXuat As brand, w.LoaiXe As vehicleType, w.NgayHen As date, w.TrangThai As status, w.GhiChu As note, " +
            "w.MaPhieuTiepNhan As jobId, w.MaDichVu As serviceId, d.TenDichVu As service " +
            "From WebLichHen w Join DichVu d On d.MaDichVu = w.MaDichVu" +
            dieuKienLoc +
            " Order By Case When w.TrangThai = N'Chờ xác nhận' Then 0 Else 1 End, w.NgayTao Desc " +
            "Offset @skip Rows Fetch Next @size Rows Only";

        int tongSoLich = await db.DocGiaTriAsync<int>(sqlDem, thamSo);
        List<Dictionary<string, object?>> danhSachLich = await db.DocBangAsync(sqlDanhSach, thamSo);

        return Ok(new
        {
            items = danhSachLich,
            total = tongSoLich,
            page,
            totalPages = Math.Max(1, (int)Math.Ceiling(tongSoLich / (double)pageSize))
        });
    }

    // Nhận lịch: tạo phiếu tiếp nhận (gọi nghiệp vụ của Người 3, có trừ kho) và gắn phiếu vào lịch.
    [HttpPost("/api/workshop/bookings/{id}/accept"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> TiepNhanLich(string id, YeuCauNhanLich input)
    {
        string maPhieu = await db.TrongGiaoDichAsync(async s =>
        {
            string sqlLichHen = "Select * " +
                "From WebLichHen With (UpdLock, HoldLock) " +
                "Where MaLichHen = @id And TrangThai = N'Chờ xác nhận' And MaPhieuTiepNhan Is Null";

            Dictionary<string, object?> lichHen = (await s.DocBangAsync(sqlLichHen, new { id })).SingleOrDefault()
                ?? throw new InvalidOperationException("Lịch đã xử lý hoặc không tồn tại.");

            ThongTinTiepNhan thongTinTiepNhan = new ThongTinTiepNhan(
                lichHen.Chuoi("TenKhachHang"),
                lichHen.Chuoi("SoDienThoai"),
                lichHen.Chuoi("BienXe"),
                lichHen.Chuoi("LoaiXe"),
                lichHen.Chuoi("HangSanXuat"),
                input.MauSac,
                input.NgayHenTra,
                [new DichVuChon(lichHen.Chuoi("MaDichVu"), 1)],
                input.MaTho);

            string maPhieuMoi = await workflow.TaoPhieu(s, thongTinTiepNhan, PhanQuyenNguoiDung.MaNhanVien(User));

            string sqlCapNhatLich = "Update WebLichHen " +
                "Set TrangThai = N'Đã tiếp nhận', MaPhieuTiepNhan = @receipt " +
                "Where MaLichHen = @id";

            await s.ThucThiAsync(sqlCapNhatLich, new { id, receipt = maPhieuMoi });

            return maPhieuMoi;
        });

        return Ok(new { id = maPhieu, message = "Đã tiếp nhận lịch hẹn thành phiếu sửa chữa." });
    }

    [HttpPost("/api/workshop/bookings/{id}/reject"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> TuChoiLich(string id)
    {
        string sqlTuChoi = "Update WebLichHen " +
            "Set TrangThai = N'Đã hủy' " +
            "Where MaLichHen = @id And TrangThai = N'Chờ xác nhận'";

        if (await db.ThucThiAsync(sqlTuChoi, new { id }) != 1)
        {
            throw new InvalidOperationException("Lịch đã được xử lý.");
        }

        return Ok(new { message = "Đã hủy yêu cầu đặt lịch." });
    }

    private static bool LaGioHenHopLe(DateTime ngayHen)
    {
        DateTime bayGio = XuLyDuLieu.ThoiGianHienTai;

        if (ngayHen <= bayGio || ngayHen > bayGio.AddDays(90))
        {
            return false;
        }

        if (ngayHen.Hour < 8 || ngayHen.TimeOfDay > new TimeSpan(17, 30, 0))
        {
            return false;
        }

        return ngayHen.Minute % 30 == 0;
    }
}
