using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;

// Người 1 tổng hợp: bộ thêm/sửa/xóa dùng chung cho mọi danh mục khai báo trong DinhNghiaDanhMuc.
// Mỗi người sửa hàm Kiểm tra... thuộc phần mình; Them/Sua/Xoa/Luu là phần dùng chung.
[ApiController, Authorize, Route("api/catalog")]
public sealed class DanhMucController(XuLyDuLieu db) : ControllerBase
{
    private const string TrangThaiDangXuLy = "(N'Chờ sửa', N'Đang sửa')";

    // Cập nhật lại tổng tiền điều chỉnh của mọi chức vụ sau khi đổi khoản thưởng/phạt.
    private const string SqlTinhLaiTienDieuChinh = """
        UPDATE c
        SET TienDieuChinh = COALESCE((
            SELECT SUM(k.SoTien)
            FROM ChucVuDieuChinh d
            JOIN KhoanDieuChinh k ON k.MaKhoanDieuChinh = d.MaKhoanDieuChinh
            WHERE d.MaChucVu = c.MaChucVu), 0)
        FROM ChucVu c
        """;

    // ===== Đọc danh mục =====

    // Danh sách danh mục mà tài khoản hiện tại được dùng (giao diện dựa vào đây để vẽ form).
    [HttpGet("schema")]
    public IActionResult DinhNghia()
    {
        var danhSach = DinhNghiaDanhMuc.TatCa
            .Where(x => DuocDung(x.Value))
            .Select(x => new { id = x.Key, title = x.Value.TieuDe, keys = x.Value.Khoa, fields = x.Value.CacTruong });

        return Ok(danhSach);
    }

    [HttpGet("{kind}")]
    public async Task<IActionResult> DanhSach(string kind, string? q = null, int page = 1, int pageSize = 12)
    {
        CauHinhDanhMuc cauHinh = CauHinh(kind);
        PhanTrang.KiemTra(page, pageSize, q);

        // Không bao giờ trả cột mật khẩu
        List<TruongDanhMuc> cacTruong = cauHinh.CacTruong.Where(f => f.Kieu != "password").ToList();
        string cacCot = string.Join(", ", cacTruong.Select(f => $"[{f.Ten}]"));
        string cotSapXep = string.Join(", ", cauHinh.Khoa.Select(k => $"[{k}]"));

        // Tìm kiếm trên các cột chữ, mã, số điện thoại
        List<string> dieuKienTim = cacTruong
            .Where(f => f.Kieu is "text" or "code" or "phone")
            .Select(f => $"[{f.Ten}] Like @q")
            .ToList();

        if (kind == "customers")
        {
            var plate = BienSoXe.KhoaSql("x.BienXe");
            var plateQuery = BienSoXe.KhoaSql("@query");
            cacCot += $", (SELECT COUNT(DISTINCT {plate}) FROM Xe x WHERE x.MaKhachHang=KhachHang.MaKhachHang) AS vehicleCount";
            dieuKienTim.Add($"EXISTS (SELECT 1 FROM Xe x WHERE x.MaKhachHang=KhachHang.MaKhachHang AND {plate} LIKE '%' + NULLIF({plateQuery}, '') + '%')");
        }

        string dieuKienLoc = dieuKienTim.Count > 0 ? " Where (" + string.Join(" Or ", dieuKienTim) + ")" : "";

        var query = (q ?? "").Trim();
        var thamSo = new
        {
            q = "%" + query + "%",
            query,
            skip = (page - 1) * pageSize,
            size = pageSize
        };

        string sqlDem = $"Select Count(*) From [{cauHinh.Bang}]" + dieuKienLoc;

        string sqlDanhSach = $"Select {cacCot} From [{cauHinh.Bang}]" + dieuKienLoc +
            $" Order By {cotSapXep} " +
            "Offset @skip Rows Fetch Next @size Rows Only";

        int tongSoDong = await db.DocGiaTriAsync<int>(sqlDem, thamSo);
        List<Dictionary<string, object?>> danhSachDong = await db.DocBangAsync(sqlDanhSach, thamSo);

        return Ok(new
        {
            items = danhSachDong,
            total = tongSoDong,
            page,
            totalPages = Math.Max(1, (int)Math.Ceiling(tongSoDong / (double)pageSize))
        });
    }

