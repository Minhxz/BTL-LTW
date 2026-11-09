using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QLyCuaHangSuaXe.LopDungChung;

namespace QLyCuaHangSuaXe.Controllers;

[ApiController, Authorize, Route("api/workshop")]
public sealed class BaoCaoController(XuLyDuLieu duLieu) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> TongQuan()
    {
        string maNhanVien = PhanQuyenNguoiDung.MaNhanVien(User);
        bool laTho = User.IsInRole("Mechanic");
        string sqlDemPhieu =
            "Select TrangThai As status, Count(*) As count "
            + "From PhieuTiepNhan p "
            + "Where @mine = 0 Or Exists (Select 1 From ChiTietPhanCong c Where c.MaXe = p.MaXe And c.MaNhanVien = @employee) "
            + "Group By TrangThai";
        List<Dictionary<string, object?>> soPhieuTheoTrangThai = await duLieu.DocBangAsync(
            sqlDemPhieu,
            new { mine = laTho, employee = maNhanVien }
        );
        Dictionary<string, object?> tongQuan = new Dictionary<string, object?>
        {
            ["jobs"] = soPhieuTheoTrangThai,
            ["database"] = new SqlConnectionStringBuilder(duLieu.ChuoiKetNoi).InitialCatalog,
        };
        if (User.IsInRole("Manager") || User.IsInRole("Cashier"))
        {
            tongQuan["pendingBookings"] = await duLieu.DocGiaTriAsync<int>(
                "Select Count(*) From WebLichHen Where TrangThai = N'Chờ xác nhận'"
            );
            tongQuan["todayRevenue"] = await duLieu.DocGiaTriAsync<decimal>(
                "Select Coalesce(Sum(TienThanhToan), 0) From HoaDon Where NgayThanhToan = @today",
                new { today = XuLyDuLieu.HomNay }
            );
        }

        if (User.IsInRole("Manager"))
        {
            string sqlCanhBaoKho =
                "Select Top(8) MaPhuTung As id, TenPhuTung As name, SoLuongTon As stock, HanSuDung As expiry "
                + "From PhuTung "
                + "Where TrangThai = 1 And (SoLuongTon <= 5 Or HanSuDung < @soon) "
                + "Order By HanSuDung, SoLuongTon";
            tongQuan["stockAlerts"] = await duLieu.DocBangAsync(
                sqlCanhBaoKho,
                new { soon = XuLyDuLieu.HomNay.AddDays(30) }
            );
        }

        return Ok(tongQuan);
    }

    [HttpGet("reports"), Authorize(Roles = "Manager")]
    public async Task<IActionResult> BaoCao(
        [FromQuery(Name = "from")] string? ngayBatDau = null,
        [FromQuery(Name = "to")] string? ngayKetThuc = null
    )
    {
        (DateTime tuNgay, DateTime denNgay) = LayKhoangBaoCao(ngayBatDau, ngayKetThuc);
        if (tuNgay > denNgay)
        {
            throw new ArgumentException("Khoảng ngày không hợp lệ.");
        }

        var thamSo = new { from = tuNgay, to = denNgay };
        decimal daThu = await duLieu.DocGiaTriAsync<decimal>(
            "Select Coalesce(Sum(TienThanhToan), 0) From HoaDon Where NgayThanhToan Between @from And @to",
            thamSo
        );
        decimal tienNhapKho = await duLieu.DocGiaTriAsync<decimal>(
            "Select Coalesce(Sum(TongTienNhap), 0) From PhieuNhap Where NgayNhap Between @from And @to",
            thamSo
        );
        string sqlChuaThu =
            "Select Coalesce(Sum(c.ThanhTien), 0) "
            + "From PhieuTiepNhan p Join ChiTietPhieuTiepNhan c On c.MaPhieuTiepNhan = p.MaPhieuTiepNhan "
            + "Where p.TrangThai = N'Hoàn thành' "
            + "And Not Exists (Select 1 From HoaDon h Where h.MaPhieuTiepNhan = p.MaPhieuTiepNhan)";
        decimal chuaThu = await duLieu.DocGiaTriAsync<decimal>(sqlChuaThu);
        string sqlTheoNgay =
            "Select NgayThanhToan As date, Sum(TienThanhToan) As amount, Count(*) As count "
            + "From HoaDon "
            + "Where NgayThanhToan Between @from And @to "
            + "Group By NgayThanhToan "
            + "Order By NgayThanhToan";
        List<Dictionary<string, object?>> doanhThuTheoNgay = await duLieu.DocBangAsync(
            sqlTheoNgay,
            thamSo
        );
        string sqlDichVuNhieuNhat =
            "Select Top(8) d.TenDichVu As name, Sum(c.SoLuong) As quantity, Sum(c.ThanhTien) As amount "
            + "From ChiTietPhieuTiepNhan c "
            + "Join DichVu d On d.MaDichVu = c.MaDichVu "
            + "Join HoaDon h On h.MaPhieuTiepNhan = c.MaPhieuTiepNhan "
            + "Where h.NgayThanhToan Between @from And @to "
            + "Group By d.TenDichVu "
            + "Order By Sum(c.ThanhTien) Desc";
        List<Dictionary<string, object?>> dichVuNhieuNhat = await duLieu.DocBangAsync(
            sqlDichVuNhieuNhat,
            thamSo
        );
        return Ok(
            new
            {
                from = tuNgay,
                to = denNgay,
                paid = daThu,
                purchases = tienNhapKho,
                unpaid = chuaThu,
                cashDifference = daThu - tienNhapKho,
                daily = doanhThuTheoNgay,
                services = dichVuNhieuNhat,
            }
        );
    }

    [HttpGet("reports/export"), Authorize(Roles = "Manager")]
    public async Task<IActionResult> XuatCsv(
        [FromQuery(Name = "from")] string? ngayBatDau = null,
        [FromQuery(Name = "to")] string? ngayKetThuc = null
    )
    {
        (DateTime tuNgay, DateTime denNgay) = LayKhoangBaoCao(ngayBatDau, ngayKetThuc);
        string sqlHoaDon =
            "Select MaHoaDon, MaPhieuTiepNhan, NgayThanhToan, TienThanhToan "
            + "From HoaDon "
            + "Where (@from Is Null Or NgayThanhToan >= @from) And (@to Is Null Or NgayThanhToan <= @to) "
            + "Order By NgayThanhToan";
        List<Dictionary<string, object?>> danhSachHoaDon = await duLieu.DocBangAsync(
            sqlHoaDon,
            new { from = tuNgay, to = denNgay }
        );
        StringBuilder noiDung = new StringBuilder(
            "Mã hóa đơn,Mã tiếp nhận,Ngày thanh toán,Số tiền\r\n"
        );
        for (int chiSo = 0; chiSo < danhSachHoaDon.Count; chiSo++)
        {
            if (chiSo > 0)
            {
                noiDung.Append("\r\n");
            }

            noiDung.Append(string.Join(',', danhSachHoaDon[chiSo].Values.Select(TaoOCsv)));
        }

        byte[] duLieuFile = Encoding
            .UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(noiDung.ToString()))
            .ToArray();
        return File(duLieuFile, "text/csv; charset=utf-8", "doanh-thu.csv");
    }

    private static (DateTime TuNgay, DateTime DenNgay) LayKhoangBaoCao(
        string? ngayBatDau,
        string? ngayKetThuc
    )
    {
        (DateTime? tuNgay, DateTime? denNgay) = PhanTrang.KhoangNgay(ngayBatDau, ngayKetThuc);
        DateTime dauThang = new DateTime(XuLyDuLieu.HomNay.Year, XuLyDuLieu.HomNay.Month, 1);
        return (tuNgay ?? dauThang, denNgay ?? XuLyDuLieu.HomNay);
    }

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
