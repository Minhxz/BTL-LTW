using System.Security.Claims;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.LopDungChung;

public sealed class NghiepVuSuaXe(XuLyDuLieu duLieu)
{
    public const string TruyVanPhieu = """
        SELECT p.MaPhieuTiepNhan AS id,
               p.MaXe AS vehicleId,
               p.NgayLap AS createdOn,
               p.NgayHen AS dueDate,
               p.TrangThai AS status,
               x.BienXe AS licensePlate,
               x.LoaiXe AS vehicleType,
               x.HangSanXuat AS brand,
               x.MauSac AS color,
               k.TenKhachHang AS customerName,
               k.SoDienThoai AS phone,
               n.HoTen AS receivedBy,
               COALESCE((SELECT SUM(d.ThanhTien) FROM ChiTietPhieuTiepNhan d WHERE d.MaPhieuTiepNhan = p.MaPhieuTiepNhan), 0) AS total,
               (SELECT COUNT(*) FROM HoaDon h WHERE h.MaPhieuTiepNhan = p.MaPhieuTiepNhan) AS paid
        FROM PhieuTiepNhan p
        JOIN Xe x ON x.MaXe = p.MaXe
        JOIN KhachHang k ON k.MaKhachHang = x.MaKhachHang
        JOIN NhanVien n ON n.MaNhanVien = p.MaNhanVien
        """;

    public Task<string> TaoPhieuAsync(ThongTinTiepNhan yeuCau, string maNhanVienThamChieu)
    {
        return duLieu.TrongGiaoDichAsync(phien => TaoPhieu(phien, yeuCau, maNhanVienThamChieu));
    }

    public async Task<string> TaoPhieu(
        PhienGiaoDich phien,
        ThongTinTiepNhan yeuCau,
        string maNhanVienThamChieu
    )
    {
        await KiemTraXe(phien, yeuCau);
        var (phuTungCan, giaDichVu) = await TinhDichVu(phien, yeuCau.LoaiXe.Trim(), yeuCau.DichVu);
        await TruKho(phien, phuTungCan);
        string maKhachHang = await LayKhachHang(phien, yeuCau);
        string bienSo = yeuCau.BienSo.Trim().ToUpperInvariant();
        string maXe = XuLyDuLieu.TaoMaMoi("XE");
        string maPhieu = XuLyDuLieu.TaoMaMoi("PTN");
        string sqlThemXe =
            "Insert Into Xe (MaXe, MaKhachHang, BienXe, LoaiXe, HangSanXuat, MauSac) "
            + "Values (@id, @customer, @plate, @type, @brand, @color)";
        await phien.ThucThiAsync(
            sqlThemXe,
            new
            {
                id = maXe,
                customer = maKhachHang,
                plate = bienSo,
                type = yeuCau.LoaiXe.Trim(),
                brand = yeuCau.HangXe.Trim(),
                color = yeuCau.MauSac.Trim(),
            }
        );
        string sqlThemPhieu =
            "Insert Into PhieuTiepNhan (MaPhieuTiepNhan, MaXe, MaNhanVien, NgayLap, NgayHen, TrangThai) "
            + "Values (@id, @vehicle, @employee, @today, @due, N'Chờ sửa')";
        await phien.ThucThiAsync(
            sqlThemPhieu,
            new
            {
                id = maPhieu,
                vehicle = maXe,
                employee = maNhanVienThamChieu,
                today = XuLyDuLieu.HomNay,
                due = yeuCau.NgayHenTra.Date,
            }
        );
        string sqlThemDichVu =
            "Insert Into ChiTietPhieuTiepNhan (MaPhieuTiepNhan, MaDichVu, SoLuong, ThanhTien) "
            + "Values (@id, @service, @quantity, @amount)";
        foreach (DichVuChon dong in yeuCau.DichVu)
        {
            await phien.ThucThiAsync(
                sqlThemDichVu,
                new
                {
                    id = maPhieu,
                    service = dong.MaDichVu.Trim(),
                    quantity = dong.SoLuong,
                    amount = giaDichVu[dong.MaDichVu] * dong.SoLuong,
                }
            );
        }

        await phien.ThucThiAsync(
            "Insert Into WebPhieuTiepNhan (MaPhieuTiepNhan) Values (@id)",
            new { id = maPhieu }
        );
        string sqlChotPhuTung =
            "Insert Into WebPhieuPhuTung (MaPhieuTiepNhan, MaPhuTung, SoLuong) "
            + "Values (@id, @part, @quantity)";
        foreach (KeyValuePair<string, int> phuTung in phuTungCan)
        {
            await phien.ThucThiAsync(
                sqlChotPhuTung,
                new
                {
                    id = maPhieu,
                    part = phuTung.Key,
                    quantity = phuTung.Value,
                }
            );
        }

        if (!string.IsNullOrWhiteSpace(yeuCau.MaTho))
        {
            YeuCauPhanCong phanCong = new YeuCauPhanCong(
                yeuCau.MaTho,
                XuLyDuLieu.ThoiGianHienTai,
                yeuCau.NgayHenTra.Date.AddDays(1)
            );
            await PhanCong(phien, maPhieu, phanCong);
        }

        return maPhieu;
    }