    [HttpGet("customers/{customerId}/vehicles")]
    public async Task<IActionResult> XeCuaKhach(string customerId)
    {
        CauHinh("customers");
        var rows = await db.DocBangAsync($$"""
            WITH XeTheoBienSo AS (
                SELECT x.BienXe, x.LoaiXe, x.HangSanXuat, x.MauSac,
                       ROW_NUMBER() OVER (
                           PARTITION BY {{BienSoXe.KhoaSql("x.BienXe")}}
                           ORDER BY p.NgayLap DESC, x.MaXe DESC
                       ) AS rn
                FROM Xe x
                OUTER APPLY (SELECT MAX(NgayLap) AS NgayLap FROM PhieuTiepNhan WHERE MaXe=x.MaXe) p
                WHERE x.MaKhachHang=@customerId
            )
            SELECT BienXe, LoaiXe, HangSanXuat, MauSac
            FROM XeTheoBienSo
            WHERE rn=1
            ORDER BY BienXe
            """, new { customerId });
        return Ok(rows);
    }

    // Danh sách cho ô chọn: chỉ trả mã và nhãn, không trả thông tin nhân sự hoặc lương.
    [HttpGet("options/{kind}")]
    public async Task<IActionResult> DanhSachChon(string kind)
    {
        bool laQuanLy = User.IsInRole("Manager");
        bool laThuNgan = User.IsInRole("Cashier");

        string sql = kind switch
        {
            "customers" when laQuanLy || laThuNgan =>
                "Select MaKhachHang As id, TenKhachHang + N' · ' + SoDienThoai As label From KhachHang Order By TenKhachHang",
            "services" =>
                "Select MaDichVu As id, TenDichVu As label From DichVu Where TrangThai = 1 Order By TenDichVu",
            "parts" =>
                "Select MaPhuTung As id, TenPhuTung + N' · tồn ' + Cast(SoLuongTon As nvarchar(15)) As label From PhuTung Where TrangThai = 1 Order By TenPhuTung",
            "suppliers" when laQuanLy =>
                "Select MaNhaCungCap As id, TenNhaCungCap As label From NhaCungCap Order By TenNhaCungCap",
            "employees" when laQuanLy =>
                "Select MaNhanVien As id, HoTen As label From NhanVien Where TrangThai = 1 Order By HoTen",
            "mechanics" =>
                "Select n.MaNhanVien As id, n.HoTen As label From NhanVien n Join ChucVu c On c.MaChucVu = n.MaChucVu " +
                "Where n.TrangThai = 1 And c.TenChucVu Like N'Thợ%' Order By n.HoTen",
            "roles" when laQuanLy =>
                "Select MaChucVu As id, TenChucVu As label From ChucVu Order By MaChucVu",
            "adjustments" when laQuanLy =>
                "Select MaKhoanDieuChinh As id, TenKhoanDieuChinh As label From KhoanDieuChinh Order By MaKhoanDieuChinh",
            "shifts" =>
                "Select MaCaLam As id, MaCaLam + N' · ' + Convert(nvarchar(5), ThoiGianBatDau) + N'–' + Convert(nvarchar(5), ThoiGianKetThuc) As label " +
                "From CaLam Order By ThoiGianBatDau",
            _ => throw new UnauthorizedAccessException("Danh sách không khả dụng với tài khoản này.")
        };

        return Ok(await db.DocBangAsync(sql));
    }

    // ===== Thêm / sửa / xóa =====

    [HttpPost("{kind}")]
    public Task<IActionResult> Them(string kind, Dictionary<string, JsonElement> input)
    {
        return Luu(kind, null, input);
    }

    [HttpPut("{kind}/{key}")]
    public Task<IActionResult> Sua(string kind, string key, Dictionary<string, JsonElement> input)
    {
        return Luu(kind, key, input);
    }

