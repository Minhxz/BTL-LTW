using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;

[ApiController, Authorize, Route("api/catalog")]
public sealed class DanhMucController(XuLyDuLieu duLieu, IDataProtectionProvider baoVeDuLieu)
    : ControllerBase
{
    private readonly ITimeLimitedDataProtector baoVeMa = baoVeDuLieu
        .CreateProtector("DanhMuc.MaDaCap")
        .ToTimeLimitedDataProtector();
    private const string TrangThaiDangXuLy = "(N'Chờ sửa', N'Đang sửa')";
    private const string SqlTinhLaiTienDieuChinh = """
        UPDATE c
        SET TienDieuChinh = COALESCE((
            SELECT SUM(k.SoTien)
            FROM ChucVuDieuChinh d
            JOIN KhoanDieuChinh k ON k.MaKhoanDieuChinh = d.MaKhoanDieuChinh
            WHERE d.MaChucVu = c.MaChucVu), 0)
        FROM ChucVu c
        """;

    [HttpGet("schema")]
    public IActionResult DinhNghia()
    {
        var danhSach = DinhNghiaDanhMuc
            .TatCa.Where(phanTu => DuocDung(phanTu.Value))
            .Select(phanTu => new
            {
                id = phanTu.Key,
                title = phanTu.Value.TieuDe,
                keys = phanTu.Value.Khoa,
                fields = phanTu.Value.CacTruong,
            });
        return Ok(danhSach);
    }

    [HttpGet("{kind}")]
    public async Task<IActionResult> DanhSach(
        [FromRoute(Name = "kind")] string loaiDanhMuc,
        [FromQuery(Name = "q")] string? tuKhoa = null,
        [FromQuery(Name = "page")] int trang = 1,
        [FromQuery(Name = "pageSize")] int kichThuocTrang = 12
    )
    {
        CauHinhDanhMuc cauHinh = CauHinh(loaiDanhMuc);
        PhanTrang.KiemTra(trang, kichThuocTrang, tuKhoa);
        List<TruongDanhMuc> cacTruong = cauHinh
            .CacTruong.Where(truong => truong.Kieu != "password")
            .ToList();
        string cacCot = string.Join(", ", cacTruong.Select(truong => $"[{truong.Ten}]"));
        string cotSapXep = string.Join(", ", cauHinh.Khoa.Select(cot => $"[{cot}]"));
        List<string> dieuKienTim = cacTruong
            .Where(truong => truong.Kieu is "text" or "code" or "phone")
            .Select(truong => $"[{truong.Ten}] Like @q")
            .ToList();
        if (loaiDanhMuc == "customers")
        {
            var bieuThucBienSo = BienSoXe.KhoaSql("x.BienXe");
            var bieuThucTimBienSo = BienSoXe.KhoaSql("@query");
            cacCot +=
                $", (SELECT COUNT(DISTINCT {bieuThucBienSo}) FROM Xe x WHERE x.MaKhachHang=KhachHang.MaKhachHang) AS vehicleCount";
            dieuKienTim.Add(
                $"EXISTS (SELECT 1 FROM Xe x WHERE x.MaKhachHang=KhachHang.MaKhachHang AND {bieuThucBienSo} LIKE '%' + NULLIF({bieuThucTimBienSo}, '') + '%')"
            );
        }

        string dieuKienLoc =
            dieuKienTim.Count > 0 ? " Where (" + string.Join(" Or ", dieuKienTim) + ")" : "";
        var chuoiTimKiem = (tuKhoa ?? "").Trim();
        var thamSo = new
        {
            q = "%" + chuoiTimKiem + "%",
            query = chuoiTimKiem,
            skip = (trang - 1) * kichThuocTrang,
            size = kichThuocTrang,
        };
        string sqlDem = $"Select Count(*) From [{cauHinh.Bang}]" + dieuKienLoc;
        string sqlDanhSach =
            $"Select {cacCot} From [{cauHinh.Bang}]"
            + dieuKienLoc
            + $" Order By {cotSapXep} "
            + "Offset @skip Rows Fetch Next @size Rows Only";
        int tongSoDong = await duLieu.DocGiaTriAsync<int>(sqlDem, thamSo);
        List<Dictionary<string, object?>> danhSachDong = await duLieu.DocBangAsync(
            sqlDanhSach,
            thamSo
        );
        return Ok(
            new
            {
                items = danhSachDong,
                total = tongSoDong,
                page = trang,
                totalPages = Math.Max(1, (int)Math.Ceiling(tongSoDong / (double)kichThuocTrang)),
            }
        );
    }

    [HttpGet("customers/{customerId}/vehicles")]
    public async Task<IActionResult> XeCuaKhach([FromRoute(Name = "customerId")] string maKhachHang)
    {
        CauHinh("customers");
        var cacDong = await duLieu.DocBangAsync(
            $$"""
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
            """,
            new { customerId = maKhachHang }
        );
        return Ok(cacDong);
    }

    [HttpGet("options/{kind}")]
    public async Task<IActionResult> DanhSachChon([FromRoute(Name = "kind")] string loaiDanhMuc)
    {
        bool laQuanLy = User.IsInRole("Manager");
        bool laThuNgan = User.IsInRole("Cashier");
        string cauLenhSql = loaiDanhMuc switch
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
                "Select n.MaNhanVien As id, n.HoTen As label, c.TenChucVu As role, "
                    + "Case When c.TenChucVu Like N'%chính%' Then 3 Else 2 End As capacity, "
                    + "(Select Count(Distinct a.MaXe) From ChiTietPhanCong a Join PhieuTiepNhan p On p.MaXe = a.MaXe "
                    + "Where a.MaNhanVien = n.MaNhanVien And p.TrangThai In " + TrangThaiDangXuLy + ") As busy "
                    + "From NhanVien n Join ChucVu c On c.MaChucVu = n.MaChucVu "
                    + "Where n.TrangThai = 1 And c.TenChucVu Like N'Thợ%' Order By c.TenChucVu, n.HoTen",
            "roles" when laQuanLy =>
                "Select MaChucVu As id, TenChucVu As label From ChucVu Order By MaChucVu",
            "adjustments" when laQuanLy =>
                "Select MaKhoanDieuChinh As id, TenKhoanDieuChinh As label From KhoanDieuChinh Order By MaKhoanDieuChinh",
            "shifts" =>
                "Select MaCaLam As id, MaCaLam + N' · ' + Convert(nvarchar(5), ThoiGianBatDau) + N'–' + Convert(nvarchar(5), ThoiGianKetThuc) As label "
                    + "From CaLam Order By ThoiGianBatDau",
            _ => throw new UnauthorizedAccessException(
                "Danh sách không khả dụng với tài khoản này."
            ),
        };
        return Ok(await duLieu.DocBangAsync(cauLenhSql));
    }

    [HttpPost("{kind}")]
    public Task<IActionResult> Them(
        [FromRoute(Name = "kind")] string loaiDanhMuc,
        Dictionary<string, JsonElement> yeuCau
    )
    {
        return Luu(loaiDanhMuc, null, yeuCau);
    }

    [HttpPost("{kind}/code")]
    public async Task<IActionResult> CapMa([FromRoute(Name = "kind")] string loaiDanhMuc)
    {
        CauHinhDanhMuc cauHinh = CauHinh(loaiDanhMuc);
        TruongDanhMuc truongMa =
            cauHinh.CacTruong.SingleOrDefault(truong => truong.Kieu == "code")
            ?? throw new ArgumentException("Danh mục này dùng các thông tin đã chọn làm khóa.");
        string maDaCap = await duLieu.TrongGiaoDichAsync(phien =>
            phien.TaoMaTuDongAsync(cauHinh.Bang, truongMa.Ten, TienToMa(loaiDanhMuc))
        );
        string noiDung = string.Join(
            "|",
            loaiDanhMuc,
            PhanQuyenNguoiDung.MaNhanVien(User),
            maDaCap
        );
        return Ok(new { id = maDaCap, ticket = baoVeMa.Protect(noiDung, TimeSpan.FromHours(4)) });
    }

    private static string TienToMa(string loaiDanhMuc) =>
        loaiDanhMuc switch
        {
            "customers" => "KH",
            "vehicles" => "XE",
            "services" => "DV",
            "suppliers" => "NCC",
            "parts" => "PT",
            "employees" => "NV",
            "roles" => "CV",
            "adjustments" => "DC",
            "shifts" => "CA",
            _ => throw new ArgumentException("Danh mục không có mã riêng."),
        };

    private string? DocMaDaCap(string loaiDanhMuc, Dictionary<string, JsonElement> yeuCau)
    {
        if (!yeuCau.TryGetValue("maDaCap", out JsonElement veCapMa))
            return null;
        try
        {
            string[] cacPhan = baoVeMa.Unprotect(veCapMa.GetString() ?? "").Split('|');
            if (
                cacPhan.Length != 3
                || cacPhan[0] != loaiDanhMuc
                || cacPhan[1] != PhanQuyenNguoiDung.MaNhanVien(User)
            )
                throw new CryptographicException();
            return cacPhan[2];
        }
        catch (Exception loi) when (loi is CryptographicException or InvalidOperationException)
        {
            throw new ArgumentException(
                "Mã trên biểu mẫu đã hết hạn hoặc không hợp lệ. Vui lòng mở lại biểu mẫu."
            );
        }
    }

    [HttpPut("{kind}/{key}")]
    public Task<IActionResult> Sua(
        [FromRoute(Name = "kind")] string loaiDanhMuc,
        [FromRoute(Name = "key")] string khoaBanGhi,
        Dictionary<string, JsonElement> yeuCau
    )
    {
        return Luu(loaiDanhMuc, khoaBanGhi, yeuCau);
    }

    private async Task<IActionResult> Luu(
        string loaiDanhMuc,
        string? khoaBanGhi,
        Dictionary<string, JsonElement> yeuCau
    )
    {
        CauHinhDanhMuc cauHinh = CauHinh(loaiDanhMuc);
        bool themMoi = khoaBanGhi == null;
        string? maDaCap = themMoi ? DocMaDaCap(loaiDanhMuc, yeuCau) : null;
        Dictionary<string, object?> duLieuNhap = DocDuLieuNhap(cauHinh, yeuCau, themMoi);
        await duLieu.TrongGiaoDichAsync(async phien =>
        {
            if (themMoi)
            {
                foreach (
                    TruongDanhMuc truong in cauHinh.CacTruong.Where(truong => truong.Kieu == "code")
                )
                {
                    duLieuNhap[truong.Ten] =
                        maDaCap
                        ?? await phien.TaoMaTuDongAsync(
                            cauHinh.Bang,
                            truong.Ten,
                            TienToMa(loaiDanhMuc)
                        );
                }
            }

            Dictionary<string, object?> giaTriKhoa = DocKhoa(cauHinh, khoaBanGhi, duLieuNhap);
            string dieuKienKhoa = DieuKienKhoa(cauHinh);
            Dictionary<string, object?>? banGhiCu = null;
            if (!themMoi)
            {
                string sqlBanGhiCu =
                    $"Select * From [{cauHinh.Bang}] With (UpdLock, HoldLock) Where {dieuKienKhoa}";
                banGhiCu =
                    (await phien.DocBangAsync(sqlBanGhiCu, giaTriKhoa)).SingleOrDefault()
                    ?? throw new KeyNotFoundException("Bản ghi không còn tồn tại.");
            }

            await KiemTraTheoDanhMuc(phien, loaiDanhMuc, banGhiCu, duLieuNhap);
            if (themMoi)
            {
                string cacCot = string.Join(", ", duLieuNhap.Keys.Select(cot => $"[{cot}]"));
                string cacThamSo = string.Join(", ", duLieuNhap.Keys.Select(cot => "@" + cot));
                await phien.ThucThiAsync(
                    $"Insert Into [{cauHinh.Bang}] ({cacCot}) Values ({cacThamSo})",
                    duLieuNhap
                );
            }
            else
            {
                foreach (string cotKhoa in cauHinh.Khoa)
                {
                    duLieuNhap.Remove(cotKhoa);
                }

                string cacCotCapNhat = string.Join(
                    ", ",
                    duLieuNhap.Keys.Select(cot => $"[{cot}] = @{cot}")
                );
                foreach (KeyValuePair<string, object?> cap in giaTriKhoa)
                {
                    duLieuNhap.Add(cap.Key, cap.Value);
                }

                await phien.ThucThiAsync(
                    $"Update [{cauHinh.Bang}] Set {cacCotCapNhat} Where {dieuKienKhoa}",
                    duLieuNhap
                );
            }

            if (loaiDanhMuc is "adjustments" or "roleAdjustments")
            {
                await phien.ThucThiAsync(SqlTinhLaiTienDieuChinh);
            }

            return true;
        });
        return StatusCode(
            themMoi ? 201 : 200,
            new
            {
                message = "Đã lưu dữ liệu.",
                id = khoaBanGhi
                    ?? string.Join(
                        "~",
                        cauHinh.Khoa.Select(cot =>
                            Convert.ToString(duLieuNhap[cot], CultureInfo.InvariantCulture)
                        )
                    ),
            }
        );
    }

    [HttpDelete("{kind}/{key}")]
    public async Task<IActionResult> Xoa(
        [FromRoute(Name = "kind")] string loaiDanhMuc,
        [FromRoute(Name = "key")] string khoaBanGhi
    )
    {
        CauHinhDanhMuc cauHinh = CauHinh(loaiDanhMuc);
        await duLieu.TrongGiaoDichAsync(async phien =>
        {
            Dictionary<string, object?> giaTriKhoa = DocKhoa(cauHinh, khoaBanGhi, []);
            if (loaiDanhMuc == "employees" && khoaBanGhi == PhanQuyenNguoiDung.MaNhanVien(User))
            {
                throw new InvalidOperationException("Không thể xóa tài khoản đang đăng nhập.");
            }

            if (loaiDanhMuc == "serviceParts")
            {
                string maDichVu = khoaBanGhi.Split('~')[0];
                if (await DemPhieuDangXuLyCuaDichVu(phien, maDichVu) > 0)
                {
                    throw new InvalidOperationException(
                        "Không xóa định mức đang được dùng cho xe chưa hoàn thành."
                    );
                }
            }

            if (loaiDanhMuc == "services")
            {
                await phien.ThucThiAsync("Delete WebDichVu Where MaDichVu = @k0", giaTriKhoa);
            }

            int soDongXoa = await phien.ThucThiAsync(
                $"Delete [{cauHinh.Bang}] Where {DieuKienKhoa(cauHinh)}",
                giaTriKhoa
            );
            if (soDongXoa == 0)
            {
                throw new KeyNotFoundException("Không tìm thấy bản ghi.");
            }

            if (loaiDanhMuc is "adjustments" or "roleAdjustments")
            {
                await phien.ThucThiAsync(SqlTinhLaiTienDieuChinh);
            }

            return true;
        });
        return Ok(new { message = "Đã xóa bản ghi." });
    }

    private bool DuocDung(CauHinhDanhMuc cauHinh)
    {
        return cauHinh.VaiTro.Split(',').Any(User.IsInRole);
    }

    private CauHinhDanhMuc CauHinh(string loaiDanhMuc)
    {
        if (!DinhNghiaDanhMuc.TatCa.TryGetValue(loaiDanhMuc, out CauHinhDanhMuc? cauHinh))
        {
            throw new KeyNotFoundException("Không có danh mục này.");
        }

        if (!DuocDung(cauHinh))
        {
            throw new UnauthorizedAccessException("Bạn không có quyền quản lý danh mục này.");
        }

        return cauHinh;
    }

    private static string DieuKienKhoa(CauHinhDanhMuc cauHinh)
    {
        return string.Join(" And ", cauHinh.Khoa.Select((cot, chiSo) => $"[{cot}] = @k{chiSo}"));
    }

    private static Dictionary<string, object?> DocKhoa(
        CauHinhDanhMuc cauHinh,
        string? khoaBanGhi,
        Dictionary<string, object?> duLieuNhap
    )
    {
        string[]? cacPhanKhoa = khoaBanGhi?.Split('~');
        if (cacPhanKhoa != null && cacPhanKhoa.Length != cauHinh.Khoa.Length)
        {
            throw new ArgumentException("Mã bản ghi không hợp lệ.");
        }

        Dictionary<string, object?> giaTriKhoa = new Dictionary<string, object?>();
        for (int chiSo = 0; chiSo < cauHinh.Khoa.Length; chiSo++)
        {
            giaTriKhoa["k" + chiSo] =
                cacPhanKhoa == null ? duLieuNhap[cauHinh.Khoa[chiSo]] : cacPhanKhoa[chiSo];
        }

        return giaTriKhoa;
    }

    private static Dictionary<string, object?> DocDuLieuNhap(
        CauHinhDanhMuc cauHinh,
        Dictionary<string, JsonElement> yeuCau,
        bool themMoi
    )
    {
        Dictionary<string, object?> ketQua = new Dictionary<string, object?>();
        IEnumerable<TruongDanhMuc> truongCanDoc = cauHinh.CacTruong.Where(truong =>
            !truong.ChiDoc
            && truong.Kieu != "code"
            && (themMoi || !cauHinh.Khoa.Contains(truong.Ten))
        );
        foreach (TruongDanhMuc truong in truongCanDoc)
        {
            JsonElement giaTriGoc = yeuCau.GetValueOrDefault(truong.Ten);
            string chuoi =
                giaTriGoc.ValueKind == JsonValueKind.Undefined ? "" : giaTriGoc.ToString().Trim();
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
                "money" or "factor" or "signedMoney" or "positiveInt" or "int" or "hours" => DocSo(
                    truong,
                    chuoi
                ),
                "bool" => chuoi.Equals("true", StringComparison.OrdinalIgnoreCase) || chuoi == "1",
                "date" => DocNgay(chuoi),
                "time" => DocGio(chuoi),
                _ => DocChuoi(truong, chuoi),
            };
        }

        return ketQua;
    }

    private static decimal DocSo(TruongDanhMuc truong, string chuoi)
    {
        if (
            !decimal.TryParse(
                chuoi,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal so
            )
            || Math.Abs(so) > 100000000000m
        )
        {
            throw new ArgumentException($"{truong.Nhan} không hợp lệ.");
        }

        bool ngoaiKhoang = truong.Kieu switch
        {
            "money" or "positiveInt" => so <= 0,
            "factor" => so <= 0 || so >= 100,
            "hours" => so < 0 || so > 24,
            "signedMoney" => so == 0,
            _ => false,
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
        if (
            !DateTime.TryParseExact(
                chuoi,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime ngay
            )
        )
        {
            throw new ArgumentException("Ngày không hợp lệ.");
        }

        return ngay;
    }

    private static TimeSpan DocGio(string chuoi)
    {
        if (
            !TimeSpan.TryParse(chuoi, CultureInfo.InvariantCulture, out TimeSpan gio)
            || gio < TimeSpan.Zero
            || gio >= TimeSpan.FromDays(1)
        )
        {
            throw new ArgumentException("Giờ không hợp lệ.");
        }

        return gio;
    }

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

    private static async Task<int> DemPhieuDangXuLyCuaDichVu(PhienGiaoDich phien, string maDichVu)
    {
        string sqlDem =
            "Select Count(*) "
            + "From ChiTietPhieuTiepNhan d Join PhieuTiepNhan p On p.MaPhieuTiepNhan = d.MaPhieuTiepNhan "
            + "Where d.MaDichVu = @id And p.TrangThai In "
            + TrangThaiDangXuLy;
        return await phien.DocGiaTriAsync<int>(sqlDem, new { id = maDichVu });
    }

    private async Task KiemTraTheoDanhMuc(
        PhienGiaoDich phien,
        string loaiDanhMuc,
        Dictionary<string, object?>? banGhiCu,
        Dictionary<string, object?> duLieuNhap
    )
    {
        switch (loaiDanhMuc)
        {
            case "parts":
                await KiemTraPhuTung(phien, banGhiCu, duLieuNhap);
                break;
            case "vehicles":
                await KiemTraXe(phien, banGhiCu);
                break;
            case "serviceParts":
                await KiemTraDinhMuc(phien, banGhiCu, duLieuNhap);
                break;
            case "employees":
                await KiemTraNhanVien(phien, banGhiCu, duLieuNhap);
                break;
            case "roles":
                await KiemTraChucVu(phien, banGhiCu, duLieuNhap);
                break;
            case "shifts":
                await KiemTraCaLam(phien, banGhiCu, duLieuNhap);
                break;
            case "attendance":
                await KiemTraChamCong(phien, banGhiCu, duLieuNhap);
                break;
        }
    }

    private static async Task KiemTraXe(PhienGiaoDich phien, Dictionary<string, object?>? banGhiCu)
    {
        if (banGhiCu == null)
        {
            return;
        }

        int soPhieu = await phien.DocGiaTriAsync<int>(
            "Select Count(*) From PhieuTiepNhan Where MaXe = @id",
            new { id = banGhiCu.Chuoi("MaXe") }
        );
        if (soPhieu > 0)
        {
            throw new InvalidOperationException(
                "Xe đã có phiếu tiếp nhận. Không sửa thông tin lịch sử; tạo lượt xe mới khi tiếp nhận lần tiếp theo."
            );
        }
    }

    private async Task KiemTraNhanVien(
        PhienGiaoDich phien,
        Dictionary<string, object?>? banGhiCu,
        Dictionary<string, object?> duLieuNhap
    )
    {
        if (banGhiCu == null && !duLieuNhap.ContainsKey("MatKhau"))
        {
            throw new ArgumentException("Nhập mật khẩu từ 6 ký tự cho nhân viên mới.");
        }

        string sqlTenChucVu = "Select TenChucVu From ChucVu Where MaChucVu = @id";
        string? chucVuMoi = await phien.DocGiaTriAsync<string>(
            sqlTenChucVu,
            new { id = duLieuNhap["MaChucVu"] }
        );
        if (chucVuMoi == null)
        {
            throw new ArgumentException("Chọn chức vụ hợp lệ.");
        }

        if (banGhiCu == null)
        {
            return;
        }

        string? chucVuCu = await phien.DocGiaTriAsync<string>(
            sqlTenChucVu,
            new { id = banGhiCu.Chuoi("MaChucVu") }
        );
        bool khoaTaiKhoan = !(bool)duLieuNhap["TrangThai"]!;
        bool doiNhomQuyen =
            PhanQuyenNguoiDung.XacDinhVaiTro(chucVuMoi)
            != PhanQuyenNguoiDung.XacDinhVaiTro(chucVuCu ?? "");
        if (!khoaTaiKhoan && !doiNhomQuyen)
        {
            return;
        }

        string maNhanVien = banGhiCu.Chuoi("MaNhanVien");
        if (maNhanVien == PhanQuyenNguoiDung.MaNhanVien(User))
        {
            throw new InvalidOperationException(
                "Không thể tự khóa hoặc đổi nhóm quyền tài khoản đang đăng nhập."
            );
        }

        string sqlXeDangSua =
            "Select Count(*) "
            + "From ChiTietPhanCong c Join PhieuTiepNhan p On p.MaXe = c.MaXe "
            + "Where c.MaNhanVien = @id And p.TrangThai In "
            + TrangThaiDangXuLy;
        if (await phien.DocGiaTriAsync<int>(sqlXeDangSua, new { id = maNhanVien }) > 0)
        {
            throw new InvalidOperationException(
                "Nhân viên còn xe đang xử lý. Hoàn tất hoặc chuyển phân công trước."
            );
        }
    }

    private async Task KiemTraPhuTung(
        PhienGiaoDich phien,
        Dictionary<string, object?>? banGhiCu,
        Dictionary<string, object?> duLieuNhap
    )
    {
        DateTime hanSuDungMoi = (DateTime)duLieuNhap["HanSuDung"]!;
        if (banGhiCu == null)
        {
            duLieuNhap["SoLuongTon"] = 0;
            if (hanSuDungMoi < XuLyDuLieu.HomNay)
            {
                throw new ArgumentException("Không tạo lô phụ tùng đã hết hạn sử dụng.");
            }

            return;
        }

        string maPhuTung = banGhiCu.Chuoi("MaPhuTung");
        bool doiNhaCungCap = !string.Equals(
            Convert.ToString(duLieuNhap["MaNhaCungCap"]),
            banGhiCu.Chuoi("MaNhaCungCap"),
            StringComparison.OrdinalIgnoreCase
        );
        bool doiHanSuDung = hanSuDungMoi != Convert.ToDateTime(banGhiCu["HanSuDung"]);
        bool doiGiaNhap = (decimal)duLieuNhap["DonGiaNhap"]! != banGhiCu.SoTien("DonGiaNhap");
        if (doiNhaCungCap || doiHanSuDung || doiGiaNhap)
        {
            if (await LoPhuTungDaSuDung(phien, maPhuTung, banGhiCu.SoNguyen("SoLuongTon")))
            {
                throw new InvalidOperationException(
                    "Lô phụ tùng đã sử dụng (có tồn, nhập kho, kiểm kê hoặc xuất sửa): không đổi nhà cung cấp, hạn dùng hay giá nhập; hãy tạo mã lô mới."
                );
            }

            if (doiHanSuDung && hanSuDungMoi < XuLyDuLieu.HomNay)
            {
                throw new ArgumentException("Hạn sử dụng mới không được trước hôm nay.");
            }
        }

        decimal giaBanCu = banGhiCu.SoTien("DonGiaBan");
        decimal giaBanMoi = (decimal)duLieuNhap["DonGiaBan"]!;
        if (giaBanMoi != giaBanCu)
        {
            string sqlLuuGiaBan =
                "Insert Into DieuChinhGiaBan (MaDieuChinhGia, MaPhuTung, MaNhanVien, NgayDieuChinh, GiaBanCu, GiaBanMoi, LyDo) "
                + "Values (@MaDieuChinhGia, @MaPhuTung, @MaNhanVien, @NgayDieuChinh, @GiaBanCu, @GiaBanMoi, N'Cập nhật từ website')";
            await phien.ThucThiAsync(
                sqlLuuGiaBan,
                new
                {
                    MaDieuChinhGia = XuLyDuLieu.TaoMaMoi("DG"),
                    MaPhuTung = maPhuTung,
                    MaNhanVien = PhanQuyenNguoiDung.MaNhanVien(User),
                    NgayDieuChinh = XuLyDuLieu.HomNay,
                    GiaBanCu = giaBanCu,
                    GiaBanMoi = giaBanMoi,
                }
            );
        }
    }

    private static async Task<bool> LoPhuTungDaSuDung(
        PhienGiaoDich phien,
        string maPhuTung,
        int soLuongTon
    )
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
        int daSuDung = await phien.DocGiaTriAsync<int>(sqlKiemTra, new { MaPhuTung = maPhuTung });
        return daSuDung == 1;
    }

    private static async Task KiemTraDinhMuc(
        PhienGiaoDich phien,
        Dictionary<string, object?>? banGhiCu,
        Dictionary<string, object?> duLieuNhap
    )
    {
        string maDichVu =
            banGhiCu?.Chuoi("MaDichVu") ?? Convert.ToString(duLieuNhap["MaDichVu"]) ?? "";
        if (await DemPhieuDangXuLyCuaDichVu(phien, maDichVu) > 0)
        {
            throw new InvalidOperationException(
                "Dịch vụ có xe đang sửa/chờ sửa. Chỉ thay đổi định mức khi các phiếu này kết thúc."
            );
        }
    }

    private static async Task KiemTraChucVu(
        PhienGiaoDich phien,
        Dictionary<string, object?>? banGhiCu,
        Dictionary<string, object?> duLieuNhap
    )
    {
        if (banGhiCu == null)
        {
            duLieuNhap["TienDieuChinh"] = 0;
            return;
        }

        string vaiTroCu = PhanQuyenNguoiDung.XacDinhVaiTro(banGhiCu.Chuoi("TenChucVu"));
        string vaiTroMoi = PhanQuyenNguoiDung.XacDinhVaiTro((string)duLieuNhap["TenChucVu"]!);
        if (vaiTroCu == vaiTroMoi)
        {
            return;
        }

        int soNhanVien = await phien.DocGiaTriAsync<int>(
            "Select Count(*) From NhanVien Where MaChucVu = @id",
            new { id = banGhiCu.Chuoi("MaChucVu") }
        );
        if (soNhanVien > 0)
        {
            throw new InvalidOperationException(
                "Chức vụ đang được nhân viên sử dụng; không đổi nhóm quyền bằng cách đổi tên chức vụ."
            );
        }
    }

    private static async Task KiemTraCaLam(
        PhienGiaoDich phien,
        Dictionary<string, object?>? banGhiCu,
        Dictionary<string, object?> duLieuNhap
    )
    {
        TimeSpan gioBatDau = (TimeSpan)duLieuNhap["ThoiGianBatDau"]!;
        TimeSpan gioKetThuc = (TimeSpan)duLieuNhap["ThoiGianKetThuc"]!;
        if (gioKetThuc <= gioBatDau)
        {
            throw new ArgumentException("Giờ kết thúc phải sau giờ bắt đầu.");
        }

        duLieuNhap["ThoiGianCaLam"] = (decimal)(gioKetThuc - gioBatDau).TotalHours;
        if (banGhiCu == null)
        {
            return;
        }

        int soLanChamCong = await phien.DocGiaTriAsync<int>(
            "Select Count(*) From ChiTietCaLam Where MaCaLam = @id",
            new { id = banGhiCu.Chuoi("MaCaLam") }
        );
        if (soLanChamCong > 0)
        {
            throw new InvalidOperationException(
                "Ca đã được chấm công. Hãy tạo ca mới để giữ đúng lịch sử."
            );
        }
    }

    private static async Task KiemTraChamCong(
        PhienGiaoDich phien,
        Dictionary<string, object?>? banGhiCu,
        Dictionary<string, object?> duLieuNhap
    )
    {
        Dictionary<string, object?> banGhi = new Dictionary<string, object?>(
            banGhiCu ?? new Dictionary<string, object?>()
        );
        foreach (KeyValuePair<string, object?> cap in duLieuNhap)
        {
            banGhi[cap.Key] = cap.Value;
        }

        if ((DateTime)banGhi["NgayLam"]! > XuLyDuLieu.HomNay)
        {
            throw new ArgumentException("Không chấm công cho ngày tương lai.");
        }

        decimal soGioCa = await phien.DocGiaTriAsync<decimal>(
            "Select ThoiGianCaLam From CaLam Where MaCaLam = @id",
            new { id = banGhi["MaCaLam"] }
        );
        if ((decimal)duLieuNhap["TongThoiGianLamThucTe"]! > soGioCa)
        {
            throw new ArgumentException("Số giờ thực tế không được vượt thời lượng ca.");
        }

        string sqlCaTrungGio =
            "Select Count(*) "
            + "From ChiTietCaLam a "
            + "Join CaLam c On c.MaCaLam = a.MaCaLam "
            + "Join CaLam n On n.MaCaLam = @shift "
            + "Where a.MaNhanVien = @id And a.NgayLam = @date And a.MaCaLam <> @shift "
            + "And c.ThoiGianBatDau < n.ThoiGianKetThuc And n.ThoiGianBatDau < c.ThoiGianKetThuc";
        int soCaTrung = await phien.DocGiaTriAsync<int>(
            sqlCaTrungGio,
            new
            {
                id = banGhi["MaNhanVien"],
                date = banGhi["NgayLam"],
                shift = banGhi["MaCaLam"],
            }
        );
        if (soCaTrung > 0)
        {
            throw new InvalidOperationException("Nhân viên đã chấm công ca trùng giờ.");
        }
    }
}
