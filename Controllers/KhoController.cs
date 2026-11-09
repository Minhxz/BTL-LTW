using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;

[ApiController, Authorize(Roles = "Manager"), Route("api/workshop/stock")]
public sealed class KhoController(XuLyDuLieu duLieu) : ControllerBase
{
    private const int TonKhoToiDa = 1000000;

    [HttpGet("purchases")]
    public async Task<IActionResult> DanhSach(
        [FromQuery(Name = "q")] string? tuKhoa = null,
        [FromQuery(Name = "page")] int trang = 1,
        [FromQuery(Name = "pageSize")] int kichThuocTrang = 12
    )
    {
        PhanTrang.KiemTra(trang, kichThuocTrang, tuKhoa);
        var thamSo = new
        {
            TuKhoa = "%" + (tuKhoa ?? "").Trim() + "%",
            BoQua = (trang - 1) * kichThuocTrang,
            SoDong = kichThuocTrang,
        };
        string sqlDemPhieu =
            "Select Count(*) "
            + "From PhieuNhap "
            + "Where MaPhieuNhap Like @TuKhoa Or GhiChu Like @TuKhoa";
        string sqlDanhSachPhieu =
            "Select pn.MaPhieuNhap As id, pn.NgayNhap As date, nv.HoTen As employee, pn.GhiChu As note, pn.TongTienNhap As amount "
            + "From PhieuNhap pn Join NhanVien nv On nv.MaNhanVien = pn.MaNhanVienNhap "
            + "Where pn.MaPhieuNhap Like @TuKhoa Or pn.GhiChu Like @TuKhoa "
            + "Order By pn.NgayNhap Desc, pn.MaPhieuNhap Desc "
            + "Offset @BoQua Rows Fetch Next @SoDong Rows Only";
        int tongSoPhieu = await duLieu.DocGiaTriAsync<int>(sqlDemPhieu, thamSo);
        List<Dictionary<string, object?>> danhSachPhieu = await duLieu.DocBangAsync(
            sqlDanhSachPhieu,
            thamSo
        );
        return Ok(
            new
            {
                items = danhSachPhieu,
                total = tongSoPhieu,
                page = trang,
                totalPages = TinhSoTrang(tongSoPhieu, kichThuocTrang),
            }
        );
    }

    [HttpGet("purchases/{maPhieuNhap}")]
    public async Task<IActionResult> ChiTiet(string maPhieuNhap)
    {
        string sqlKiemTraPhieu =
            "Select Count(*) " + "From PhieuNhap " + "Where MaPhieuNhap = @MaPhieuNhap";
        int soPhieu = await duLieu.DocGiaTriAsync<int>(
            sqlKiemTraPhieu,
            new { MaPhieuNhap = maPhieuNhap }
        );
        if (soPhieu == 0)
        {
            throw new KeyNotFoundException("Không tìm thấy phiếu nhập.");
        }

        string sqlChiTietPhieu =
            "Select ct.MaPhuTung As partId, pt.TenPhuTung As name, ct.SoLuongNhap As quantity, "
            + "ct.ThanhTien / ct.SoLuongNhap As price, ct.ThanhTien As amount "
            + "From ChiTietPhieuNhap ct Join PhuTung pt On pt.MaPhuTung = ct.MaPhuTung "
            + "Where ct.MaPhieuNhap = @MaPhieuNhap "
            + "Order By ct.MaPhuTung";
        List<Dictionary<string, object?>> chiTietPhieu = await duLieu.DocBangAsync(
            sqlChiTietPhieu,
            new { MaPhieuNhap = maPhieuNhap }
        );
        return Ok(new { lines = chiTietPhieu });
    }