    // key = null: thêm mới; key = mã bản ghi (khóa nhiều cột nối bằng ~): sửa.
    private async Task<IActionResult> Luu(string kind, string? key, Dictionary<string, JsonElement> input)
    {
        CauHinhDanhMuc cauHinh = CauHinh(kind);
        bool themMoi = key == null;
        Dictionary<string, object?> data = DocDuLieuNhap(cauHinh, input, themMoi);

        await db.TrongGiaoDichAsync(async s =>
        {
            Dictionary<string, object?> giaTriKhoa = DocKhoa(cauHinh, key, data);
            string dieuKienKhoa = DieuKienKhoa(cauHinh);

            // 1. Khi sửa: đọc và khóa bản ghi cũ
            Dictionary<string, object?>? old = null;

            if (!themMoi)
            {
                string sqlBanGhiCu = $"Select * From [{cauHinh.Bang}] With (UpdLock, HoldLock) Where {dieuKienKhoa}";
                old = (await s.DocBangAsync(sqlBanGhiCu, giaTriKhoa)).SingleOrDefault()
                    ?? throw new KeyNotFoundException("Bản ghi không còn tồn tại.");
            }

            // 2. Kiểm tra nghiệp vụ riêng của từng danh mục
            await KiemTraTheoDanhMuc(s, kind, old, data);

            // 3. Ghi xuống CSDL
            if (themMoi)
            {
                string cacCot = string.Join(", ", data.Keys.Select(k => $"[{k}]"));
                string cacThamSo = string.Join(", ", data.Keys.Select(k => "@" + k));

                await s.ThucThiAsync($"Insert Into [{cauHinh.Bang}] ({cacCot}) Values ({cacThamSo})", data);
            }
            else
            {
                // Không cập nhật cột khóa; giá trị khóa đưa vào tham số @k0, @k1...
                foreach (string cotKhoa in cauHinh.Khoa)
                {
                    data.Remove(cotKhoa);
                }

                string cacCotCapNhat = string.Join(", ", data.Keys.Select(k => $"[{k}] = @{k}"));

                foreach (KeyValuePair<string, object?> cap in giaTriKhoa)
                {
                    data.Add(cap.Key, cap.Value);
                }

                await s.ThucThiAsync($"Update [{cauHinh.Bang}] Set {cacCotCapNhat} Where {dieuKienKhoa}", data);
            }

            // 4. Khoản thưởng/phạt thay đổi thì tính lại tiền điều chỉnh của chức vụ
            if (kind is "adjustments" or "roleAdjustments")
            {
                await s.ThucThiAsync(SqlTinhLaiTienDieuChinh);
            }

            return true;
        });

        return StatusCode(themMoi ? 201 : 200, new { message = "Đã lưu dữ liệu." });
    }

    [HttpDelete("{kind}/{key}")]
    public async Task<IActionResult> Xoa(string kind, string key)
    {
        CauHinhDanhMuc cauHinh = CauHinh(kind);

        await db.TrongGiaoDichAsync(async s =>
        {
            Dictionary<string, object?> giaTriKhoa = DocKhoa(cauHinh, key, []);

            if (kind == "employees" && key == PhanQuyenNguoiDung.MaNhanVien(User))
            {
                throw new InvalidOperationException("Không thể xóa tài khoản đang đăng nhập.");
            }

            if (kind == "serviceParts")
            {
                string maDichVu = key.Split('~')[0];

                if (await DemPhieuDangXuLyCuaDichVu(s, maDichVu) > 0)
                {
                    throw new InvalidOperationException("Không xóa định mức đang được dùng cho xe chưa hoàn thành.");
                }
            }

            // Dịch vụ có nội dung web (mô tả, ảnh) thì xóa phần đó trước
            if (kind == "services")
            {
                await s.ThucThiAsync("Delete WebDichVu Where MaDichVu = @k0", giaTriKhoa);
            }

            // Bản ghi đang được dùng ở bảng khác sẽ bị khóa ngoại chặn lại (báo lỗi 409)
            int soDongXoa = await s.ThucThiAsync($"Delete [{cauHinh.Bang}] Where {DieuKienKhoa(cauHinh)}", giaTriKhoa);

            if (soDongXoa == 0)
            {
                throw new KeyNotFoundException("Không tìm thấy bản ghi.");
            }

            if (kind is "adjustments" or "roleAdjustments")
            {
                await s.ThucThiAsync(SqlTinhLaiTienDieuChinh);
            }

            return true;
        });

        return Ok(new { message = "Đã xóa bản ghi." });
    }

