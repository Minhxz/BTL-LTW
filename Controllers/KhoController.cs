using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;

// Người 4: phiếu nhập kho, kiểm kê (điều chỉnh tồn) và lịch sử điều chỉnh.
// Chỉ tài khoản Quản lý được dùng các chức năng này.
[ApiController, Authorize(Roles = "Manager"), Route("api/workshop/stock")]
public sealed class KhoController(XuLyDuLieu db) : ControllerBase
{
    private const int TonKhoToiDa = 1000000;

    // Danh sách phiếu nhập: tìm theo mã phiếu hoặc ghi chú, mới nhất lên đầu.
    [HttpGet("purchases")]
    public async Task<IActionResult> DanhSach(string? q = null, int page = 1, int pageSize = 12)
    {
        PhanTrang.KiemTra(page, pageSize, q);

        var thamSo = new
        {
            TuKhoa = "%" + (q ?? "").Trim() + "%",
            BoQua = (page - 1) * pageSize,
            SoDong = pageSize
        };

        string sqlDemPhieu = "Select Count(*) " +
            "From PhieuNhap " +
            "Where MaPhieuNhap Like @TuKhoa Or GhiChu Like @TuKhoa";

        string sqlDanhSachPhieu = "Select pn.MaPhieuNhap As id, pn.NgayNhap As date, nv.HoTen As employee, pn.GhiChu As note, pn.TongTienNhap As amount " +
            "From PhieuNhap pn Join NhanVien nv On nv.MaNhanVien = pn.MaNhanVienNhap " +
            "Where pn.MaPhieuNhap Like @TuKhoa Or pn.GhiChu Like @TuKhoa " +
            "Order By pn.NgayNhap Desc, pn.MaPhieuNhap Desc " +
            "Offset @BoQua Rows Fetch Next @SoDong Rows Only";

        int tongSoPhieu = await db.DocGiaTriAsync<int>(sqlDemPhieu, thamSo);
        List<Dictionary<string, object?>> danhSachPhieu = await db.DocBangAsync(sqlDanhSachPhieu, thamSo);

        return Ok(new
        {
            items = danhSachPhieu,
            total = tongSoPhieu,
            page,
            totalPages = TinhSoTrang(tongSoPhieu, pageSize)
        });
    }

    // Chi tiết một phiếu nhập: từng phụ tùng, số lượng, đơn giá nhập và thành tiền.
    [HttpGet("purchases/{maPhieuNhap}")]
    public async Task<IActionResult> ChiTiet(string maPhieuNhap)
    {
        string sqlKiemTraPhieu = "Select Count(*) " +
            "From PhieuNhap " +
            "Where MaPhieuNhap = @MaPhieuNhap";

        int soPhieu = await db.DocGiaTriAsync<int>(sqlKiemTraPhieu, new { MaPhieuNhap = maPhieuNhap });

        if (soPhieu == 0)
        {
            throw new KeyNotFoundException("Không tìm thấy phiếu nhập.");
        }

        string sqlChiTietPhieu = "Select ct.MaPhuTung As partId, pt.TenPhuTung As name, ct.SoLuongNhap As quantity, " +
            "ct.ThanhTien / ct.SoLuongNhap As price, ct.ThanhTien As amount " +
            "From ChiTietPhieuNhap ct Join PhuTung pt On pt.MaPhuTung = ct.MaPhuTung " +
            "Where ct.MaPhieuNhap = @MaPhieuNhap " +
            "Order By ct.MaPhuTung";

        List<Dictionary<string, object?>> chiTietPhieu = await db.DocBangAsync(sqlChiTietPhieu, new { MaPhieuNhap = maPhieuNhap });

        return Ok(new { lines = chiTietPhieu });
    }