    private static async Task KiemTraXe(PhienGiaoDich phien, ThongTinTiepNhan yeuCau)
    {
        DateTime ngayHen = yeuCau.NgayHenTra.Date;
        if (ngayHen < XuLyDuLieu.HomNay || ngayHen > XuLyDuLieu.HomNay.AddDays(90))
        {
            throw new ArgumentException("Ngày hẹn trả phải từ hôm nay đến 90 ngày tới.");
        }

        string bienSoChuanHoa = BienSoXe.ChuanHoa(yeuCau.BienSo);
        string sqlXeDangSua =
            "Select Count(*) "
            + "From Xe x With (UpdLock, HoldLock) Join PhieuTiepNhan p On p.MaXe = x.MaXe "
            + "Where "
            + BienSoXe.KhoaSql("x.BienXe")
            + " = @plate And p.TrangThai In (N'Chờ sửa', N'Đang sửa')";
        int soPhieuDangMo = await phien.DocGiaTriAsync<int>(
            sqlXeDangSua,
            new { plate = bienSoChuanHoa }
        );
        if (soPhieuDangMo > 0)
        {
            throw new InvalidOperationException("Xe đang có phiếu chưa hoàn thành.");
        }
    }

    public Task<object> XemTruocPhieu(YeuCauXemTruocPhieu yeuCau)
    {
        return duLieu.TrongGiaoDichAsync<object>(async phien =>
        {
            var (phuTungCan, giaDichVu) = await TinhDichVu(
                phien,
                yeuCau.LoaiXe.Trim(),
                yeuCau.DichVu
            );
            List<object> danhSachPhuTung = new List<object>(phuTungCan.Count);
            foreach (KeyValuePair<string, int> can in phuTungCan)
            {
                string sqlPhuTung =
                    "Select TenPhuTung, SoLuongTon, TrangThai, HanSuDung "
                    + "From PhuTung "
                    + "Where MaPhuTung = @id";
                Dictionary<string, object?> phuTung = (
                    await phien.DocBangAsync(sqlPhuTung, new { id = can.Key })
                ).Single();
                int soLuongTon = phuTung.SoNguyen("SoLuongTon");
                string vanDe = "";
                if (!(bool)phuTung["TrangThai"]!)
                {
                    vanDe = "Ngừng sử dụng";
                }
                else if ((DateTime)phuTung["HanSuDung"]! < XuLyDuLieu.HomNay)
                {
                    vanDe = "Hết hạn";
                }
                else if (soLuongTon < can.Value)
                {
                    vanDe = "Thiếu tồn";
                }

                danhSachPhuTung.Add(
                    new
                    {
                        id = can.Key,
                        name = phuTung.Chuoi("TenPhuTung"),
                        quantity = can.Value,
                        stock = soLuongTon,
                        issue = vanDe,
                    }
                );
            }

            decimal tongTien = yeuCau.DichVu.Sum(dong => giaDichVu[dong.MaDichVu] * dong.SoLuong);
            return new { total = tongTien, parts = danhSachPhuTung };
        });
    }