    // ===== Hàm dùng chung =====

    private bool DuocDung(CauHinhDanhMuc cauHinh)
    {
        return cauHinh.VaiTro.Split(',').Any(User.IsInRole);
    }

    private CauHinhDanhMuc CauHinh(string kind)
    {
        if (!DinhNghiaDanhMuc.TatCa.TryGetValue(kind, out CauHinhDanhMuc? cauHinh))
        {
            throw new KeyNotFoundException("Không có danh mục này.");
        }

        if (!DuocDung(cauHinh))
        {
            throw new UnauthorizedAccessException("Bạn không có quyền quản lý danh mục này.");
        }

        return cauHinh;
    }

    // Ví dụ khóa 2 cột: "[MaDichVu] = @k0 And [MaPhuTung] = @k1".
    private static string DieuKienKhoa(CauHinhDanhMuc cauHinh)
    {
        return string.Join(" And ", cauHinh.Khoa.Select((cot, i) => $"[{cot}] = @k{i}"));
    }

    // Giá trị khóa: lấy từ key trên URL (sửa/xóa) hoặc từ dữ liệu nhập (thêm mới).
    private static Dictionary<string, object?> DocKhoa(CauHinhDanhMuc cauHinh, string? key, Dictionary<string, object?> data)
    {
        string[]? cacPhanKhoa = key?.Split('~');

        if (cacPhanKhoa != null && cacPhanKhoa.Length != cauHinh.Khoa.Length)
        {
            throw new ArgumentException("Mã bản ghi không hợp lệ.");
        }

        Dictionary<string, object?> giaTriKhoa = new Dictionary<string, object?>();

        for (int i = 0; i < cauHinh.Khoa.Length; i++)
        {
            giaTriKhoa["k" + i] = cacPhanKhoa == null ? data[cauHinh.Khoa[i]] : cacPhanKhoa[i];
        }

        return giaTriKhoa;
    }

    // Đọc và kiểm tra dữ liệu nhập theo kiểu của từng trường. Khi sửa thì bỏ qua cột khóa.
    private static Dictionary<string, object?> DocDuLieuNhap(CauHinhDanhMuc cauHinh, Dictionary<string, JsonElement> input, bool themMoi)
    {
        Dictionary<string, object?> ketQua = new Dictionary<string, object?>();

        IEnumerable<TruongDanhMuc> truongCanDoc = cauHinh.CacTruong
            .Where(f => !f.ChiDoc && (themMoi || !cauHinh.Khoa.Contains(f.Ten)));

        foreach (TruongDanhMuc truong in truongCanDoc)
        {
            JsonElement giaTriGoc = input.GetValueOrDefault(truong.Ten);
            string chuoi = giaTriGoc.ValueKind == JsonValueKind.Undefined ? "" : giaTriGoc.ToString().Trim();

            if (chuoi.Length == 0)
            {
                if (truong.KhongBatBuoc)
                {
                    continue;
                }

                throw new ArgumentException($"Vui lòng nhập {truong.Nhan.ToLowerInvariant()}.");
            }

            ketQua[truong.Ten] = truong.Kieu switch
            {
                "money" or "factor" or "signedMoney" or "positiveInt" or "int" or "hours" => DocSo(truong, chuoi),
                "bool" => chuoi.Equals("true", StringComparison.OrdinalIgnoreCase) || chuoi == "1",
                "date" => DocNgay(chuoi),
                "time" => DocGio(chuoi),
                _ => DocChuoi(truong, chuoi)
            };
        }

        return ketQua;
    }

