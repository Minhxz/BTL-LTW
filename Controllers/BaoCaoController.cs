using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QLyCuaHangSuaXe.LopDungChung;

namespace QLyCuaHangSuaXe.Controllers;

// Người 5: tổng quan, báo cáo doanh thu và xuất CSV.
[ApiController, Authorize, Route("api/workshop")]
public sealed class BaoCaoController(XuLyDuLieu db) : ControllerBase
{
    // Số liệu trang tổng quan. Mỗi vai trò chỉ thấy phần của mình.
    [HttpGet("dashboard")]
    public async Task<IActionResult> TongQuan()
    {
        string maNhanVien = PhanQuyenNguoiDung.MaNhanVien(User);
        bool laTho = User.IsInRole("Mechanic");

        // 1. Số phiếu theo trạng thái (thợ chỉ đếm xe được phân công cho mình)
        string sqlDemPhieu = "Select TrangThai As status, Count(*) As count " +
            "From PhieuTiepNhan p " +
            "Where @mine = 0 Or Exists (Select 1 From ChiTietPhanCong c Where c.MaXe = p.MaXe And c.MaNhanVien = @employee) " +
            "Group By TrangThai";

        List<Dictionary<string, object?>> soPhieuTheoTrangThai = await db.DocBangAsync(sqlDemPhieu, new { mine = laTho, employee = maNhanVien });

        Dictionary<string, object?> tongQuan = new Dictionary<string, object?>
        {
            ["jobs"] = soPhieuTheoTrangThai,
            ["database"] = new SqlConnectionStringBuilder(db.ChuoiKetNoi).InitialCatalog
        };

        // 2. Quản lý và thu ngân: lịch chờ xác nhận, doanh thu hôm nay
        if (User.IsInRole("Manager") || User.IsInRole("Cashier"))
        {
            tongQuan["pendingBookings"] = await db.DocGiaTriAsync<int>(
                "Select Count(*) From WebLichHen Where TrangThai = N'Chờ xác nhận'");

            tongQuan["todayRevenue"] = await db.DocGiaTriAsync<decimal>(
                "Select Coalesce(Sum(TienThanhToan), 0) From HoaDon Where NgayThanhToan = @today",
                new { today = XuLyDuLieu.HomNay });
        }

        // 3. Quản lý: cảnh báo phụ tùng sắp hết (≤ 5) hoặc sắp hết hạn (30 ngày)
        if (User.IsInRole("Manager"))
        {
            string sqlCanhBaoKho = "Select Top(8) MaPhuTung As id, TenPhuTung As name, SoLuongTon As stock, HanSuDung As expiry " +
                "From PhuTung " +
                "Where TrangThai = 1 And (SoLuongTon <= 5 Or HanSuDung < @soon) " +
                "Order By HanSuDung, SoLuongTon";

            tongQuan["stockAlerts"] = await db.DocBangAsync(sqlCanhBaoKho, new { soon = XuLyDuLieu.HomNay.AddDays(30) });
        }

        return Ok(tongQuan);
    }

