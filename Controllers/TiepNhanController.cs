using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;

// Người 3: phiếu sửa, tiến độ, ngày hẹn và phân công thợ.
[ApiController, Authorize(Roles = "Manager,Cashier,Mechanic"), Route("api/workshop")]
public sealed class TiepNhanController(XuLyDuLieu db, NghiepVuSuaXe workflow) : ControllerBase
{
    // Thợ chỉ thấy phiếu của xe được phân công cho mình.
    private string DieuKienPhanCong => User.IsInRole("Mechanic")
        ? " And Exists (Select 1 From ChiTietPhanCong a Where a.MaXe = p.MaXe And a.MaNhanVien = @employee)"
        : "";

    // Danh sách phiếu: tìm theo mã, khách, SĐT, biển số; lọc trạng thái và ngày lập.
    [HttpGet("jobs")]
    public async Task<IActionResult> DanhSach(string? q = null, string? status = null, string? from = null, string? to = null, int page = 1, int pageSize = 12)
    {
        PhanTrang.KiemTra(page, pageSize, q);
        (DateTime? tuNgay, DateTime? denNgay) = PhanTrang.KhoangNgay(from, to);

        string dieuKienLoc = " Where (@q = '' Or p.MaPhieuTiepNhan Like @term Or k.TenKhachHang Like @term Or k.SoDienThoai Like @term Or x.BienXe Like @term) " +
            "And (@status = '' Or p.TrangThai = @status) " +
            "And (@from Is Null Or p.NgayLap >= @from) " +
            "And (@to Is Null Or p.NgayLap <= @to)" +
            DieuKienPhanCong;

        var thamSo = new
        {
            q = q ?? "",
            term = "%" + q + "%",
            status = status ?? "",
            from = tuNgay,
            to = denNgay,
            employee = PhanQuyenNguoiDung.MaNhanVien(User),
            skip = (page - 1) * pageSize,
            size = pageSize
        };

        string sqlDem = "Select Count(*) From (" + NghiepVuSuaXe.TruyVanPhieu + dieuKienLoc + ") z";

        string sqlDanhSach = NghiepVuSuaXe.TruyVanPhieu + dieuKienLoc +
            " Order By p.NgayLap Desc, p.MaPhieuTiepNhan Desc " +
            "Offset @skip Rows Fetch Next @size Rows Only";

        int tongSoPhieu = await db.DocGiaTriAsync<int>(sqlDem, thamSo);
        List<Dictionary<string, object?>> danhSachPhieu = await db.DocBangAsync(sqlDanhSach, thamSo);

        return Ok(new
        {
            items = danhSachPhieu,
            total = tongSoPhieu,
            page,
            totalPages = Math.Max(1, (int)Math.Ceiling(tongSoPhieu / (double)pageSize))
        });
    }

    // Chi tiết phiếu: thông tin xe/khách, dịch vụ, thợ được phân công và hóa đơn (thợ không xem hóa đơn).
    [HttpGet("jobs/{id}")]
    public async Task<IActionResult> ChiTiet(string id)
    {
        string sqlPhieu = NghiepVuSuaXe.TruyVanPhieu + " Where p.MaPhieuTiepNhan = @id" + DieuKienPhanCong;

        Dictionary<string, object?> phieu = (await db.DocBangAsync(sqlPhieu, new { id, employee = PhanQuyenNguoiDung.MaNhanVien(User) })).SingleOrDefault()
            ?? throw new KeyNotFoundException("Không tìm thấy phiếu hoặc bạn chưa được phân công.");

        string sqlDichVu = "Select c.MaDichVu As serviceId, d.TenDichVu As name, c.SoLuong As quantity, c.ThanhTien As amount " +
            "From ChiTietPhieuTiepNhan c Join DichVu d On d.MaDichVu = c.MaDichVu " +
            "Where c.MaPhieuTiepNhan = @id";

        List<Dictionary<string, object?>> danhSachDichVu = await db.DocBangAsync(sqlDichVu, new { id });

        string sqlPhanCong = "Select c.MaNhanVien As employeeId, n.HoTen As name, c.ThoiGianBatDau As start, c.ThoiGianHoanThanh As [end] " +
            "From ChiTietPhanCong c Join NhanVien n On n.MaNhanVien = c.MaNhanVien " +
            "Where c.MaXe = @vehicle";

        List<Dictionary<string, object?>> danhSachPhanCong = await db.DocBangAsync(sqlPhanCong, new { vehicle = phieu["vehicleId"] });

        Dictionary<string, object?>? hoaDon = null;

        if (!User.IsInRole("Mechanic"))
        {
            string sqlHoaDon = "Select MaHoaDon As id, TienThanhToan As amount, NgayLap As createdOn, NgayThanhToan As paidOn " +
                "From HoaDon " +
                "Where MaPhieuTiepNhan = @id";

            hoaDon = (await db.DocBangAsync(sqlHoaDon, new { id })).SingleOrDefault();
        }

        return Ok(new { job = phieu, lines = danhSachDichVu, assignments = danhSachPhanCong, invoice = hoaDon });
    }