    private static decimal DocSo(TruongDanhMuc truong, string chuoi)
    {
        if (!decimal.TryParse(chuoi, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal so) || Math.Abs(so) > 100000000000m)
        {
            throw new ArgumentException($"{truong.Nhan} không hợp lệ.");
        }

        bool ngoaiKhoang = truong.Kieu switch
        {
            "money" or "positiveInt" => so <= 0,
            "factor" => so <= 0 || so >= 100,
            "hours" => so < 0 || so > 24,
            "signedMoney" => so == 0,
            _ => false
        };

        if (ngoaiKhoang)
        {
            throw new ArgumentException($"{truong.Nhan} ngoài khoảng cho phép.");
        }

        bool phaiLaSoNguyen = truong.Kieu is "money" or "signedMoney" or "positiveInt" or "int";

        if (phaiLaSoNguyen && so != decimal.Truncate(so))
        {
            throw new ArgumentException($"{truong.Nhan} phải là số nguyên.");
        }

        if (truong.Kieu == "positiveInt" && so > 10000)
        {
            throw new ArgumentException("Số lượng tối đa 10.000.");
        }

        return so;
    }

    private static DateTime DocNgay(string chuoi)
    {
        if (!DateTime.TryParseExact(chuoi, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime ngay))
        {
            throw new ArgumentException("Ngày không hợp lệ.");
        }

        return ngay;
    }

    private static TimeSpan DocGio(string chuoi)
    {
        if (!TimeSpan.TryParse(chuoi, CultureInfo.InvariantCulture, out TimeSpan gio) || gio < TimeSpan.Zero || gio >= TimeSpan.FromDays(1))
        {
            throw new ArgumentException("Giờ không hợp lệ.");
        }

        return gio;
    }

    // Chữ, mã, số điện thoại, mật khẩu (mật khẩu được băm trước khi lưu).
    private static string DocChuoi(TruongDanhMuc truong, string chuoi)
    {
        if (chuoi.Length > truong.DoDaiToiDa)
        {
            throw new ArgumentException($"{truong.Nhan} tối đa {truong.DoDaiToiDa} ký tự.");
        }

        if (truong.Kieu == "code" && !Regex.IsMatch(chuoi, @"^[A-Za-z0-9_-]{1,10}$"))
        {
            throw new ArgumentException("Mã chỉ gồm chữ không dấu, số, gạch dưới hoặc gạch ngang.");
        }

        if (truong.Kieu == "phone" && !Regex.IsMatch(chuoi, @"^0[0-9]{8,10}$"))
        {
            throw new ArgumentException("Số điện thoại gồm 9–11 chữ số, bắt đầu bằng 0.");
        }

        if (truong.Kieu == "password")
        {
            if (chuoi.Length < 6)
            {
                throw new ArgumentException("Mật khẩu tối thiểu 6 ký tự.");
            }

            return XuLyDuLieu.BamMatKhau(chuoi);
        }

        return chuoi;
    }

    private static async Task<int> DemPhieuDangXuLyCuaDichVu(PhienGiaoDich s, string maDichVu)
    {
        string sqlDem = "Select Count(*) " +
            "From ChiTietPhieuTiepNhan d Join PhieuTiepNhan p On p.MaPhieuTiepNhan = d.MaPhieuTiepNhan " +
            "Where d.MaDichVu = @id And p.TrangThai In " + TrangThaiDangXuLy;

        return await s.DocGiaTriAsync<int>(sqlDem, new { id = maDichVu });
    }

    // ===== Kiểm tra nghiệp vụ theo từng danh mục =====

    private async Task KiemTraTheoDanhMuc(PhienGiaoDich s, string kind, Dictionary<string, object?>? old, Dictionary<string, object?> data)
    {
        switch (kind)
        {
            case "parts":
                await KiemTraPhuTung(s, old, data);
                break;
            case "vehicles":
                await KiemTraXe(s, old);
                break;
            case "serviceParts":
                await KiemTraDinhMuc(s, old, data);
                break;
            case "employees":
                await KiemTraNhanVien(s, old, data);
                break;
            case "roles":
                await KiemTraChucVu(s, old, data);
                break;
            case "shifts":
                await KiemTraCaLam(s, old, data);
                break;
            case "attendance":
                await KiemTraChamCong(s, old, data);
                break;
        }
    }

