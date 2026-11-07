using System.Security.Claims;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.LopDungChung;

// Người 3: nghiệp vụ phiếu sửa xe — tiếp nhận, trừ kho, phân công thợ, đổi trạng thái.
public sealed class NghiepVuSuaXe(XuLyDuLieu db)
{
    // Câu truy vấn phiếu dùng chung cho danh sách phiếu, chi tiết phiếu và tra cứu.
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

    public Task<string> TaoPhieuAsync(ThongTinTiepNhan input, string employee)
    {
        return db.TrongGiaoDichAsync(s => TaoPhieu(s, input, employee));
    }

    // Tạo phiếu tiếp nhận. Các bước dùng chung một giao dịch: có lỗi thì hoàn tác toàn bộ, kể cả trừ kho.
    public async Task<string> TaoPhieu(PhienGiaoDich s, ThongTinTiepNhan input, string employee)
    {
        // 1. Kiểm tra ngày hẹn và xe chưa có phiếu đang mở
        await KiemTraXe(s, input);

        // 2. Tính giá dịch vụ, tổng phụ tùng cần dùng rồi trừ kho
        var (phuTungCan, giaDichVu) = await TinhDichVu(s, input.LoaiXe.Trim(), input.DichVu);
        await TruKho(s, phuTungCan);

        // 3. Lấy khách hàng theo số điện thoại (chưa có thì tạo mới)
        string maKhachHang = await LayKhachHang(s, input);
        string bienSo = input.BienSo.Trim().ToUpperInvariant();
        string maXe = XuLyDuLieu.TaoMaMoi("XE");
        string maPhieu = XuLyDuLieu.TaoMaMoi("PTN");

        // 4. Lưu xe và phiếu tiếp nhận
        string sqlThemXe = "Insert Into Xe (MaXe, MaKhachHang, BienXe, LoaiXe, HangSanXuat, MauSac) " +
            "Values (@id, @customer, @plate, @type, @brand, @color)";

        await s.ThucThiAsync(sqlThemXe, new
        {
            id = maXe,
            customer = maKhachHang,
            plate = bienSo,
            type = input.LoaiXe.Trim(),
            brand = input.HangXe.Trim(),
            color = input.MauSac.Trim()
        });

        string sqlThemPhieu = "Insert Into PhieuTiepNhan (MaPhieuTiepNhan, MaXe, MaNhanVien, NgayLap, NgayHen, TrangThai) " +
            "Values (@id, @vehicle, @employee, @today, @due, N'Chờ sửa')";

        await s.ThucThiAsync(sqlThemPhieu, new
        {
            id = maPhieu,
            vehicle = maXe,
            employee,
            today = XuLyDuLieu.HomNay,
            due = input.NgayHenTra.Date
        });

        // 5. Lưu từng dịch vụ với giá đã chốt
        string sqlThemDichVu = "Insert Into ChiTietPhieuTiepNhan (MaPhieuTiepNhan, MaDichVu, SoLuong, ThanhTien) " +
            "Values (@id, @service, @quantity, @amount)";

        foreach (DichVuChon dong in input.DichVu)
        {
            await s.ThucThiAsync(sqlThemDichVu, new
            {
                id = maPhieu,
                service = dong.MaDichVu.Trim(),
                quantity = dong.SoLuong,
                amount = giaDichVu[dong.MaDichVu] * dong.SoLuong
            });
        }

        // 6. Đánh dấu phiếu do web tạo và chốt số phụ tùng đã xuất (để hủy phiếu thì hoàn kho đúng)
        await s.ThucThiAsync("Insert Into WebPhieuTiepNhan (MaPhieuTiepNhan) Values (@id)", new { id = maPhieu });

        string sqlChotPhuTung = "Insert Into WebPhieuPhuTung (MaPhieuTiepNhan, MaPhuTung, SoLuong) " +
            "Values (@id, @part, @quantity)";

        foreach (KeyValuePair<string, int> phuTung in phuTungCan)
        {
            await s.ThucThiAsync(sqlChotPhuTung, new { id = maPhieu, part = phuTung.Key, quantity = phuTung.Value });
        }

        // 7. Phân công thợ ngay nếu đã chọn
        if (!string.IsNullOrWhiteSpace(input.MaTho))
        {
            YeuCauPhanCong phanCong = new YeuCauPhanCong(input.MaTho, XuLyDuLieu.ThoiGianHienTai, input.NgayHenTra.Date.AddDays(1));
            await PhanCong(s, maPhieu, phanCong);
        }

        return maPhieu;
    }