    // Lập phiếu nhập: cộng tồn từng phụ tùng, thành tiền = giá nhập của lô × số lượng.
    [HttpPost("purchases")]
    public async Task<IActionResult> NhapKho(ThongTinPhieuNhap input)
    {
        KiemTraPhieuNhap(input);

        string maNhanVien = PhanQuyenNguoiDung.MaNhanVien(User);
        DateTime ngayNhap = input.Ngay.Date;
        string ghiChu = input.GhiChu?.Trim() ?? "";

        // Cả phiếu được lưu trong 1 giao dịch: lỗi ở bất kỳ dòng nào thì không lưu gì cả.
        string maPhieuNhap = await db.TrongGiaoDichAsync(async s =>
        {
            string maPhieuMoi = XuLyDuLieu.TaoMaMoi("PN");
            decimal tongTienNhap = 0;

            // 1. Tạo phiếu nhập, tổng tiền cập nhật ở bước 4
            string sqlThemPhieu = "Insert Into PhieuNhap (MaPhieuNhap, MaNhanVienNhap, NgayNhap, GhiChu, TongTienNhap) " +
                "Values (@MaPhieuNhap, @MaNhanVien, @NgayNhap, @GhiChu, 0)";

            await s.ThucThiAsync(sqlThemPhieu, new { MaPhieuNhap = maPhieuMoi, MaNhanVien = maNhanVien, NgayNhap = ngayNhap, GhiChu = ghiChu });

            // Xử lý theo thứ tự mã để mọi giao dịch khóa phụ tùng cùng một thứ tự, tránh deadlock
            List<PhuTungNhap> danhSachNhap = input.ChiTiet
                .OrderBy(dong => dong.MaPhuTung.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (PhuTungNhap dong in danhSachNhap)
            {
                // 2. Lô phụ tùng phải tồn tại, đang hoạt động và còn hạn sử dụng
                Dictionary<string, object?> phuTung = await DocVaKhoaPhuTung(s, dong.MaPhuTung);
                string maPhuTung = phuTung.Chuoi("MaPhuTung");
                bool dangHoatDong = Convert.ToBoolean(phuTung["TrangThai"]);
                DateTime hanSuDung = Convert.ToDateTime(phuTung["HanSuDung"]);

                if (!dangHoatDong)
                {
                    throw new InvalidOperationException($"Phụ tùng {maPhuTung} đã ngừng sử dụng, không nhập thêm.");
                }

                if (hanSuDung < XuLyDuLieu.HomNay)
                {
                    throw new InvalidOperationException($"Phụ tùng {maPhuTung} đã hết hạn sử dụng, không nhận nhập kho.");
                }

                // 3. Cộng tồn và ghi chi tiết phiếu nhập
                decimal donGiaNhap = phuTung.SoTien("DonGiaNhap");
                decimal thanhTien = donGiaNhap * dong.SoLuong;
                tongTienNhap += thanhTien;

                string sqlCongTon = "Update PhuTung " +
                    "Set SoLuongTon = SoLuongTon + @SoLuong " +
                    "Where MaPhuTung = @MaPhuTung And SoLuongTon + @SoLuong <= @TonKhoToiDa";

                int soDongCapNhat = await s.ThucThiAsync(sqlCongTon, new { MaPhuTung = maPhuTung, SoLuong = dong.SoLuong, TonKhoToiDa });

                if (soDongCapNhat != 1)
                {
                    throw new InvalidOperationException($"Tồn kho phụ tùng {maPhuTung} vượt mức {TonKhoToiDa:N0}.");
                }

                string sqlThemChiTiet = "Insert Into ChiTietPhieuNhap (MaPhieuNhap, MaPhuTung, SoLuongNhap, ThanhTien) " +
                    "Values (@MaPhieuNhap, @MaPhuTung, @SoLuong, @ThanhTien)";

                await s.ThucThiAsync(sqlThemChiTiet, new { MaPhieuNhap = maPhieuMoi, MaPhuTung = maPhuTung, SoLuong = dong.SoLuong, ThanhTien = thanhTien });
            }

            // 4. Ghi tổng tiền của cả phiếu
            string sqlCapNhatTongTien = "Update PhieuNhap " +
                "Set TongTienNhap = @TongTienNhap " +
                "Where MaPhieuNhap = @MaPhieuNhap";

            await s.ThucThiAsync(sqlCapNhatTongTien, new { MaPhieuNhap = maPhieuMoi, TongTienNhap = tongTienNhap });

            return maPhieuMoi;
        });

        return StatusCode(201, new { id = maPhieuNhap, message = "Đã lưu phiếu nhập và cộng kho." });
    }

    // Kiểm kê: đưa tồn về số đếm thực tế và lưu lịch sử (tồn cũ, tồn mới, giá nhập, lý do).
    [HttpPost("adjust")]
    public async Task<IActionResult> DieuChinhTon(YeuCauDieuChinhTon input)
    {
        // Bỏ khoảng trắng trước khi đếm, tránh lý do chỉ gồm dấu cách cho đủ 5 ký tự
        string lyDo = input.LyDo.Trim();

        if (lyDo.Length < 5)
        {
            throw new ArgumentException("Nhập lý do điều chỉnh tối thiểu 5 ký tự.");
        }

        string maNhanVien = PhanQuyenNguoiDung.MaNhanVien(User);
        int tonMoi = input.TonKho;

        await db.TrongGiaoDichAsync(async s =>
        {
            Dictionary<string, object?> phuTung = await DocVaKhoaPhuTung(s, input.MaPhuTung);
            string maPhuTung = phuTung.Chuoi("MaPhuTung");
            int tonCu = phuTung.SoNguyen("SoLuongTon");
            decimal donGiaNhap = phuTung.SoTien("DonGiaNhap");

            if (tonCu == tonMoi)
            {
                throw new ArgumentException("Tồn mới trùng tồn hiện tại.");
            }

            // 1. Lưu lịch sử điều chỉnh trước khi đổi tồn
            string sqlLuuLichSu = "Insert Into DieuChinhTonKho (MaDieuChinh, MaPhuTung, MaNhanVien, NgayDieuChinh, SoLuongCu, SoLuongMoi, DonGiaNhap, LyDo) " +
                "Values (@MaDieuChinh, @MaPhuTung, @MaNhanVien, @NgayDieuChinh, @SoLuongCu, @SoLuongMoi, @DonGiaNhap, @LyDo)";

            await s.ThucThiAsync(sqlLuuLichSu, new
            {
                MaDieuChinh = XuLyDuLieu.TaoMaMoi("DC"),
                MaPhuTung = maPhuTung,
                MaNhanVien = maNhanVien,
                NgayDieuChinh = XuLyDuLieu.HomNay,
                SoLuongCu = tonCu,
                SoLuongMoi = tonMoi,
                DonGiaNhap = donGiaNhap,
                LyDo = lyDo
            });

            // 2. Cập nhật tồn kho theo số kiểm kê
            string sqlCapNhatTon = "Update PhuTung " +
                "Set SoLuongTon = @SoLuongTon " +
                "Where MaPhuTung = @MaPhuTung";

            await s.ThucThiAsync(sqlCapNhatTon, new { MaPhuTung = maPhuTung, SoLuongTon = tonMoi });

            return true;
        });

        return Ok(new { message = "Đã điều chỉnh tồn và lưu lịch sử." });
    }

    // Lịch sử kho: gộp điều chỉnh tồn (kiểm kê) và điều chỉnh giá bán, mới nhất lên đầu.
    [HttpGet("history")]
    public async Task<IActionResult> LichSuDieuChinh(string? q = null, int page = 1, int pageSize = 12)
    {
        PhanTrang.KiemTra(page, pageSize, q);

        var thamSo = new
        {
            TuKhoa = "%" + (q ?? "").Trim() + "%",
            BoQua = (page - 1) * pageSize,
            SoDong = pageSize
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

        string sqlDem = "Select Count(*) " +
            "From (" + sqlLichSu + ") ls " +
            "Where ls.partId Like @TuKhoa Or ls.reason Like @TuKhoa";

        string sqlDanhSach = "Select * " +
            "From (" + sqlLichSu + ") ls " +
            "Where ls.partId Like @TuKhoa Or ls.reason Like @TuKhoa " +
            "Order By ls.date Desc, ls.id Desc " +
            "Offset @BoQua Rows Fetch Next @SoDong Rows Only";

        int tongSoDong = await db.DocGiaTriAsync<int>(sqlDem, thamSo);
        List<Dictionary<string, object?>> lichSu = await db.DocBangAsync(sqlDanhSach, thamSo);

        return Ok(new
        {
            items = lichSu,
            total = tongSoDong,
            page,
            totalPages = TinhSoTrang(tongSoDong, pageSize)
        });
    }

    // ===== Các hàm phụ =====

    private static void KiemTraPhieuNhap(ThongTinPhieuNhap input)
    {
        if (input.Ngay.Date > XuLyDuLieu.HomNay || input.Ngay.Year < 2000)
        {
            throw new ArgumentException("Ngày nhập không được sau hôm nay và phải từ năm 2000.");
        }

        // SQL Server so sánh mã không phân biệt hoa/thường nên kiểm tra trùng cũng bỏ qua hoa/thường
        int soPhuTungKhacNhau = input.ChiTiet
            .Select(dong => dong.MaPhuTung.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (soPhuTungKhacNhau != input.ChiTiet.Count)
        {
            throw new ArgumentException("Không chọn trùng phụ tùng.");
        }
    }

    // Đọc lô phụ tùng và khóa dòng đó đến hết giao dịch, để 2 thao tác kho cùng lúc không ghi đè tồn của nhau.
    private static async Task<Dictionary<string, object?>> DocVaKhoaPhuTung(PhienGiaoDich s, string maPhuTung)
    {
        string sqlPhuTung = "Select MaPhuTung, SoLuongTon, DonGiaNhap, HanSuDung, TrangThai " +
            "From PhuTung With (UpdLock, HoldLock) " +
            "Where MaPhuTung = @MaPhuTung";

        List<Dictionary<string, object?>> ketQua = await s.DocBangAsync(sqlPhuTung, new { MaPhuTung = maPhuTung.Trim() });

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