    // Người 2: xe đã có phiếu tiếp nhận thì không sửa, để giữ đúng lịch sử.
    private static async Task KiemTraXe(PhienGiaoDich s, Dictionary<string, object?>? old)
    {
        if (old == null)
        {
            return;
        }

        int soPhieu = await s.DocGiaTriAsync<int>("Select Count(*) From PhieuTiepNhan Where MaXe = @id", new { id = old.Chuoi("MaXe") });

        if (soPhieu > 0)
        {
            throw new InvalidOperationException("Xe đã có phiếu tiếp nhận. Không sửa thông tin lịch sử; tạo lượt xe mới khi tiếp nhận lần tiếp theo.");
        }
    }

    // Người 1: nhân viên mới phải có mật khẩu; không tự khóa/đổi quyền chính mình; còn xe đang sửa thì không khóa/đổi quyền.
    private async Task KiemTraNhanVien(PhienGiaoDich s, Dictionary<string, object?>? old, Dictionary<string, object?> data)
    {
        if (old == null && !data.ContainsKey("MatKhau"))
        {
            throw new ArgumentException("Nhập mật khẩu từ 6 ký tự cho nhân viên mới.");
        }

        string sqlTenChucVu = "Select TenChucVu From ChucVu Where MaChucVu = @id";
        string? chucVuMoi = await s.DocGiaTriAsync<string>(sqlTenChucVu, new { id = data["MaChucVu"] });

        if (chucVuMoi == null)
        {
            throw new ArgumentException("Chọn chức vụ hợp lệ.");
        }

        if (old == null)
        {
            return;
        }

        string? chucVuCu = await s.DocGiaTriAsync<string>(sqlTenChucVu, new { id = old.Chuoi("MaChucVu") });
        bool khoaTaiKhoan = !(bool)data["TrangThai"]!;
        bool doiNhomQuyen = PhanQuyenNguoiDung.XacDinhVaiTro(chucVuMoi) != PhanQuyenNguoiDung.XacDinhVaiTro(chucVuCu ?? "");

        if (!khoaTaiKhoan && !doiNhomQuyen)
        {
            return;
        }

        string maNhanVien = old.Chuoi("MaNhanVien");

        if (maNhanVien == PhanQuyenNguoiDung.MaNhanVien(User))
        {
            throw new InvalidOperationException("Không thể tự khóa hoặc đổi nhóm quyền tài khoản đang đăng nhập.");
        }

        string sqlXeDangSua = "Select Count(*) " +
            "From ChiTietPhanCong c Join PhieuTiepNhan p On p.MaXe = c.MaXe " +
            "Where c.MaNhanVien = @id And p.TrangThai In " + TrangThaiDangXuLy;

        if (await s.DocGiaTriAsync<int>(sqlXeDangSua, new { id = maNhanVien }) > 0)
        {
            throw new InvalidOperationException("Nhân viên còn xe đang xử lý. Hoàn tất hoặc chuyển phân công trước.");
        }
    }