    [HttpPost("jobs/preview"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> XemTruocPhieu(YeuCauXemTruocPhieu input)
    {
        return Ok(await workflow.XemTruocPhieu(input));
    }

    [HttpPost("jobs"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> TaoPhieu(ThongTinTiepNhan input)
    {
        string maPhieu = await workflow.TaoPhieuAsync(input, PhanQuyenNguoiDung.MaNhanVien(User));

        return StatusCode(201, new { id = maPhieu, message = "Đã tiếp nhận xe và cập nhật kho." });
    }

    [HttpPut("jobs/{id}/status")]
    public async Task<IActionResult> DoiTrangThai(string id, YeuCauTrangThai input)
    {
        await workflow.DoiTrangThaiAsync(id, input.TrangThai, User);

        return Ok(new { message = "Đã cập nhật tiến độ." });
    }

    // Đổi ngày hẹn trả: chỉ với phiếu đang xử lý, trong 90 ngày tới.
    [HttpPut("jobs/{id}/due-date"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> DoiNgayHen(string id, YeuCauNgayHen input)
    {
        DateTime ngayHenMoi = input.NgayHenTra.Date;

        if (ngayHenMoi < XuLyDuLieu.HomNay || ngayHenMoi > XuLyDuLieu.HomNay.AddDays(90))
        {
            throw new ArgumentException("Ngày hẹn phải trong 90 ngày tới.");
        }

        string sqlDoiNgayHen = "Update PhieuTiepNhan " +
            "Set NgayHen = @due " +
            "Where MaPhieuTiepNhan = @id And TrangThai In (N'Chờ sửa', N'Đang sửa')";

        if (await db.ThucThiAsync(sqlDoiNgayHen, new { id, due = ngayHenMoi }) != 1)
        {
            throw new InvalidOperationException("Chỉ sửa ngày hẹn của phiếu đang xử lý.");
        }

        return Ok(new { message = "Đã cập nhật ngày hẹn." });
    }

    [HttpPost("jobs/{id}/assign"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> PhanCong(string id, YeuCauPhanCong input)
    {
        await db.TrongGiaoDichAsync(async s =>
        {
            await workflow.PhanCong(s, id, input);
            return true;
        });

        return Ok(new { message = "Đã phân công thợ." });
    }

    // Bỏ phân công: chỉ khi phiếu còn Chờ sửa.
    [HttpDelete("jobs/{id}/assign/{employee}"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> BoPhanCong(string id, string employee)
    {
        await db.TrongGiaoDichAsync(async s =>
        {
            string sqlPhieu = "Select MaXe, TrangThai " +
                "From PhieuTiepNhan With (UpdLock, HoldLock) " +
                "Where MaPhieuTiepNhan = @id";

            Dictionary<string, object?> phieu = (await s.DocBangAsync(sqlPhieu, new { id })).SingleOrDefault()
                ?? throw new KeyNotFoundException("Không tìm thấy phiếu.");

            if (phieu.Chuoi("TrangThai") != "Chờ sửa")
            {
                throw new InvalidOperationException("Chỉ bỏ phân công trước khi bắt đầu sửa.");
            }

            await s.ThucThiAsync("Delete ChiTietPhanCong Where MaXe = @vehicle And MaNhanVien = @employee",
                new { vehicle = phieu.Chuoi("MaXe"), employee });

            return true;
        });

        return Ok(new { message = "Đã bỏ phân công." });
    }
}