    private static async Task KiemTraXe(PhienGiaoDich s, ThongTinTiepNhan input)
    {
        DateTime ngayHen = input.NgayHenTra.Date;

        if (ngayHen < XuLyDuLieu.HomNay || ngayHen > XuLyDuLieu.HomNay.AddDays(90))
        {
            throw new ArgumentException("Ngày hẹn trả phải từ hôm nay đến 90 ngày tới.");
        }

        // So sánh biển số đã chuẩn hóa (bỏ khoảng trắng, gạch, chấm) để không lọt xe trùng
        string bienSoChuanHoa = BienSoXe.ChuanHoa(input.BienSo);

        string sqlXeDangSua = "Select Count(*) " +
            "From Xe x With (UpdLock, HoldLock) Join PhieuTiepNhan p On p.MaXe = x.MaXe " +
            "Where " + BienSoXe.KhoaSql("x.BienXe") + " = @plate And p.TrangThai In (N'Chờ sửa', N'Đang sửa')";

        int soPhieuDangMo = await s.DocGiaTriAsync<int>(sqlXeDangSua, new { plate = bienSoChuanHoa });

        if (soPhieuDangMo > 0)
        {
            throw new InvalidOperationException("Xe đang có phiếu chưa hoàn thành.");
        }
    }

    // Xem trước tổng tiền và tình trạng phụ tùng trước khi tiếp nhận. Không lưu gì vào CSDL.
    public Task<object> XemTruocPhieu(YeuCauXemTruocPhieu input)
    {
        return db.TrongGiaoDichAsync<object>(async s =>
        {
            var (phuTungCan, giaDichVu) = await TinhDichVu(s, input.LoaiXe.Trim(), input.DichVu);
            List<object> danhSachPhuTung = new List<object>(phuTungCan.Count);

            foreach (KeyValuePair<string, int> can in phuTungCan)
            {
                string sqlPhuTung = "Select TenPhuTung, SoLuongTon, TrangThai, HanSuDung " +
                    "From PhuTung " +
                    "Where MaPhuTung = @id";

                Dictionary<string, object?> phuTung = (await s.DocBangAsync(sqlPhuTung, new { id = can.Key })).Single();
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

                danhSachPhuTung.Add(new
                {
                    id = can.Key,
                    name = phuTung.Chuoi("TenPhuTung"),
                    quantity = can.Value,
                    stock = soLuongTon,
                    issue = vanDe
                });
            }

            decimal tongTien = input.DichVu.Sum(dong => giaDichVu[dong.MaDichVu] * dong.SoLuong);

            return new { total = tongTien, parts = danhSachPhuTung };
        });
    }

    // Tính giá từng dịch vụ và tổng số phụ tùng cần dùng theo định mức.
    // Phụ tùng sắp theo mã (SortedDictionary) để các giao dịch khóa cùng một thứ tự.
    private static async Task<(SortedDictionary<string, int> PhuTung, Dictionary<string, decimal> Gia)> TinhDichVu(
        PhienGiaoDich s, string mauXe, List<DichVuChon> dichVu)
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