    [HttpPost("purchases")]
    public async Task<IActionResult> NhapKho(ThongTinPhieuNhap yeuCau)
    {
        KiemTraPhieuNhap(yeuCau);
        string maNhanVien = PhanQuyenNguoiDung.MaNhanVien(User);
        DateTime ngayNhap = yeuCau.Ngay.Date;
        string ghiChu = yeuCau.GhiChu?.Trim() ?? "";
        string maPhieuNhap = await duLieu.TrongGiaoDichAsync(async phien =>
        {
            string maPhieuMoi = XuLyDuLieu.TaoMaMoi("PN");
            decimal tongTienNhap = 0;
            string sqlThemPhieu =
                "Insert Into PhieuNhap (MaPhieuNhap, MaNhanVienNhap, NgayNhap, GhiChu, TongTienNhap) "
                + "Values (@MaPhieuNhap, @MaNhanVien, @NgayNhap, @GhiChu, 0)";
            await phien.ThucThiAsync(
                sqlThemPhieu,
                new
                {
                    MaPhieuNhap = maPhieuMoi,
                    MaNhanVien = maNhanVien,
                    NgayNhap = ngayNhap,
                    GhiChu = ghiChu,
                }
            );
            List<PhuTungNhap> danhSachNhap = yeuCau
                .ChiTiet.OrderBy(dong => dong.MaPhuTung.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (PhuTungNhap dong in danhSachNhap)
            {
                Dictionary<string, object?> phuTung = await DocVaKhoaPhuTung(phien, dong.MaPhuTung);
                string maPhuTung = phuTung.Chuoi("MaPhuTung");
                bool dangHoatDong = Convert.ToBoolean(phuTung["TrangThai"]);
                DateTime hanSuDung = Convert.ToDateTime(phuTung["HanSuDung"]);
                if (!dangHoatDong)
                {
                    throw new InvalidOperationException(
                        $"Phụ tùng {maPhuTung} đã ngừng sử dụng, không nhập thêm."
                    );
                }

                if (hanSuDung < XuLyDuLieu.HomNay)
                {
                    throw new InvalidOperationException(
                        $"Phụ tùng {maPhuTung} đã hết hạn sử dụng, không nhận nhập kho."
                    );
                }

                decimal donGiaNhap = phuTung.SoTien("DonGiaNhap");
                decimal thanhTien = donGiaNhap * dong.SoLuong;
                tongTienNhap += thanhTien;
                string sqlCongTon =
                    "Update PhuTung "
                    + "Set SoLuongTon = SoLuongTon + @SoLuong "
                    + "Where MaPhuTung = @MaPhuTung And SoLuongTon + @SoLuong <= @TonKhoToiDa";
                int soDongCapNhat = await phien.ThucThiAsync(
                    sqlCongTon,
                    new
                    {
                        MaPhuTung = maPhuTung,
                        SoLuong = dong.SoLuong,
                        TonKhoToiDa,
                    }
                );
                if (soDongCapNhat != 1)
                {
                    throw new InvalidOperationException(
                        $"Tồn kho phụ tùng {maPhuTung} vượt mức {TonKhoToiDa:N0}."
                    );
                }

                string sqlThemChiTiet =
                    "Insert Into ChiTietPhieuNhap (MaPhieuNhap, MaPhuTung, SoLuongNhap, ThanhTien) "
                    + "Values (@MaPhieuNhap, @MaPhuTung, @SoLuong, @ThanhTien)";
                await phien.ThucThiAsync(
                    sqlThemChiTiet,
                    new
                    {
                        MaPhieuNhap = maPhieuMoi,
                        MaPhuTung = maPhuTung,
                        SoLuong = dong.SoLuong,
                        ThanhTien = thanhTien,
                    }
                );
            }

            string sqlCapNhatTongTien =
                "Update PhieuNhap "
                + "Set TongTienNhap = @TongTienNhap "
                + "Where MaPhieuNhap = @MaPhieuNhap";
            await phien.ThucThiAsync(
                sqlCapNhatTongTien,
                new { MaPhieuNhap = maPhieuMoi, TongTienNhap = tongTienNhap }
            );
            return maPhieuMoi;
        });
        return StatusCode(
            201,
            new { id = maPhieuNhap, message = "Đã lưu phiếu nhập và cộng kho." }
        );
    }

    [HttpPost("adjust")]
    public async Task<IActionResult> DieuChinhTon(YeuCauDieuChinhTon yeuCau)
    {
        string lyDo = yeuCau.LyDo.Trim();
        if (lyDo.Length < 5)
        {
            throw new ArgumentException("Nhập lý do điều chỉnh tối thiểu 5 ký tự.");
        }

        string maNhanVien = PhanQuyenNguoiDung.MaNhanVien(User);
        int tonMoi = yeuCau.TonKho;
        await duLieu.TrongGiaoDichAsync(async phien =>
        {
            Dictionary<string, object?> phuTung = await DocVaKhoaPhuTung(phien, yeuCau.MaPhuTung);
            string maPhuTung = phuTung.Chuoi("MaPhuTung");
            int tonCu = phuTung.SoNguyen("SoLuongTon");
            decimal donGiaNhap = phuTung.SoTien("DonGiaNhap");
            if (tonCu == tonMoi)
            {
                throw new ArgumentException("Tồn mới trùng tồn hiện tại.");
            }

            string sqlLuuLichSu =
                "Insert Into DieuChinhTonKho (MaDieuChinh, MaPhuTung, MaNhanVien, NgayDieuChinh, SoLuongCu, SoLuongMoi, DonGiaNhap, LyDo) "
                + "Values (@MaDieuChinh, @MaPhuTung, @MaNhanVien, @NgayDieuChinh, @SoLuongCu, @SoLuongMoi, @DonGiaNhap, @LyDo)";
            await phien.ThucThiAsync(
                sqlLuuLichSu,
                new
                {
                    MaDieuChinh = XuLyDuLieu.TaoMaMoi("DC"),
                    MaPhuTung = maPhuTung,
                    MaNhanVien = maNhanVien,
                    NgayDieuChinh = XuLyDuLieu.HomNay,
                    SoLuongCu = tonCu,
                    SoLuongMoi = tonMoi,
                    DonGiaNhap = donGiaNhap,
                    LyDo = lyDo,
                }
            );
            string sqlCapNhatTon =
                "Update PhuTung "
                + "Set SoLuongTon = @SoLuongTon "
                + "Where MaPhuTung = @MaPhuTung";
            await phien.ThucThiAsync(
                sqlCapNhatTon,
                new { MaPhuTung = maPhuTung, SoLuongTon = tonMoi }
            );
            return true;
        });
        return Ok(new { message = "Đã điều chỉnh tồn và lưu lịch sử." });
    }

    [HttpGet("history")]
    public async Task<IActionResult> LichSuDieuChinh(
        [FromQuery(Name = "q")] string? tuKhoa = null,
        [FromQuery(Name = "page")] int trang = 1,
        [FromQuery(Name = "pageSize")] int kichThuocTrang = 12
    )
    {
        PhanTrang.KiemTra(trang, kichThuocTrang, tuKhoa);
        var thamSo = new
        {
            TuKhoa = "%" + (tuKhoa ?? "").Trim() + "%",
            BoQua = (trang - 1) * kichThuocTrang,
            SoDong = kichThuocTrang,
        };
        const string sqlLichSu = """
            SELECT dc.MaDieuChinh AS id,
                   dc.MaPhuTung AS partId,
                   N'Tồn kho' AS kind,
                   dc.NgayDieuChinh AS date,
                   CAST(dc.SoLuongCu AS decimal(18,0)) AS oldValue,
                   CAST(dc.SoLuongMoi AS decimal(18,0)) AS newValue,
                   dc.LyDo AS reason,
                   nv.HoTen AS employee
            FROM DieuChinhTonKho dc
            JOIN NhanVien nv ON nv.MaNhanVien = dc.MaNhanVien

            UNION ALL

            SELECT dg.MaDieuChinhGia, dg.MaPhuTung, N'Giá bán', dg.NgayDieuChinh,
                   dg.GiaBanCu, dg.GiaBanMoi, dg.LyDo, nv.HoTen
            FROM DieuChinhGiaBan dg
            JOIN NhanVien nv ON nv.MaNhanVien = dg.MaNhanVien
            """;
        string sqlDem =
            "Select Count(*) "
            + "From ("
            + sqlLichSu
            + ") ls "
            + "Where ls.partId Like @TuKhoa Or ls.reason Like @TuKhoa";
        string sqlDanhSach =
            "Select * "
            + "From ("
            + sqlLichSu
            + ") ls "
            + "Where ls.partId Like @TuKhoa Or ls.reason Like @TuKhoa "
            + "Order By ls.date Desc, ls.id Desc "
            + "Offset @BoQua Rows Fetch Next @SoDong Rows Only";
        int tongSoDong = await duLieu.DocGiaTriAsync<int>(sqlDem, thamSo);
        List<Dictionary<string, object?>> lichSu = await duLieu.DocBangAsync(sqlDanhSach, thamSo);
        return Ok(
            new
            {
                items = lichSu,
                total = tongSoDong,
                page = trang,
                totalPages = TinhSoTrang(tongSoDong, kichThuocTrang),
            }
        );
    }

    private static void KiemTraPhieuNhap(ThongTinPhieuNhap yeuCau)
    {
        if (yeuCau.Ngay.Date > XuLyDuLieu.HomNay || yeuCau.Ngay.Year < 2000)
        {
            throw new ArgumentException("Ngày nhập không được sau hôm nay và phải từ năm 2000.");
        }

        int soPhuTungKhacNhau = yeuCau
            .ChiTiet.Select(dong => dong.MaPhuTung.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        if (soPhuTungKhacNhau != yeuCau.ChiTiet.Count)
        {
            throw new ArgumentException("Không chọn trùng phụ tùng.");
        }
    }

    private static async Task<Dictionary<string, object?>> DocVaKhoaPhuTung(
        PhienGiaoDich phien,
        string maPhuTung
    )
    {
        string sqlPhuTung =
            "Select MaPhuTung, SoLuongTon, DonGiaNhap, HanSuDung, TrangThai "
            + "From PhuTung With (UpdLock, HoldLock) "
            + "Where MaPhuTung = @MaPhuTung";
        List<Dictionary<string, object?>> ketQua = await phien.DocBangAsync(
            sqlPhuTung,
            new { MaPhuTung = maPhuTung.Trim() }
        );
        if (ketQua.Count == 0)
        {
            throw new KeyNotFoundException($"Không tìm thấy phụ tùng {maPhuTung.Trim()}.");
        }

        return ketQua[0];
    }

    private static int TinhSoTrang(int tongSoDong, int soDongMoiTrang)
    {
        int soTrang = (int)Math.Ceiling(tongSoDong / (double)soDongMoiTrang);
        return Math.Max(1, soTrang);
    }
}