    // Người 4: kiểm tra khi thêm/sửa lô phụ tùng.
    // old = dữ liệu đang lưu (null khi thêm mới); data = dữ liệu người dùng gửi lên.
    private async Task KiemTraPhuTung(PhienGiaoDich s, Dictionary<string, object?>? old, Dictionary<string, object?> data)
    {
        DateTime hanSuDungMoi = (DateTime)data["HanSuDung"]!;

        // 1. Thêm lô mới: tồn ban đầu luôn bằng 0, hàng chỉ vào kho qua phiếu nhập
        if (old == null)
        {
            data["SoLuongTon"] = 0;

            if (hanSuDungMoi < XuLyDuLieu.HomNay)
            {
                throw new ArgumentException("Không tạo lô phụ tùng đã hết hạn sử dụng.");
            }

            return;
        }

        string maPhuTung = old.Chuoi("MaPhuTung");

        // 2. Sửa lô: nhà cung cấp, hạn dùng, giá nhập là thông tin của lô
        bool doiNhaCungCap = !string.Equals(Convert.ToString(data["MaNhaCungCap"]), old.Chuoi("MaNhaCungCap"), StringComparison.OrdinalIgnoreCase);
        bool doiHanSuDung = hanSuDungMoi != Convert.ToDateTime(old["HanSuDung"]);
        bool doiGiaNhap = (decimal)data["DonGiaNhap"]! != old.SoTien("DonGiaNhap");

        if (doiNhaCungCap || doiHanSuDung || doiGiaNhap)
        {
            if (await LoPhuTungDaSuDung(s, maPhuTung, old.SoNguyen("SoLuongTon")))
            {
                throw new InvalidOperationException("Lô phụ tùng đã sử dụng (có tồn, nhập kho, kiểm kê hoặc xuất sửa): không đổi nhà cung cấp, hạn dùng hay giá nhập; hãy tạo mã lô mới.");
            }

            if (doiHanSuDung && hanSuDungMoi < XuLyDuLieu.HomNay)
            {
                throw new ArgumentException("Hạn sử dụng mới không được trước hôm nay.");
            }
        }

        // 3. Giá bán được phép đổi, nhưng phải lưu lịch sử giá bán
        decimal giaBanCu = old.SoTien("DonGiaBan");
        decimal giaBanMoi = (decimal)data["DonGiaBan"]!;

        if (giaBanMoi != giaBanCu)
        {
            string sqlLuuGiaBan = "Insert Into DieuChinhGiaBan (MaDieuChinhGia, MaPhuTung, MaNhanVien, NgayDieuChinh, GiaBanCu, GiaBanMoi, LyDo) " +
                "Values (@MaDieuChinhGia, @MaPhuTung, @MaNhanVien, @NgayDieuChinh, @GiaBanCu, @GiaBanMoi, N'Cập nhật từ website')";

            await s.ThucThiAsync(sqlLuuGiaBan, new
            {
                MaDieuChinhGia = XuLyDuLieu.TaoMaMoi("DG"),
                MaPhuTung = maPhuTung,
                MaNhanVien = PhanQuyenNguoiDung.MaNhanVien(User),
                NgayDieuChinh = XuLyDuLieu.HomNay,
                GiaBanCu = giaBanCu,
                GiaBanMoi = giaBanMoi
            });
        }
    }

    // Lô "đã sử dụng": còn tồn, đã có phiếu nhập, đã kiểm kê hoặc đã xuất cho phiếu sửa.
    private static async Task<bool> LoPhuTungDaSuDung(PhienGiaoDich s, string maPhuTung, int soLuongTon)
    {
        if (soLuongTon > 0)
        {
            return true;
        }

        string sqlKiemTra = """
            SELECT CASE
                WHEN EXISTS (SELECT 1 FROM ChiTietPhieuNhap WHERE MaPhuTung = @MaPhuTung) THEN 1
                WHEN EXISTS (SELECT 1 FROM DieuChinhTonKho WHERE MaPhuTung = @MaPhuTung) THEN 1
                WHEN EXISTS (SELECT 1 FROM WebPhieuPhuTung WHERE MaPhuTung = @MaPhuTung) THEN 1
                ELSE 0
            END
            """;

        int daSuDung = await s.DocGiaTriAsync<int>(sqlKiemTra, new { MaPhuTung = maPhuTung });

        return daSuDung == 1;
    }

    // Người 3: không đổi định mức khi dịch vụ đang có xe chờ sửa/đang sửa.
    private static async Task KiemTraDinhMuc(PhienGiaoDich s, Dictionary<string, object?>? old, Dictionary<string, object?> data)
    {
        string maDichVu = old?.Chuoi("MaDichVu") ?? Convert.ToString(data["MaDichVu"]) ?? "";

        if (await DemPhieuDangXuLyCuaDichVu(s, maDichVu) > 0)
        {
            throw new InvalidOperationException("Dịch vụ có xe đang sửa/chờ sửa. Chỉ thay đổi định mức khi các phiếu này kết thúc.");
        }
    }

