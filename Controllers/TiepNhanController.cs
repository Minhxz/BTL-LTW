using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;

[ApiController, Authorize(Roles = "Manager,Cashier,Mechanic"), Route("api/workshop")]
public sealed class TiepNhanController(XuLyDuLieu duLieu, NghiepVuSuaXe nghiepVuSuaXe)
    : ControllerBase
{
    private string DieuKienPhanCong =>
        User.IsInRole("Mechanic")
            ? " And Exists (Select 1 From ChiTietPhanCong a Where a.MaXe = p.MaXe And a.MaNhanVien = @employee)"
            : "";

    [HttpGet("jobs")]
    public async Task<IActionResult> DanhSach(
        [FromQuery(Name = "q")] string? tuKhoa = null,
        [FromQuery(Name = "status")] string? trangThai = null,
        [FromQuery(Name = "from")] string? ngayBatDau = null,
        [FromQuery(Name = "to")] string? ngayKetThuc = null,
        [FromQuery(Name = "page")] int trang = 1,
        [FromQuery(Name = "pageSize")] int kichThuocTrang = 12
    )
    {
        PhanTrang.KiemTra(trang, kichThuocTrang, tuKhoa);
        (DateTime? tuNgay, DateTime? denNgay) = PhanTrang.KhoangNgay(ngayBatDau, ngayKetThuc);
        string dieuKienLoc =
            " Where (@q = '' Or p.MaPhieuTiepNhan Like @term Or k.TenKhachHang Like @term Or k.SoDienThoai Like @term Or x.BienXe Like @term) "
            + "And (@status = '' Or p.TrangThai = @status) "
            + "And (@from Is Null Or p.NgayLap >= @from) "
            + "And (@to Is Null Or p.NgayLap <= @to)"
            + DieuKienPhanCong;
        var thamSo = new
        {
            q = tuKhoa ?? "",
            term = "%" + tuKhoa + "%",
            status = trangThai ?? "",
            from = tuNgay,
            to = denNgay,
            employee = PhanQuyenNguoiDung.MaNhanVien(User),
            skip = (trang - 1) * kichThuocTrang,
            size = kichThuocTrang,
        };
        string sqlDem = "Select Count(*) From (" + NghiepVuSuaXe.TruyVanPhieu + dieuKienLoc + ") z";
        string sqlDanhSach =
            NghiepVuSuaXe.TruyVanPhieu
            + dieuKienLoc
            + " Order By p.NgayLap Desc, p.MaPhieuTiepNhan Desc "
            + "Offset @skip Rows Fetch Next @size Rows Only";
        int tongSoPhieu = await duLieu.DocGiaTriAsync<int>(sqlDem, thamSo);
        List<Dictionary<string, object?>> danhSachPhieu = await duLieu.DocBangAsync(
            sqlDanhSach,
            thamSo
        );
        return Ok(
            new
            {
                items = danhSachPhieu,
                total = tongSoPhieu,
                page = trang,
                totalPages = Math.Max(1, (int)Math.Ceiling(tongSoPhieu / (double)kichThuocTrang)),
            }
        );
    }

    [HttpGet("jobs/{id}")]
    public async Task<IActionResult> ChiTiet([FromRoute(Name = "id")] string maBanGhi)
    {
        string sqlPhieu =
            NghiepVuSuaXe.TruyVanPhieu + " Where p.MaPhieuTiepNhan = @id" + DieuKienPhanCong;
        Dictionary<string, object?> phieu =
            (
                await duLieu.DocBangAsync(
                    sqlPhieu,
                    new { id = maBanGhi, employee = PhanQuyenNguoiDung.MaNhanVien(User) }
                )
            ).SingleOrDefault()
            ?? throw new KeyNotFoundException("Không tìm thấy phiếu hoặc bạn chưa được phân công.");
        string sqlDichVu =
            "Select c.MaDichVu As serviceId, d.TenDichVu As name, c.SoLuong As quantity, c.ThanhTien As amount "
            + "From ChiTietPhieuTiepNhan c Join DichVu d On d.MaDichVu = c.MaDichVu "
            + "Where c.MaPhieuTiepNhan = @id";
        List<Dictionary<string, object?>> danhSachDichVu = await duLieu.DocBangAsync(
            sqlDichVu,
            new { id = maBanGhi }
        );
        string sqlPhanCong =
            "Select c.MaNhanVien As employeeId, n.HoTen As name, c.ThoiGianBatDau As start, c.ThoiGianHoanThanh As [end] "
            + "From ChiTietPhanCong c Join NhanVien n On n.MaNhanVien = c.MaNhanVien "
            + "Where c.MaXe = @vehicle";
        List<Dictionary<string, object?>> danhSachPhanCong = await duLieu.DocBangAsync(
            sqlPhanCong,
            new { vehicle = phieu["vehicleId"] }
        );
        Dictionary<string, object?>? hoaDon = null;
        if (!User.IsInRole("Mechanic"))
        {
            string sqlHoaDon =
                "Select MaHoaDon As id, TienThanhToan As amount, NgayLap As createdOn, NgayThanhToan As paidOn "
                + "From HoaDon "
                + "Where MaPhieuTiepNhan = @id";
            hoaDon = (
                await duLieu.DocBangAsync(sqlHoaDon, new { id = maBanGhi })
            ).SingleOrDefault();
        }

        return Ok(
            new
            {
                job = phieu,
                lines = danhSachDichVu,
                assignments = danhSachPhanCong,
                invoice = hoaDon,
            }
        );
    }

    [HttpPost("jobs/preview"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> XemTruocPhieu(YeuCauXemTruocPhieu yeuCau)
    {
        return Ok(await nghiepVuSuaXe.XemTruocPhieu(yeuCau));
    }

    [HttpPost("jobs"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> TaoPhieu(ThongTinTiepNhan yeuCau)
    {
        string maPhieu = await nghiepVuSuaXe.TaoPhieuAsync(
            yeuCau,
            PhanQuyenNguoiDung.MaNhanVien(User)
        );
        return StatusCode(201, new { id = maPhieu, message = "Đã tiếp nhận xe và cập nhật kho." });
    }

    [HttpPut("jobs/{id}/status")]
    public async Task<IActionResult> DoiTrangThai(
        [FromRoute(Name = "id")] string maBanGhi,
        YeuCauTrangThai yeuCau
    )
    {
        await nghiepVuSuaXe.DoiTrangThaiAsync(maBanGhi, yeuCau.TrangThai, User);
        return Ok(new { message = "Đã cập nhật tiến độ." });
    }

    [HttpPut("jobs/{id}/due-date"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> DoiNgayHen(
        [FromRoute(Name = "id")] string maBanGhi,
        YeuCauNgayHen yeuCau
    )
    {
        DateTime ngayHenMoi = yeuCau.NgayHenTra.Date;
        if (ngayHenMoi < XuLyDuLieu.HomNay || ngayHenMoi > XuLyDuLieu.HomNay.AddDays(90))
        {
            throw new ArgumentException("Ngày hẹn phải trong 90 ngày tới.");
        }

        string sqlDoiNgayHen =
            "Update PhieuTiepNhan "
            + "Set NgayHen = @due "
            + "Where MaPhieuTiepNhan = @id And TrangThai In (N'Chờ sửa', N'Đang sửa')";
        if (await duLieu.ThucThiAsync(sqlDoiNgayHen, new { id = maBanGhi, due = ngayHenMoi }) != 1)
        {
            throw new InvalidOperationException("Chỉ sửa ngày hẹn của phiếu đang xử lý.");
        }

        return Ok(new { message = "Đã cập nhật ngày hẹn." });
    }

    [HttpPost("jobs/{id}/assign"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> PhanCong(
        [FromRoute(Name = "id")] string maBanGhi,
        YeuCauPhanCong yeuCau
    )
    {
        await duLieu.TrongGiaoDichAsync(async phien =>
        {
            await nghiepVuSuaXe.PhanCong(phien, maBanGhi, yeuCau);
            return true;
        });
        return Ok(new { message = "Đã phân công thợ." });
    }

    [HttpDelete("jobs/{id}/assign/{employee}"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> BoPhanCong(
        [FromRoute(Name = "id")] string maBanGhi,
        [FromRoute(Name = "employee")] string maNhanVienThamChieu
    )
    {
        await duLieu.TrongGiaoDichAsync(async phien =>
        {
            string sqlPhieu =
                "Select MaXe, TrangThai "
                + "From PhieuTiepNhan With (UpdLock, HoldLock) "
                + "Where MaPhieuTiepNhan = @id";
            Dictionary<string, object?> phieu =
                (await phien.DocBangAsync(sqlPhieu, new { id = maBanGhi })).SingleOrDefault()
                ?? throw new KeyNotFoundException("Không tìm thấy phiếu.");
            if (phieu.Chuoi("TrangThai") != "Chờ sửa")
            {
                throw new InvalidOperationException("Chỉ bỏ phân công trước khi bắt đầu sửa.");
            }

            await phien.ThucThiAsync(
                "Delete ChiTietPhanCong Where MaXe = @vehicle And MaNhanVien = @employee",
                new { vehicle = phieu.Chuoi("MaXe"), employee = maNhanVienThamChieu }
            );
            return true;
        });
        return Ok(new { message = "Đã bỏ phân công." });
    }
}