        foreach (DichVuChon dong in dichVu.OrderBy(x => x.MaDichVu, StringComparer.OrdinalIgnoreCase))
        {
            if (dong.SoLuong is < 1 or > 1000)
            {
                throw new ArgumentException("Số lượng dịch vụ từ 1 đến 1000.");
            }

            string sqlDichVu = "Select TenDichVu, ThanhTienDichVu " +
                "From DichVu With (UpdLock, HoldLock) " +
                "Where MaDichVu = @id And TrangThai = 1";

            Dictionary<string, object?> thongTinDichVu = (await s.DocBangAsync(sqlDichVu, new { id = dong.MaDichVu.Trim() })).SingleOrDefault()
                ?? throw new InvalidOperationException("Dịch vụ không tồn tại hoặc đã ngừng áp dụng: " + dong.MaDichVu);

            PhanLoaiXeMay.KiemTraDichVu(mauXe, thongTinDichVu.Chuoi("TenDichVu"));
            giaDichVu[dong.MaDichVu] = thongTinDichVu.SoTien("ThanhTienDichVu");

            string sqlDinhMuc = "Select MaPhuTung, SoLuong " +
                "From ChiTietDichVu With (HoldLock) " +
                "Where MaDichVu = @id";

            foreach (Dictionary<string, object?> dinhMuc in await s.DocBangAsync(sqlDinhMuc, new { id = dong.MaDichVu.Trim() }))
            {
                string maPhuTung = dinhMuc.Chuoi("MaPhuTung");
                long tongCan = (long)phuTungCan.GetValueOrDefault(maPhuTung) + (long)dinhMuc.SoNguyen("SoLuong") * dong.SoLuong;

                if (tongCan > int.MaxValue)
                {
                    throw new ArgumentException($"Số lượng phụ tùng {maPhuTung} vượt giới hạn.");
                }

                phuTungCan[maPhuTung] = (int)tongCan;
            }
        }

