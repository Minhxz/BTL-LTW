using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;

[ApiController, Authorize(Roles = "Manager,Cashier"), Route("api/workshop")]
public sealed class HoaDonController(XuLyDuLieu duLieu) : ControllerBase
{
    private const string BangHoaDon =
        " From HoaDon h "
        + "Join PhieuTiepNhan p On p.MaPhieuTiepNhan = h.MaPhieuTiepNhan "
        + "Join Xe x On x.MaXe = p.MaXe "
        + "Join KhachHang k On k.MaKhachHang = x.MaKhachHang ";
    private const string DieuKienLoc =
        " Where (h.MaHoaDon Like @q Or k.TenKhachHang Like @q Or x.BienXe Like @q) "
        + "And (@from Is Null Or h.NgayThanhToan >= @from) "
        + "And (@to Is Null Or h.NgayThanhToan <= @to) ";

    [HttpPost("jobs/{id}/invoice"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> LapHoaDon(
        [FromRoute(Name = "id")] string maBanGhi,
        YeuCauThanhToan yeuCau
    )
    {
        DateTime ngayThanhToan = yeuCau.NgayThanhToan.Date;
        string maHoaDon = await duLieu.TrongGiaoDichAsync(async phien =>
        {
            string sqlPhieu =
                "Select NgayLap, TrangThai "
                + "From PhieuTiepNhan With (UpdLock, HoldLock) "
                + "Where MaPhieuTiepNhan = @id";
            Dictionary<string, object?> phieu =
                (await phien.DocBangAsync(sqlPhieu, new { id = maBanGhi })).SingleOrDefault()
                ?? throw new KeyNotFoundException("Không tìm thấy phiếu.");
            if (phieu.Chuoi("TrangThai") != "Hoàn thành")
            {
                throw new InvalidOperationException("Chỉ thu tiền khi xe đã hoàn thành.");
            }

            if (ngayThanhToan > XuLyDuLieu.HomNay || ngayThanhToan < (DateTime)phieu["NgayLap"]!)
            {
                throw new ArgumentException(
                    "Ngày thanh toán không được trước ngày tiếp nhận hoặc sau hôm nay."
                );
            }

            string sqlDemHoaDon =
                "Select Count(*) "
                + "From HoaDon With (UpdLock, HoldLock) "
                + "Where MaPhieuTiepNhan = @id";
            if (await phien.DocGiaTriAsync<int>(sqlDemHoaDon, new { id = maBanGhi }) > 0)
            {
                throw new InvalidOperationException("Phiếu đã có hóa đơn; không thu tiền hai lần.");
            }

            string sqlTongTien =
                "Select Sum(ThanhTien) From ChiTietPhieuTiepNhan Where MaPhieuTiepNhan = @id";
            decimal soTien = await phien.DocGiaTriAsync<decimal>(
                sqlTongTien,
                new { id = maBanGhi }
            );
            if (soTien <= 0)
            {
                throw new InvalidOperationException("Phiếu chưa có chi tiết tính tiền hợp lệ.");
            }

            string maHoaDonMoi = XuLyDuLieu.TaoMaMoi("HD");
            string sqlThemHoaDon =
                "Insert Into HoaDon (MaHoaDon, MaPhieuTiepNhan, TienThanhToan, NgayLap, NgayThanhToan) "
                + "Values (@code, @id, @amount, @date, @date)";
            await phien.ThucThiAsync(
                sqlThemHoaDon,
                new
                {
                    code = maHoaDonMoi,
                    id = maBanGhi,
                    amount = soTien,
                    date = ngayThanhToan,
                }
            );
            return maHoaDonMoi;
        });
        return StatusCode(
            201,
            new { id = maHoaDon, message = "Đã lập hóa đơn và ghi nhận doanh thu." }
        );
    }

    [HttpGet("invoices"), Authorize(Roles = "Manager,Cashier")]
    public async Task<IActionResult> DanhSachHoaDon(
        [FromQuery(Name = "q")] string? tuKhoa = null,
        [FromQuery(Name = "from")] string? ngayBatDau = null,
        [FromQuery(Name = "to")] string? ngayKetThuc = null,
        [FromQuery(Name = "page")] int trang = 1,
        [FromQuery(Name = "pageSize")] int kichThuocTrang = 12
    )
    {
        PhanTrang.KiemTra(trang, kichThuocTrang, tuKhoa);
        (DateTime? tuNgay, DateTime? denNgay) = PhanTrang.KhoangNgay(ngayBatDau, ngayKetThuc);
        var thamSo = new
        {
            q = "%" + tuKhoa + "%",
            from = tuNgay,
            to = denNgay,
            skip = (trang - 1) * kichThuocTrang,
            size = kichThuocTrang,
        };
        string sqlDem = "Select Count(*)" + BangHoaDon + DieuKienLoc;
        string sqlDanhSach =
            "Select h.MaHoaDon As id, h.MaPhieuTiepNhan As jobId, k.TenKhachHang As customerName, x.BienXe As licensePlate, "
            + "h.TienThanhToan As amount, h.NgayLap As createdOn, h.NgayThanhToan As paidOn"
            + BangHoaDon
            + DieuKienLoc
            + "Order By h.NgayThanhToan Desc, h.MaHoaDon Desc "
            + "Offset @skip Rows Fetch Next @size Rows Only";
        string sqlTongTien = "Select Coalesce(Sum(h.TienThanhToan), 0)" + BangHoaDon + DieuKienLoc;
        int tongSoHoaDon = await duLieu.DocGiaTriAsync<int>(sqlDem, thamSo);
        List<Dictionary<string, object?>> danhSachHoaDon = await duLieu.DocBangAsync(
            sqlDanhSach,
            thamSo
        );
        decimal tongTien = await duLieu.DocGiaTriAsync<decimal>(sqlTongTien, thamSo);
        return Ok(
            new
            {
                items = danhSachHoaDon,
                total = tongSoHoaDon,
                page = trang,
                totalPages = Math.Max(1, (int)Math.Ceiling(tongSoHoaDon / (double)kichThuocTrang)),
                totalAmount = tongTien,
            }
        );
    }
}
