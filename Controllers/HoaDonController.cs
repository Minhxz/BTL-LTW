using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;

// Người 5: lập hóa đơn và lọc danh sách hóa đơn.
[ApiController, Authorize(Roles = "Manager,Cashier"), Route("api/workshop")]
public sealed class HoaDonController(XuLyDuLieu db) : ControllerBase
{
    // Phần From/Where dùng chung cho đếm, liệt kê và tính tổng hóa đơn.
    private const string BangHoaDon = " From HoaDon h " +
        "Join PhieuTiepNhan p On p.MaPhieuTiepNhan = h.MaPhieuTiepNhan " +
        "Join Xe x On x.MaXe = p.MaXe " +
        "Join KhachHang k On k.MaKhachHang = x.MaKhachHang ";

    private const string DieuKienLoc = " Where (h.MaHoaDon Like @q Or k.TenKhachHang Like @q Or x.BienXe Like @q) " +
        "And (@from Is Null Or h.NgayThanhToan >= @from) " +
        "And (@to Is Null Or h.NgayThanhToan <= @to) ";

    // Thu tiền một phiếu đã hoàn thành. Mỗi phiếu chỉ có một hóa đơn; số tiền lấy từ giá đã chốt.
    [HttpPost("jobs/{id}/invoice"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> LapHoaDon(string id, YeuCauThanhToan input)
    {
        DateTime ngayThanhToan = input.NgayThanhToan.Date;

        string maHoaDon = await db.TrongGiaoDichAsync(async s =>
        {
            // 1. Phiếu phải tồn tại và đã hoàn thành
            string sqlPhieu = "Select NgayLap, TrangThai " +
                "From PhieuTiepNhan With (UpdLock, HoldLock) " +
                "Where MaPhieuTiepNhan = @id";

            Dictionary<string, object?> phieu = (await s.DocBangAsync(sqlPhieu, new { id })).SingleOrDefault()
                ?? throw new KeyNotFoundException("Không tìm thấy phiếu.");

            if (phieu.Chuoi("TrangThai") != "Hoàn thành")
            {
                throw new InvalidOperationException("Chỉ thu tiền khi xe đã hoàn thành.");
            }

            // 2. Ngày thanh toán nằm trong khoảng ngày tiếp nhận → hôm nay
            if (ngayThanhToan > XuLyDuLieu.HomNay || ngayThanhToan < (DateTime)phieu["NgayLap"]!)
            {
                throw new ArgumentException("Ngày thanh toán không được trước ngày tiếp nhận hoặc sau hôm nay.");
            }

            // 3. Chống thu tiền hai lần
            string sqlDemHoaDon = "Select Count(*) " +
                "From HoaDon With (UpdLock, HoldLock) " +
                "Where MaPhieuTiepNhan = @id";

            if (await s.DocGiaTriAsync<int>(sqlDemHoaDon, new { id }) > 0)
            {
                throw new InvalidOperationException("Phiếu đã có hóa đơn; không thu tiền hai lần.");
            }

            // 4. Tổng tiền từ các dịch vụ đã chốt giá trên phiếu
            string sqlTongTien = "Select Sum(ThanhTien) From ChiTietPhieuTiepNhan Where MaPhieuTiepNhan = @id";
            decimal soTien = await s.DocGiaTriAsync<decimal>(sqlTongTien, new { id });

            if (soTien <= 0)
            {
                throw new InvalidOperationException("Phiếu chưa có chi tiết tính tiền hợp lệ.");
            }

            // 5. Lưu hóa đơn
            string maHoaDonMoi = XuLyDuLieu.TaoMaMoi("HD");

            string sqlThemHoaDon = "Insert Into HoaDon (MaHoaDon, MaPhieuTiepNhan, TienThanhToan, NgayLap, NgayThanhToan) " +
                "Values (@code, @id, @amount, @date, @date)";

            await s.ThucThiAsync(sqlThemHoaDon, new { code = maHoaDonMoi, id, amount = soTien, date = ngayThanhToan });

            return maHoaDonMoi;
        });

        return StatusCode(201, new { id = maHoaDon, message = "Đã lập hóa đơn và ghi nhận doanh thu." });
    }

    // Danh sách hóa đơn: tìm theo mã, tên khách, biển số và lọc theo ngày thanh toán.
    [HttpGet("invoices"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> DanhSachHoaDon(string? q = null, string? from = null, string? to = null, int page = 1, int pageSize = 12)
    {
        PhanTrang.KiemTra(page, pageSize, q);
        (DateTime? tuNgay, DateTime? denNgay) = PhanTrang.KhoangNgay(from, to);

        var thamSo = new
        {
            q = "%" + q + "%",
            from = tuNgay,
            to = denNgay,
            skip = (page - 1) * pageSize,
            size = pageSize
        };

        string sqlDem = "Select Count(*)" + BangHoaDon + DieuKienLoc;

        string sqlDanhSach = "Select h.MaHoaDon As id, h.MaPhieuTiepNhan As jobId, k.TenKhachHang As customerName, x.BienXe As licensePlate, " +
            "h.TienThanhToan As amount, h.NgayLap As createdOn, h.NgayThanhToan As paidOn" +
            BangHoaDon + DieuKienLoc +
            "Order By h.NgayThanhToan Desc, h.MaHoaDon Desc " +
            "Offset @skip Rows Fetch Next @size Rows Only";

        string sqlTongTien = "Select Coalesce(Sum(h.TienThanhToan), 0)" + BangHoaDon + DieuKienLoc;

        int tongSoHoaDon = await db.DocGiaTriAsync<int>(sqlDem, thamSo);
        List<Dictionary<string, object?>> danhSachHoaDon = await db.DocBangAsync(sqlDanhSach, thamSo);
        decimal tongTien = await db.DocGiaTriAsync<decimal>(sqlTongTien, thamSo);

        return Ok(new
        {
            items = danhSachHoaDon,
            total = tongSoHoaDon,
            page,
            totalPages = Math.Max(1, (int)Math.Ceiling(tongSoHoaDon / (double)pageSize)),
            totalAmount = tongTien
        });
    }
}