        return (phuTungCan, giaDichVu);
    }

    // Trừ kho từng phụ tùng; chỉ trừ khi còn đủ tồn, còn hạn và đang hoạt động.
    private static async Task TruKho(PhienGiaoDich s, SortedDictionary<string, int> phuTungCan)
    {
        string sqlTruKho = "Update PhuTung With (UpdLock) " +
            "Set SoLuongTon = SoLuongTon - @quantity " +
            "Where MaPhuTung = @id And TrangThai = 1 And HanSuDung >= @today And SoLuongTon >= @quantity";

        foreach (KeyValuePair<string, int> can in phuTungCan)
        {
            int soDongCapNhat = await s.ThucThiAsync(sqlTruKho, new { id = can.Key, quantity = can.Value, today = XuLyDuLieu.HomNay });

            if (soDongCapNhat != 1)
            {
                throw new InvalidOperationException($"Phụ tùng {can.Key} không đủ tồn, hết hạn hoặc ngừng sử dụng. Phiếu chưa được lưu và kho được giữ nguyên.");
            }
        }
    }

    private static async Task<string> LayKhachHang(PhienGiaoDich s, ThongTinTiepNhan input)
    {
        string sqlKhachHang = "Select MaKhachHang, TenKhachHang " +
            "From KhachHang With (UpdLock, HoldLock) " +
            "Where SoDienThoai = @phone";

        List<Dictionary<string, object?>> khachHang = await s.DocBangAsync(sqlKhachHang, new { phone = input.SoDienThoai });

        // Số điện thoại mới: tạo khách hàng mới
        if (khachHang.Count == 0)
        {
            string maKhachHangMoi = XuLyDuLieu.TaoMaMoi("KH");

            string sqlThemKhach = "Insert Into KhachHang (MaKhachHang, TenKhachHang, SoDienThoai) " +
                "Values (@id, @name, @phone)";

            await s.ThucThiAsync(sqlThemKhach, new { id = maKhachHangMoi, name = input.TenKhachHang.Trim(), phone = input.SoDienThoai });

            return maKhachHangMoi;
        }

        // Số điện thoại đã có: họ tên phải khớp, tránh gắn xe vào nhầm khách
        string tenDaDangKy = khachHang[0].Chuoi("TenKhachHang").Trim();

        if (!tenDaDangKy.Equals(input.TenKhachHang.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Số điện thoại đã có trong danh sách khách hàng. Hãy nhập đúng họ tên đã đăng ký.");
        }

        return khachHang[0].Chuoi("MaKhachHang");
    }

    // Phân công (hoặc đổi thời gian) thợ cho xe của phiếu. Thợ chính tối đa 3 xe, thợ phụ 2 xe.
    public async Task PhanCong(PhienGiaoDich s, string id, YeuCauPhanCong input)
    {
        if (input.KetThuc <= input.BatDau || input.KetThuc - input.BatDau > TimeSpan.FromDays(90))
        {
            throw new ArgumentException("Thời gian kết thúc phải sau bắt đầu, tối đa 90 ngày.");
        }

        string sqlPhieu = "Select MaXe, TrangThai, NgayLap " +
            "From PhieuTiepNhan With (UpdLock, HoldLock) " +
            "Where MaPhieuTiepNhan = @id";

        Dictionary<string, object?> phieu = (await s.DocBangAsync(sqlPhieu, new { id })).SingleOrDefault()
            ?? throw new KeyNotFoundException("Không tìm thấy phiếu.");

        if (phieu.Chuoi("TrangThai") is not ("Chờ sửa" or "Đang sửa"))
        {
            throw new InvalidOperationException("Chỉ phân công phiếu chưa hoàn tất.");
        }

        if (input.BatDau.Date < (DateTime)phieu["NgayLap"]!)
        {
            throw new ArgumentException("Thời gian phân công không được trước ngày lập phiếu.");
        }

        string sqlTho = "Select c.TenChucVu " +
            "From NhanVien n With (UpdLock, HoldLock) Join ChucVu c On c.MaChucVu = n.MaChucVu " +
            "Where n.MaNhanVien = @employee And n.TrangThai = 1 And c.TenChucVu Like N'Thợ%'";

        List<Dictionary<string, object?>> tho = await s.DocBangAsync(sqlTho, new { employee = input.MaNhanVien });

        if (tho.Count != 1)
        {
            throw new InvalidOperationException("Chọn thợ đang làm việc.");
        }

        // Đếm số xe khác mà thợ đang nhận
        string maXe = phieu.Chuoi("MaXe");

        string sqlDemXe = "Select Count(Distinct c.MaXe) " +
            "From ChiTietPhanCong c With (UpdLock, HoldLock) Join PhieuTiepNhan p On p.MaXe = c.MaXe " +
            "Where c.MaNhanVien = @employee And c.MaXe <> @vehicle And p.TrangThai In (N'Chờ sửa', N'Đang sửa')";

        int soXeDangNhan = await s.DocGiaTriAsync<int>(sqlDemXe, new { employee = input.MaNhanVien, vehicle = maXe });
        bool laThoChinh = tho[0].Chuoi("TenChucVu").Contains("chính", StringComparison.OrdinalIgnoreCase);
        int soXeToiDa = laThoChinh ? 3 : 2;

        if (soXeDangNhan >= soXeToiDa)
        {
            throw new InvalidOperationException("Thợ đã nhận đủ số xe đang xử lý. Vui lòng chọn thợ khác.");
        }

        // Đã phân công thì cập nhật thời gian, chưa có thì thêm mới
        const string sqlLuuPhanCong = """
            IF EXISTS (SELECT 1 FROM ChiTietPhanCong WHERE MaNhanVien = @employee AND MaXe = @vehicle)
                UPDATE ChiTietPhanCong
                SET ThoiGianBatDau = @start, ThoiGianHoanThanh = @end
                WHERE MaNhanVien = @employee AND MaXe = @vehicle
            ELSE
                INSERT ChiTietPhanCong (MaNhanVien, MaXe, ThoiGianBatDau, ThoiGianHoanThanh)
                VALUES (@employee, @vehicle, @start, @end)
            """;

        await s.ThucThiAsync(sqlLuuPhanCong, new { employee = input.MaNhanVien, vehicle = maXe, start = input.BatDau, end = input.KetThuc });
    }

    // Luồng trạng thái: Chờ sửa → Đang sửa → Hoàn thành; chỉ hủy khi Chờ sửa (hủy thì hoàn kho).
    public Task<bool> DoiTrangThaiAsync(string id, string next, ClaimsPrincipal user)
    {
        return db.TrongGiaoDichAsync(async s =>
        {
            string sqlPhieu = "Select MaXe, TrangThai " +
                "From PhieuTiepNhan With (UpdLock, HoldLock) " +
                "Where MaPhieuTiepNhan = @id";

            Dictionary<string, object?> phieu = (await s.DocBangAsync(sqlPhieu, new { id })).SingleOrDefault()
                ?? throw new KeyNotFoundException("Không tìm thấy phiếu tiếp nhận.");

            string maXe = phieu.Chuoi("MaXe");
            bool laTho = user.IsInRole("Mechanic");

            // 1. Thợ chỉ cập nhật xe mình được phân công
            if (laTho)
            {
                string sqlDuocPhanCong = "Select Count(*) From ChiTietPhanCong Where MaXe = @vehicle And MaNhanVien = @employee";
                int duocPhanCong = await s.DocGiaTriAsync<int>(sqlDuocPhanCong, new { vehicle = maXe, employee = PhanQuyenNguoiDung.MaNhanVien(user) });

                if (duocPhanCong == 0)
                {
                    throw new UnauthorizedAccessException("Bạn chỉ được cập nhật xe được phân công.");
                }
            }

            // 2. Chỉ cho chuyển theo đúng luồng
            string trangThaiCu = phieu.Chuoi("TrangThai");
            bool hopLe = (trangThaiCu == "Chờ sửa" && next == "Đang sửa")
                || (trangThaiCu == "Đang sửa" && next == "Hoàn thành")
                || (trangThaiCu == "Chờ sửa" && next == "Đã hủy" && !laTho);

            if (!hopLe)
            {
                throw new InvalidOperationException("Chuyển trạng thái không hợp lệ. Luồng: Chờ sửa → Đang sửa → Hoàn thành; chỉ hủy khi Chờ sửa.");
            }

            // 3. Bắt đầu sửa thì phải có thợ
            if (next == "Đang sửa")
            {
                int soTho = await s.DocGiaTriAsync<int>("Select Count(*) From ChiTietPhanCong Where MaXe = @id", new { id = maXe });

                if (soTho == 0)
                {
                    throw new InvalidOperationException("Cần phân công thợ trước khi bắt đầu sửa.");
                }
            }

            // 4. Hủy phiếu: hoàn lại phụ tùng theo bản chốt lúc tiếp nhận
            if (next == "Đã hủy")
            {
                int laPhieuWeb = await s.DocGiaTriAsync<int>("Select Count(*) From WebPhieuTiepNhan Where MaPhieuTiepNhan = @id", new { id });

                if (laPhieuWeb == 0)
                {
                    throw new InvalidOperationException("Phiếu tạo từ ứng dụng khác chưa có bản chốt xuất kho. Hãy hủy tại ứng dụng đã tạo phiếu để tránh hoàn kho sai.");
                }

                List<Dictionary<string, object?>> phuTungDaXuat = await s.DocBangAsync(
                    "Select MaPhuTung, SoLuong From WebPhieuPhuTung Where MaPhieuTiepNhan = @id", new { id });

                foreach (Dictionary<string, object?> phuTung in phuTungDaXuat)
                {
                    await s.ThucThiAsync(
                        "Update PhuTung Set SoLuongTon = SoLuongTon + @quantity Where MaPhuTung = @part",
                        new { quantity = phuTung.SoNguyen("SoLuong"), part = phuTung.Chuoi("MaPhuTung") });
                }
            }

            // 5. Lưu trạng thái mới
            await s.ThucThiAsync("Update PhieuTiepNhan Set TrangThai = @next Where MaPhieuTiepNhan = @id", new { id, next });

            return true;
        });
    }
}