    // Người 1: chức vụ mới có tiền điều chỉnh 0; chức vụ đang có người thì không đổi nhóm quyền qua tên.
    private static async Task KiemTraChucVu(PhienGiaoDich s, Dictionary<string, object?>? old, Dictionary<string, object?> data)
    {
        if (old == null)
        {
            data["TienDieuChinh"] = 0;
            return;
        }

        string vaiTroCu = PhanQuyenNguoiDung.XacDinhVaiTro(old.Chuoi("TenChucVu"));
        string vaiTroMoi = PhanQuyenNguoiDung.XacDinhVaiTro((string)data["TenChucVu"]!);

        if (vaiTroCu == vaiTroMoi)
        {
            return;
        }

        int soNhanVien = await s.DocGiaTriAsync<int>("Select Count(*) From NhanVien Where MaChucVu = @id", new { id = old.Chuoi("MaChucVu") });

        if (soNhanVien > 0)
        {
            throw new InvalidOperationException("Chức vụ đang được nhân viên sử dụng; không đổi nhóm quyền bằng cách đổi tên chức vụ.");
        }
    }

    // Người 1: giờ kết thúc sau giờ bắt đầu; tự tính số giờ ca; ca đã chấm công thì không sửa.
    private static async Task KiemTraCaLam(PhienGiaoDich s, Dictionary<string, object?>? old, Dictionary<string, object?> data)
    {
        TimeSpan gioBatDau = (TimeSpan)data["ThoiGianBatDau"]!;
        TimeSpan gioKetThuc = (TimeSpan)data["ThoiGianKetThuc"]!;

        if (gioKetThuc <= gioBatDau)
        {
            throw new ArgumentException("Giờ kết thúc phải sau giờ bắt đầu.");
        }

        data["ThoiGianCaLam"] = (decimal)(gioKetThuc - gioBatDau).TotalHours;

        if (old == null)
        {
            return;
        }

        int soLanChamCong = await s.DocGiaTriAsync<int>("Select Count(*) From ChiTietCaLam Where MaCaLam = @id", new { id = old.Chuoi("MaCaLam") });

        if (soLanChamCong > 0)
        {
            throw new InvalidOperationException("Ca đã được chấm công. Hãy tạo ca mới để giữ đúng lịch sử.");
        }
    }

    // Người 1: không chấm công tương lai, không vượt thời lượng ca, không trùng giờ với ca khác cùng ngày.
    private static async Task KiemTraChamCong(PhienGiaoDich s, Dictionary<string, object?>? old, Dictionary<string, object?> data)
    {
        // Khi sửa, cột khóa (nhân viên, ca, ngày) không có trong data nên ghép với bản ghi cũ
        Dictionary<string, object?> banGhi = new Dictionary<string, object?>(old ?? new Dictionary<string, object?>());

        foreach (KeyValuePair<string, object?> cap in data)
        {
            banGhi[cap.Key] = cap.Value;
        }

        if ((DateTime)banGhi["NgayLam"]! > XuLyDuLieu.HomNay)
        {
            throw new ArgumentException("Không chấm công cho ngày tương lai.");
        }

        decimal soGioCa = await s.DocGiaTriAsync<decimal>("Select ThoiGianCaLam From CaLam Where MaCaLam = @id", new { id = banGhi["MaCaLam"] });

        if ((decimal)data["TongThoiGianLamThucTe"]! > soGioCa)
        {
            throw new ArgumentException("Số giờ thực tế không được vượt thời lượng ca.");
        }

        // Hai ca giao nhau khi ca này bắt đầu trước lúc ca kia kết thúc và ngược lại
        string sqlCaTrungGio = "Select Count(*) " +
            "From ChiTietCaLam a " +
            "Join CaLam c On c.MaCaLam = a.MaCaLam " +
            "Join CaLam n On n.MaCaLam = @shift " +
            "Where a.MaNhanVien = @id And a.NgayLam = @date And a.MaCaLam <> @shift " +
            "And c.ThoiGianBatDau < n.ThoiGianKetThuc And n.ThoiGianBatDau < c.ThoiGianKetThuc";

        int soCaTrung = await s.DocGiaTriAsync<int>(sqlCaTrungGio, new
        {
            id = banGhi["MaNhanVien"],
            date = banGhi["NgayLam"],
            shift = banGhi["MaCaLam"]
        });

        if (soCaTrung > 0)
        {
            throw new InvalidOperationException("Nhân viên đã chấm công ca trùng giờ.");
        }
    }
}