    // Báo cáo theo khoảng ngày; mặc định từ đầu tháng đến hôm nay.
    [HttpGet("reports"), Authorize(Roles = "Manager")]
    public async Task<IActionResult> BaoCao(string? from = null, string? to = null)
    {
        (DateTime tuNgay, DateTime denNgay) = LayKhoangBaoCao(from, to);

        if (tuNgay > denNgay)
        {
            throw new ArgumentException("Khoảng ngày không hợp lệ.");
        }

        var thamSo = new { from = tuNgay, to = denNgay };

        // Đã thu: tổng hóa đơn trong kỳ
        decimal daThu = await db.DocGiaTriAsync<decimal>(
            "Select Coalesce(Sum(TienThanhToan), 0) From HoaDon Where NgayThanhToan Between @from And @to", thamSo);

        // Nhập kho: tổng phiếu nhập trong kỳ
        decimal tienNhapKho = await db.DocGiaTriAsync<decimal>(
            "Select Coalesce(Sum(TongTienNhap), 0) From PhieuNhap Where NgayNhap Between @from And @to", thamSo);

        // Chưa thu: phiếu đã hoàn thành nhưng chưa có hóa đơn (không theo kỳ)
        string sqlChuaThu = "Select Coalesce(Sum(c.ThanhTien), 0) " +
            "From PhieuTiepNhan p Join ChiTietPhieuTiepNhan c On c.MaPhieuTiepNhan = p.MaPhieuTiepNhan " +
            "Where p.TrangThai = N'Hoàn thành' " +
            "And Not Exists (Select 1 From HoaDon h Where h.MaPhieuTiepNhan = p.MaPhieuTiepNhan)";

        decimal chuaThu = await db.DocGiaTriAsync<decimal>(sqlChuaThu);

        string sqlTheoNgay = "Select NgayThanhToan As date, Sum(TienThanhToan) As amount, Count(*) As count " +
            "From HoaDon " +
            "Where NgayThanhToan Between @from And @to " +
            "Group By NgayThanhToan " +
            "Order By NgayThanhToan";

        List<Dictionary<string, object?>> doanhThuTheoNgay = await db.DocBangAsync(sqlTheoNgay, thamSo);

        string sqlDichVuNhieuNhat = "Select Top(8) d.TenDichVu As name, Sum(c.SoLuong) As quantity, Sum(c.ThanhTien) As amount " +
            "From ChiTietPhieuTiepNhan c " +
            "Join DichVu d On d.MaDichVu = c.MaDichVu " +
            "Join HoaDon h On h.MaPhieuTiepNhan = c.MaPhieuTiepNhan " +
            "Where h.NgayThanhToan Between @from And @to " +
            "Group By d.TenDichVu " +
            "Order By Sum(c.ThanhTien) Desc";

        List<Dictionary<string, object?>> dichVuNhieuNhat = await db.DocBangAsync(sqlDichVuNhieuNhat, thamSo);

        return Ok(new
        {
            from = tuNgay,
            to = denNgay,
            paid = daThu,
            purchases = tienNhapKho,
            unpaid = chuaThu,
            cashDifference = daThu - tienNhapKho,
            daily = doanhThuTheoNgay,
            services = dichVuNhieuNhat
        });
    }

    // Xuất danh sách hóa đơn trong kỳ ra file CSV (mở được bằng Excel).
    [HttpGet("reports/export"), Authorize(Roles = "Manager")]
    public async Task<IActionResult> XuatCsv(string? from = null, string? to = null)
    {
        (DateTime tuNgay, DateTime denNgay) = LayKhoangBaoCao(from, to);

        string sqlHoaDon = "Select MaHoaDon, MaPhieuTiepNhan, NgayThanhToan, TienThanhToan " +
            "From HoaDon " +
            "Where (@from Is Null Or NgayThanhToan >= @from) And (@to Is Null Or NgayThanhToan <= @to) " +
            "Order By NgayThanhToan";

        List<Dictionary<string, object?>> danhSachHoaDon = await db.DocBangAsync(sqlHoaDon, new { from = tuNgay, to = denNgay });

        StringBuilder noiDung = new StringBuilder("Mã hóa đơn,Mã tiếp nhận,Ngày thanh toán,Số tiền\r\n");

        for (int i = 0; i < danhSachHoaDon.Count; i++)
        {
            if (i > 0)
            {
                noiDung.Append("\r\n");
            }

            noiDung.Append(string.Join(',', danhSachHoaDon[i].Values.Select(TaoOCsv)));
        }

        // Thêm BOM UTF-8 để Excel hiển thị đúng tiếng Việt
        byte[] duLieuFile = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(noiDung.ToString())).ToArray();

        return File(duLieuFile, "text/csv; charset=utf-8", "doanh-thu.csv");
    }

    // Bỏ trống ngày thì lấy từ đầu tháng đến hôm nay, để báo cáo và file CSV thống nhất.
    private static (DateTime TuNgay, DateTime DenNgay) LayKhoangBaoCao(string? from, string? to)
    {
        (DateTime? tuNgay, DateTime? denNgay) = PhanTrang.KhoangNgay(from, to);
        DateTime dauThang = new DateTime(XuLyDuLieu.HomNay.Year, XuLyDuLieu.HomNay.Month, 1);

        return (tuNgay ?? dauThang, denNgay ?? XuLyDuLieu.HomNay);
    }

    // Một ô CSV: bọc nháy kép; thêm ' trước =, +, -, @ để Excel không chạy như công thức.
    private static string TaoOCsv(object? giaTri)
    {
        string chuoi = giaTri is DateTime ngay
            ? ngay.ToString("yyyy-MM-dd")
            : Convert.ToString(giaTri, CultureInfo.InvariantCulture) ?? "";

        if (chuoi.Length > 0 && "=+-@".Contains(chuoi[0]))
        {
            chuoi = "'" + chuoi;
        }

        return "\"" + chuoi.Replace("\"", "\"\"") + "\"";
    }
}