    private static async Task<(
        SortedDictionary<string, int> PhuTung,
        Dictionary<string, decimal> Gia
    )> TinhDichVu(PhienGiaoDich phien, string mauXe, List<DichVuChon> dichVu)
    {
        int soDichVuKhacNhau = dichVu
            .Select(dong => dong.MaDichVu.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        if (dichVu.Count == 0 || soDichVuKhacNhau != dichVu.Count)
        {
            throw new ArgumentException("Chọn ít nhất một dịch vụ, không chọn trùng.");
        }

        SortedDictionary<string, int> phuTungCan = new SortedDictionary<string, int>();
        Dictionary<string, decimal> giaDichVu = new Dictionary<string, decimal>();
        foreach (
            DichVuChon dong in dichVu.OrderBy(
                phanTu => phanTu.MaDichVu,
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            if (dong.SoLuong is < 1 or > 1000)
            {
                throw new ArgumentException("Số lượng dịch vụ từ 1 đến 1000.");
            }

            string sqlDichVu =
                "Select TenDichVu, ThanhTienDichVu "
                + "From DichVu With (UpdLock, HoldLock) "
                + "Where MaDichVu = @id And TrangThai = 1";
            Dictionary<string, object?> thongTinDichVu =
                (
                    await phien.DocBangAsync(sqlDichVu, new { id = dong.MaDichVu.Trim() })
                ).SingleOrDefault()
                ?? throw new InvalidOperationException(
                    "Dịch vụ không tồn tại hoặc đã ngừng áp dụng: " + dong.MaDichVu
                );
            PhanLoaiXeMay.KiemTraDichVu(mauXe, thongTinDichVu.Chuoi("TenDichVu"));
            giaDichVu[dong.MaDichVu] = thongTinDichVu.SoTien("ThanhTienDichVu");
            string sqlDinhMuc =
                "Select MaPhuTung, SoLuong "
                + "From ChiTietDichVu With (HoldLock) "
                + "Where MaDichVu = @id";
            foreach (
                Dictionary<string, object?> dinhMuc in await phien.DocBangAsync(
                    sqlDinhMuc,
                    new { id = dong.MaDichVu.Trim() }
                )
            )
            {
                string maPhuTung = dinhMuc.Chuoi("MaPhuTung");
                long tongCan =
                    (long)phuTungCan.GetValueOrDefault(maPhuTung)
                    + (long)dinhMuc.SoNguyen("SoLuong") * dong.SoLuong;
                if (tongCan > int.MaxValue)
                {
                    throw new ArgumentException($"Số lượng phụ tùng {maPhuTung} vượt giới hạn.");
                }

                phuTungCan[maPhuTung] = (int)tongCan;
            }
        }

        return (phuTungCan, giaDichVu);
    }

    private static async Task TruKho(PhienGiaoDich phien, SortedDictionary<string, int> phuTungCan)
    {
        string sqlTruKho =
            "Update PhuTung With (UpdLock) "
            + "Set SoLuongTon = SoLuongTon - @quantity "
            + "Where MaPhuTung = @id And TrangThai = 1 And HanSuDung >= @today And SoLuongTon >= @quantity";
        foreach (KeyValuePair<string, int> can in phuTungCan)
        {
            int soDongCapNhat = await phien.ThucThiAsync(
                sqlTruKho,
                new
                {
                    id = can.Key,
                    quantity = can.Value,
                    today = XuLyDuLieu.HomNay,
                }
            );
            if (soDongCapNhat != 1)
            {
                throw new InvalidOperationException(
                    $"Phụ tùng {can.Key} không đủ tồn, hết hạn hoặc ngừng sử dụng. Phiếu chưa được lưu và kho được giữ nguyên."
                );
            }
        }
    }

    private static async Task<string> LayKhachHang(PhienGiaoDich phien, ThongTinTiepNhan yeuCau)
    {
        string sqlKhachHang =
            "Select MaKhachHang, TenKhachHang "
            + "From KhachHang With (UpdLock, HoldLock) "
            + "Where SoDienThoai = @phone";
        List<Dictionary<string, object?>> khachHang = await phien.DocBangAsync(
            sqlKhachHang,
            new { phone = yeuCau.SoDienThoai }
        );
        if (khachHang.Count == 0)
        {
            string maKhachHangMoi = XuLyDuLieu.TaoMaMoi("KH");
            string sqlThemKhach =
                "Insert Into KhachHang (MaKhachHang, TenKhachHang, SoDienThoai) "
                + "Values (@id, @name, @phone)";
            await phien.ThucThiAsync(
                sqlThemKhach,
                new
                {
                    id = maKhachHangMoi,
                    name = yeuCau.TenKhachHang.Trim(),
                    phone = yeuCau.SoDienThoai,
                }
            );
            return maKhachHangMoi;
        }

        string tenDaDangKy = khachHang[0].Chuoi("TenKhachHang").Trim();
        if (!tenDaDangKy.Equals(yeuCau.TenKhachHang.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Số điện thoại đã có trong danh sách khách hàng. Hãy nhập đúng họ tên đã đăng ký."
            );
        }

        return khachHang[0].Chuoi("MaKhachHang");
    }

    public async Task PhanCong(PhienGiaoDich phien, string maBanGhi, YeuCauPhanCong yeuCau)
    {
        if (
            yeuCau.KetThuc <= yeuCau.BatDau
            || yeuCau.KetThuc - yeuCau.BatDau > TimeSpan.FromDays(90)
        )
        {
            throw new ArgumentException("Thời gian kết thúc phải sau bắt đầu, tối đa 90 ngày.");
        }

        string sqlPhieu =
            "Select MaXe, TrangThai, NgayLap "
            + "From PhieuTiepNhan With (UpdLock, HoldLock) "
            + "Where MaPhieuTiepNhan = @id";
        Dictionary<string, object?> phieu =
            (await phien.DocBangAsync(sqlPhieu, new { id = maBanGhi })).SingleOrDefault()
            ?? throw new KeyNotFoundException("Không tìm thấy phiếu.");
        if (phieu.Chuoi("TrangThai") is not ("Chờ sửa" or "Đang sửa"))
        {
            throw new InvalidOperationException("Chỉ phân công phiếu chưa hoàn tất.");
        }

        if (yeuCau.BatDau.Date < (DateTime)phieu["NgayLap"]!)
        {
            throw new ArgumentException("Thời gian phân công không được trước ngày lập phiếu.");
        }

        string sqlTho =
            "Select c.TenChucVu "
            + "From NhanVien n With (UpdLock, HoldLock) Join ChucVu c On c.MaChucVu = n.MaChucVu "
            + "Where n.MaNhanVien = @employee And n.TrangThai = 1 And c.TenChucVu Like N'Thợ%'";
        List<Dictionary<string, object?>> tho = await phien.DocBangAsync(
            sqlTho,
            new { employee = yeuCau.MaNhanVien }
        );
        if (tho.Count != 1)
        {
            throw new InvalidOperationException("Chọn thợ đang làm việc.");
        }

        string maXe = phieu.Chuoi("MaXe");
        string sqlDemXe =
            "Select Count(Distinct c.MaXe) "
            + "From ChiTietPhanCong c With (UpdLock, HoldLock) Join PhieuTiepNhan p On p.MaXe = c.MaXe "
            + "Where c.MaNhanVien = @employee And c.MaXe <> @vehicle And p.TrangThai In (N'Chờ sửa', N'Đang sửa')";
        int soXeDangNhan = await phien.DocGiaTriAsync<int>(
            sqlDemXe,
            new { employee = yeuCau.MaNhanVien, vehicle = maXe }
        );
        bool laThoChinh = tho[0]
            .Chuoi("TenChucVu")
            .Contains("chính", StringComparison.OrdinalIgnoreCase);
        int soXeToiDa = laThoChinh ? 3 : 2;
        if (soXeDangNhan >= soXeToiDa)
        {
            throw new InvalidOperationException(
                "Thợ đã nhận đủ số xe đang xử lý. Vui lòng chọn thợ khác."
            );
        }

        const string sqlLuuPhanCong = """
            IF EXISTS (SELECT 1 FROM ChiTietPhanCong WHERE MaNhanVien = @employee AND MaXe = @vehicle)
                UPDATE ChiTietPhanCong
                SET ThoiGianBatDau = @start, ThoiGianHoanThanh = @end
                WHERE MaNhanVien = @employee AND MaXe = @vehicle
            ELSE
                INSERT ChiTietPhanCong (MaNhanVien, MaXe, ThoiGianBatDau, ThoiGianHoanThanh)
                VALUES (@employee, @vehicle, @start, @end)
            """;
        await phien.ThucThiAsync(
            sqlLuuPhanCong,
            new
            {
                employee = yeuCau.MaNhanVien,
                vehicle = maXe,
                start = yeuCau.BatDau,
                end = yeuCau.KetThuc,
            }
        );
    }

    public Task<bool> DoiTrangThaiAsync(string maBanGhi, string xuLyTiep, ClaimsPrincipal nguoiDung)
    {
        return duLieu.TrongGiaoDichAsync(async phien =>
        {
            string sqlPhieu =
                "Select MaXe, TrangThai "
                + "From PhieuTiepNhan With (UpdLock, HoldLock) "
                + "Where MaPhieuTiepNhan = @id";
            Dictionary<string, object?> phieu =
                (await phien.DocBangAsync(sqlPhieu, new { id = maBanGhi })).SingleOrDefault()
                ?? throw new KeyNotFoundException("Không tìm thấy phiếu tiếp nhận.");
            string maXe = phieu.Chuoi("MaXe");
            bool laTho = nguoiDung.IsInRole("Mechanic");
            if (laTho)
            {
                string sqlDuocPhanCong =
                    "Select Count(*) From ChiTietPhanCong Where MaXe = @vehicle And MaNhanVien = @employee";
                int duocPhanCong = await phien.DocGiaTriAsync<int>(
                    sqlDuocPhanCong,
                    new { vehicle = maXe, employee = PhanQuyenNguoiDung.MaNhanVien(nguoiDung) }
                );
                if (duocPhanCong == 0)
                {
                    throw new UnauthorizedAccessException(
                        "Bạn chỉ được cập nhật xe được phân công."
                    );
                }
            }

            string trangThaiCu = phieu.Chuoi("TrangThai");
            bool hopLe =
                (trangThaiCu == "Chờ sửa" && xuLyTiep == "Đang sửa")
                || (trangThaiCu == "Đang sửa" && xuLyTiep == "Hoàn thành")
                || (trangThaiCu == "Chờ sửa" && xuLyTiep == "Đã hủy" && !laTho);
            if (!hopLe)
            {
                throw new InvalidOperationException(
                    "Chuyển trạng thái không hợp lệ. Luồng: Chờ sửa → Đang sửa → Hoàn thành; chỉ hủy khi Chờ sửa."
                );
            }

            if (xuLyTiep == "Đang sửa")
            {
                int soTho = await phien.DocGiaTriAsync<int>(
                    "Select Count(*) From ChiTietPhanCong Where MaXe = @id",
                    new { id = maXe }
                );
                if (soTho == 0)
                {
                    throw new InvalidOperationException("Cần phân công thợ trước khi bắt đầu sửa.");
                }
            }

            if (xuLyTiep == "Đã hủy")
            {
                int laPhieuWeb = await phien.DocGiaTriAsync<int>(
                    "Select Count(*) From WebPhieuTiepNhan Where MaPhieuTiepNhan = @id",
                    new { id = maBanGhi }
                );
                if (laPhieuWeb == 0)
                {
                    throw new InvalidOperationException(
                        "Phiếu tạo từ ứng dụng khác chưa có bản chốt xuất kho. Hãy hủy tại ứng dụng đã tạo phiếu để tránh hoàn kho sai."
                    );
                }

                List<Dictionary<string, object?>> phuTungDaXuat = await phien.DocBangAsync(
                    "Select MaPhuTung, SoLuong From WebPhieuPhuTung Where MaPhieuTiepNhan = @id",
                    new { id = maBanGhi }
                );
                foreach (Dictionary<string, object?> phuTung in phuTungDaXuat)
                {
                    await phien.ThucThiAsync(
                        "Update PhuTung Set SoLuongTon = SoLuongTon + @quantity Where MaPhuTung = @part",
                        new
                        {
                            quantity = phuTung.SoNguyen("SoLuong"),
                            part = phuTung.Chuoi("MaPhuTung"),
                        }
                    );
                }
            }

            await phien.ThucThiAsync(
                "Update PhieuTiepNhan Set TrangThai = @next Where MaPhieuTiepNhan = @id",
                new { id = maBanGhi, next = xuLyTiep }
            );
            return true;
        });
    }
}
