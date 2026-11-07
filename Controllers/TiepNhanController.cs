using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;

// Người 3: phiếu sửa, tiến độ, ngày hẹn và phân công thợ.
[ApiController, Authorize(Roles = "Manager,Cashier,Mechanic"), Route("api/workshop")]
public sealed class TiepNhanController(XuLyDuLieu db, NghiepVuSuaXe workflow) : ControllerBase
{
    private string DieuKienPhanCong => User.IsInRole("Mechanic") ? " AND EXISTS(SELECT 1 FROM ChiTietPhanCong a WHERE a.MaXe=p.MaXe AND a.MaNhanVien=@employee)" : "";

    [HttpGet("jobs")]
    public async Task<IActionResult> DanhSach(string? q = null, string? status = null, string? from = null, string? to = null, int page = 1, int pageSize = 12)
    {
        PhanTrang.KiemTra(page, pageSize, q);
        var dates = PhanTrang.KhoangNgay(from, to);
        var where = " WHERE (@q='' OR p.MaPhieuTiepNhan LIKE @term OR k.TenKhachHang LIKE @term OR k.SoDienThoai LIKE @term OR x.BienXe LIKE @term) AND (@status='' OR p.TrangThai=@status) AND (@from IS NULL OR p.NgayLap>=@from) AND (@to IS NULL OR p.NgayLap<=@to)" + DieuKienPhanCong;
        var args = new
        {
            q = q ?? "",
            term = "%" + q + "%",
            status = status ?? "",
            from = dates.From,
            to = dates.To,
            employee = PhanQuyenNguoiDung.MaNhanVien(User),
            skip = (page - 1) * pageSize,
            size = pageSize
        };
        var total = await db.DocGiaTriAsync<int>("SELECT COUNT(*) FROM (" + NghiepVuSuaXe.TruyVanPhieu + where + ") z", args);
        var items = await db.DocBangAsync(NghiepVuSuaXe.TruyVanPhieu + where + " ORDER BY p.NgayLap DESC,p.MaPhieuTiepNhan DESC OFFSET @skip ROWS FETCH NEXT @size ROWS ONLY", args);
        return Ok(new { items, total, page, totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)) });
    }

    [HttpGet("jobs/{id}")]
    public async Task<IActionResult> ChiTiet(string id)
    {
        var job = (await db.DocBangAsync(NghiepVuSuaXe.TruyVanPhieu + " WHERE p.MaPhieuTiepNhan=@id" + DieuKienPhanCong, new { id, employee = PhanQuyenNguoiDung.MaNhanVien(User) })).SingleOrDefault() ?? throw new KeyNotFoundException("Không tìm thấy phiếu hoặc bạn chưa được phân công.");
        var lines = await db.DocBangAsync("SELECT c.MaDichVu serviceId,d.TenDichVu name,c.SoLuong quantity,c.ThanhTien amount FROM ChiTietPhieuTiepNhan c JOIN DichVu d ON d.MaDichVu=c.MaDichVu WHERE c.MaPhieuTiepNhan=@id", new { id });
        var assignments = await db.DocBangAsync("SELECT c.MaNhanVien employeeId,n.HoTen name,c.ThoiGianBatDau start,c.ThoiGianHoanThanh [end] FROM ChiTietPhanCong c JOIN NhanVien n ON n.MaNhanVien=c.MaNhanVien WHERE c.MaXe=@vehicle", new { vehicle = job["vehicleId"] });
        var invoice = User.IsInRole("Mechanic") ? null : (await db.DocBangAsync("SELECT MaHoaDon id,TienThanhToan amount,NgayLap createdOn,NgayThanhToan paidOn FROM HoaDon WHERE MaPhieuTiepNhan=@id", new { id })).SingleOrDefault();
        return Ok(new { job, lines, assignments, invoice });
    }

    [HttpPost("jobs/preview"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> XemTruocPhieu(YeuCauXemTruocPhieu input) => Ok(await workflow.XemTruocPhieu(input));

    [HttpPost("jobs"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> TaoPhieu(ThongTinTiepNhan input) => StatusCode(201, new { id = await workflow.TaoPhieuAsync(input, PhanQuyenNguoiDung.MaNhanVien(User)), message = "Đã tiếp nhận xe và cập nhật kho." });
    [HttpPut("jobs/{id}/status")]
    public async Task<IActionResult> DoiTrangThai(string id, YeuCauTrangThai input)
    {
        await workflow.DoiTrangThaiAsync(id, input.TrangThai, User);
        return Ok(new { message = "Đã cập nhật tiến độ." });
    }

    [HttpPut("jobs/{id}/due-date"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> DoiNgayHen(string id, YeuCauNgayHen input)
    {
        if (input.NgayHenTra.Date < XuLyDuLieu.HomNay || input.NgayHenTra.Date > XuLyDuLieu.HomNay.AddDays(90))
            throw new ArgumentException("Ngày hẹn phải trong 90 ngày tới.");
        if (await db.ThucThiAsync("UPDATE PhieuTiepNhan SET NgayHen=@due WHERE MaPhieuTiepNhan=@id AND TrangThai IN(N'Chờ sửa',N'Đang sửa')", new { id, due = input.NgayHenTra.Date }) != 1)
            throw new InvalidOperationException("Chỉ sửa ngày hẹn của phiếu đang xử lý.");
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

    [HttpDelete("jobs/{id}/assign/{employee}"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> BoPhanCong(string id, string employee)
    {
        await db.TrongGiaoDichAsync(async s =>
        {
            var job = (await s.DocBangAsync("SELECT MaXe,TrangThai FROM PhieuTiepNhan WITH(UPDLOCK,HOLDLOCK) WHERE MaPhieuTiepNhan=@id", new { id })).SingleOrDefault() ?? throw new KeyNotFoundException("Không tìm thấy phiếu.");
            if (job.Chuoi("TrangThai") != "Chờ sửa")
                throw new InvalidOperationException("Chỉ bỏ phân công trước khi bắt đầu sửa.");
            await s.ThucThiAsync("DELETE ChiTietPhanCong WHERE MaXe=@vehicle AND MaNhanVien=@employee", new { vehicle = job.Chuoi("MaXe"), employee });
            return true;
        });
        return Ok(new { message = "Đã bỏ phân công." });
    }
}
