using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;

[ApiController]
public sealed class LichHenController(XuLyDuLieu duLieu, NghiepVuSuaXe nghiepVuSuaXe)
    : ControllerBase
{
    private static readonly string[] CacBuocTienDo =
    [
        "Chờ xác nhận",
        "Tiếp nhận xe",
        "Đang sửa chữa",
        "Hoàn thành",
    ];

    public static string ChuanHoaDienThoai(string noiDung)
    {
        string soDienThoai = Regex.Replace(noiDung ?? "", @"[\s().-]", "");
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

    [HttpPost("/api/appointments"), EnableRateLimiting("public-api")]
    public async Task<IActionResult> DatLich(YeuCauDatLich yeuCau)
    {
        string soDienThoai = ChuanHoaDienThoai(yeuCau.SoDienThoai);
        if (string.IsNullOrWhiteSpace(yeuCau.BienSo) || yeuCau.HangXe.Length > 50)
        {
            throw new ArgumentException("Nhập biển số và hãng xe tối đa 50 ký tự.");
        }

        string bienSoChuanHoa = BienSoXe.ChuanHoa(yeuCau.BienSo);
        bool docDuocNgay = DateTime.TryParseExact(
            yeuCau.Ngay + " " + yeuCau.Gio,
            "yyyy-MM-dd HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime ngayHen
        );
        if (!docDuocNgay || !LaGioHenHopLe(ngayHen))
        {
            throw new ArgumentException(
                "Chọn lịch trong 90 ngày tới, từ 08:00 đến 17:30 theo mốc 30 phút."
            );
        }

        string maLichHen = "LH-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        await duLieu.TrongGiaoDichAsync(async phien =>
        {
            string sqlDichVu =
                "Select MaDichVu, TenDichVu "
                + "From DichVu With (HoldLock) "
                + "Where MaDichVu = @name And TrangThai = 1";
            Dictionary<string, object?> dichVu =
                (
                    await phien.DocBangAsync(sqlDichVu, new { name = yeuCau.MaDichVu })
                ).SingleOrDefault()
                ?? throw new ArgumentException("Dịch vụ không còn hoạt động: " + yeuCau.MaDichVu);
            PhanLoaiXeMay.KiemTraDichVu(yeuCau.LoaiXe.Trim(), dichVu.Chuoi("TenDichVu"));
            string sqlLichDangCho =
                "Select Count(*) "
                + "From WebLichHen With (UpdLock, HoldLock) "
                + "Where "
                + BienSoXe.KhoaSql("BienXe")
                + " = @plate And TrangThai = N'Chờ xác nhận'";
            if (await phien.DocGiaTriAsync<int>(sqlLichDangCho, new { plate = bienSoChuanHoa }) > 0)
            {
                throw new InvalidOperationException(
                    "Xe đã có lịch đang chờ xác nhận. Hãy tra cứu hoặc liên hệ cửa hàng."
                );
            }

            string sqlThemLich =
                "Insert Into WebLichHen (MaLichHen, TenKhachHang, SoDienThoai, BienXe, LoaiXe, HangSanXuat, MauSac, MaDichVu, NgayHen, GhiChu, TrangThai, NgayTao) "
                + "Values (@code, @name, @phone, @plate, @type, @brand, N'Chưa cập nhật', @service, @date, @note, N'Chờ xác nhận', @now)";
            await phien.ThucThiAsync(
                sqlThemLich,
                new
                {
                    code = maLichHen,
                    name = yeuCau.TenKhachHang.Trim(),
                    phone = soDienThoai,
                    plate = yeuCau.BienSo.Trim().ToUpperInvariant(),
                    type = yeuCau.LoaiXe.Trim(),
                    brand = yeuCau.HangXe.Trim(),
                    service = dichVu.Chuoi("MaDichVu"),
                    date = ngayHen,
                    note = yeuCau.GhiChu?.Trim() ?? "",
                    now = XuLyDuLieu.ThoiGianHienTai,
                }
            );
            return true;
        });
        return StatusCode(
            201,
            new
            {
                reference = maLichHen,
                date = yeuCau.Ngay,
                time = yeuCau.Gio,
                service = yeuCau.MaDichVu,
            }
        );
    }

    [HttpGet("/api/repairs/lookup"), EnableRateLimiting("public-api")]
    public async Task<IActionResult> TraCuu(
        [FromQuery(Name = "reference")] string maTraCuu,
        [FromQuery(Name = "phone")] string dienThoai
    )
    {
        if (maTraCuu.Length > 40)
        {
            throw new ArgumentException("Mã tra cứu không hợp lệ.");
        }

        string sqlTraCuu =
            "Select w.*, d.TenDichVu, p.TrangThai As TienDo "
            + "From WebLichHen w "
            + "Join DichVu d On d.MaDichVu = w.MaDichVu "
            + "Left Join PhieuTiepNhan p On p.MaPhieuTiepNhan = w.MaPhieuTiepNhan "
            + "Where w.MaLichHen = @reference And w.SoDienThoai = @phone";
        Dictionary<string, object?> lichHen =
            (
                await duLieu.DocBangAsync(
                    sqlTraCuu,
                    new { reference = maTraCuu, phone = ChuanHoaDienThoai(dienThoai) }
                )
            ).SingleOrDefault()
            ?? throw new KeyNotFoundException("Không tìm thấy lịch hẹn khớp mã và số điện thoại.");
        string tienDo = lichHen.Chuoi("TienDo");
        string nhanTrangThai = tienDo == "" ? lichHen.Chuoi("TrangThai") : tienDo;
        string trangThai = nhanTrangThai switch
        {
            "Chờ xác nhận" => "Pending",
            "Chờ sửa" => "Confirmed",
            "Đang sửa" => "InProgress",
            "Hoàn thành" => "Completed",
            _ => "Cancelled",
        };
        int buocHienTai = trangThai switch
        {
            "Confirmed" => 1,
            "InProgress" => 2,
            "Completed" => 4,
            _ => 0,
        };
        var cacBuoc = CacBuocTienDo.Select(
            (tieuDe, chiSo) =>
                new
                {
                    title = tieuDe,
                    description = "",
                    state = chiSo < buocHienTai ? "completed"
                    : chiSo == buocHienTai ? "current"
                    : "upcoming",
                }
        );
        DateTime ngayHen = (DateTime)lichHen["NgayHen"]!;
        return Ok(
            new
            {
                reference = maTraCuu,
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
                stages = cacBuoc,
            }
        );
    }

    [HttpPost("/api/appointments/{reference}/cancel"), EnableRateLimiting("public-api")]
    public async Task<IActionResult> HuyLich(
        [FromRoute(Name = "reference")] string maTraCuu,
        YeuCauHuyLich yeuCau
    )
    {
        string soDienThoai = ChuanHoaDienThoai(yeuCau.SoDienThoai);
        string sqlHuyLich =
            "Update WebLichHen "
            + "Set TrangThai = N'Đã hủy' "
            + "Where MaLichHen = @reference And SoDienThoai = @phone And TrangThai = N'Chờ xác nhận' And MaPhieuTiepNhan Is Null";
        if (
            await duLieu.ThucThiAsync(sqlHuyLich, new { reference = maTraCuu, phone = soDienThoai })
            != 1
        )
        {
            throw new InvalidOperationException(
                "Lịch không tồn tại hoặc đã tiếp nhận. Liên hệ cửa hàng để thay đổi."
            );
        }

        return await TraCuu(maTraCuu, soDienThoai);
    }

    [HttpGet("/api/workshop/bookings"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> DanhSachLichHen(
        [FromQuery(Name = "q")] string? tuKhoa = null,
        [FromQuery(Name = "page")] int trang = 1,
        [FromQuery(Name = "pageSize")] int kichThuocTrang = 12
    )
    {
        PhanTrang.KiemTra(trang, kichThuocTrang, tuKhoa);
        var thamSo = new
        {
            q = "%" + tuKhoa + "%",
            skip = (trang - 1) * kichThuocTrang,
            size = kichThuocTrang,
        };
        const string dieuKienLoc =
            " Where (w.TenKhachHang Like @q Or w.BienXe Like @q Or w.SoDienThoai Like @q Or w.MaLichHen Like @q)";
        string sqlDem = "Select Count(*) From WebLichHen w" + dieuKienLoc;
        string sqlDanhSach =
            "Select w.MaLichHen As id, w.TenKhachHang As customerName, w.SoDienThoai As phone, w.BienXe As licensePlate, "
            + "w.HangSanXuat As brand, w.LoaiXe As vehicleType, w.NgayHen As date, w.TrangThai As status, w.GhiChu As note, "
            + "w.MaPhieuTiepNhan As jobId, w.MaDichVu As serviceId, d.TenDichVu As service "
            + "From WebLichHen w Join DichVu d On d.MaDichVu = w.MaDichVu"
            + dieuKienLoc
            + " Order By Case When w.TrangThai = N'Chờ xác nhận' Then 0 Else 1 End, w.NgayTao Desc "
            + "Offset @skip Rows Fetch Next @size Rows Only";
        int tongSoLich = await duLieu.DocGiaTriAsync<int>(sqlDem, thamSo);
        List<Dictionary<string, object?>> danhSachLich = await duLieu.DocBangAsync(
            sqlDanhSach,
            thamSo
        );
        return Ok(
            new
            {
                items = danhSachLich,
                total = tongSoLich,
                page = trang,
                totalPages = Math.Max(1, (int)Math.Ceiling(tongSoLich / (double)kichThuocTrang)),
            }
        );
    }

    [HttpPost("/api/workshop/bookings/{id}/accept"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> TiepNhanLich(
        [FromRoute(Name = "id")] string maBanGhi,
        YeuCauNhanLich yeuCau
    )
    {
        string maPhieu = await duLieu.TrongGiaoDichAsync(async phien =>
        {
            string sqlLichHen =
                "Select * "
                + "From WebLichHen With (UpdLock, HoldLock) "
                + "Where MaLichHen = @id And TrangThai = N'Chờ xác nhận' And MaPhieuTiepNhan Is Null";
            Dictionary<string, object?> lichHen =
                (await phien.DocBangAsync(sqlLichHen, new { id = maBanGhi })).SingleOrDefault()
                ?? throw new InvalidOperationException("Lịch đã xử lý hoặc không tồn tại.");
            ThongTinTiepNhan thongTinTiepNhan = new ThongTinTiepNhan(
                lichHen.Chuoi("TenKhachHang"),
                lichHen.Chuoi("SoDienThoai"),
                lichHen.Chuoi("BienXe"),
                lichHen.Chuoi("LoaiXe"),
                lichHen.Chuoi("HangSanXuat"),
                yeuCau.MauSac,
                yeuCau.NgayHenTra,
                [new DichVuChon(lichHen.Chuoi("MaDichVu"), 1)],
                yeuCau.MaTho
            );
            string maPhieuMoi = await nghiepVuSuaXe.TaoPhieu(
                phien,
                thongTinTiepNhan,
                PhanQuyenNguoiDung.MaNhanVien(User)
            );
            string sqlCapNhatLich =
                "Update WebLichHen "
                + "Set TrangThai = N'Đã tiếp nhận', MaPhieuTiepNhan = @receipt "
                + "Where MaLichHen = @id";
            await phien.ThucThiAsync(sqlCapNhatLich, new { id = maBanGhi, receipt = maPhieuMoi });
            return maPhieuMoi;
        });
        return Ok(new { id = maPhieu, message = "Đã tiếp nhận lịch hẹn thành phiếu sửa chữa." });
    }

    [HttpPost("/api/workshop/bookings/{id}/reject"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> TuChoiLich([FromRoute(Name = "id")] string maBanGhi)
    {
        string sqlTuChoi =
            "Update WebLichHen "
            + "Set TrangThai = N'Đã hủy' "
            + "Where MaLichHen = @id And TrangThai = N'Chờ xác nhận'";
        if (await duLieu.ThucThiAsync(sqlTuChoi, new { id = maBanGhi }) != 1)
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
