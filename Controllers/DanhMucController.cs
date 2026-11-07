using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;
[ApiController, Authorize, Route("api/catalog")]
public sealed class DanhMucController(XuLyDuLieu db) : ControllerBase
{
    private CauHinhDanhMuc CauHinh(string kind)
    {
        if (!DinhNghiaDanhMuc.TatCa.TryGetValue(kind, out var spec))
            throw new KeyNotFoundException("Không có danh mục này.");
        if (!spec.VaiTro.Split(',').Any(User.IsInRole))
            throw new UnauthorizedAccessException("Bạn không có quyền quản lý danh mục này.");
        return spec;
    }

    [HttpGet("schema")]
    public IActionResult DinhNghia() => Ok(DinhNghiaDanhMuc.TatCa.Where(x => x.Value.VaiTro.Split(',').Any(User.IsInRole)).Select(x => new { id = x.Key, title = x.Value.TieuDe, keys = x.Value.Khoa, fields = x.Value.CacTruong }));
    [HttpGet("{kind}")]
    public async Task<IActionResult> DanhSach(string kind, string? q = null, int page = 1, int pageSize = 12)
    {
        var spec = CauHinh(kind);
        PhanTrang.KiemTra(page, pageSize, q);
        var fields = spec.CacTruong.Where(f => f.Kieu != "password").ToList();
        var columns = string.Join(',', fields.Select(f => $"[{f.Ten}]"));
        var terms = fields.Where(f => f.Kieu is "text" or "code" or "phone").Select(f => $"[{f.Ten}] LIKE @q");
        if (kind == "customers")
        {
            var plate = BienSoXe.KhoaSql("x.BienXe");
            var plateQuery = BienSoXe.KhoaSql("@query");
            columns += $",(SELECT COUNT(DISTINCT {plate}) FROM Xe x WHERE x.MaKhachHang=KhachHang.MaKhachHang) AS vehicleCount";
            terms = terms.Append($"EXISTS (SELECT 1 FROM Xe x WHERE x.MaKhachHang=KhachHang.MaKhachHang AND {plate} LIKE '%' + NULLIF({plateQuery}, '') + '%')");
        }
        var where = terms.Any() ? " WHERE (" + string.Join(" OR ", terms) + ")" : "";
        var query = (q ?? "").Trim();
        var args = new
        {
            q = "%" + query + "%",
            query,
            skip = (page - 1) * pageSize,
            size = pageSize
        };
        var total = await db.DocGiaTriAsync<int>($"SELECT COUNT(*) FROM [{spec.Bang}]" + where, args);
        var items = await db.DocBangAsync($"SELECT {columns} FROM [{spec.Bang}] {where} ORDER BY {string.Join(',', spec.Khoa.Select(k => $"[{k}]"))} OFFSET @skip ROWS FETCH NEXT @size ROWS ONLY", args);
        return Ok(new { items, total, page, totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)) });
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

    [HttpGet("options/{kind}")]
    public async Task<IActionResult> DanhSachChon(string kind)
    {
        // Danh sách chọn chỉ trả mã và nhãn, không trả thông tin nhân sự hoặc lương.
        var sql = kind switch
        {
            "customers" when User.IsInRole("Manager") || User.IsInRole("Cashier") => "SELECT MaKhachHang id,TenKhachHang+N' · '+SoDienThoai label FROM KhachHang ORDER BY TenKhachHang",
            "services" => "SELECT MaDichVu id,TenDichVu label FROM DichVu WHERE TrangThai=1 ORDER BY TenDichVu",
            "parts" => "SELECT MaPhuTung id,TenPhuTung+N' · tồn '+CAST(SoLuongTon AS nvarchar(15)) label FROM PhuTung WHERE TrangThai=1 ORDER BY TenPhuTung",
            "suppliers" when User.IsInRole("Manager") => "SELECT MaNhaCungCap id,TenNhaCungCap label FROM NhaCungCap ORDER BY TenNhaCungCap",
            "employees" when User.IsInRole("Manager") => "SELECT MaNhanVien id,HoTen label FROM NhanVien WHERE TrangThai=1 ORDER BY HoTen",
            "mechanics" => "SELECT n.MaNhanVien id,n.HoTen label FROM NhanVien n JOIN ChucVu c ON c.MaChucVu=n.MaChucVu WHERE n.TrangThai=1 AND c.TenChucVu LIKE N'Thợ%' ORDER BY n.HoTen",
            "roles" when User.IsInRole("Manager") => "SELECT MaChucVu id,TenChucVu label FROM ChucVu ORDER BY MaChucVu",
            "adjustments" when User.IsInRole("Manager") => "SELECT MaKhoanDieuChinh id,TenKhoanDieuChinh label FROM KhoanDieuChinh ORDER BY MaKhoanDieuChinh",
            "shifts" => "SELECT MaCaLam id,MaCaLam+N' · '+CONVERT(nvarchar(5),ThoiGianBatDau)+N'–'+CONVERT(nvarchar(5),ThoiGianKetThuc) label FROM CaLam ORDER BY ThoiGianBatDau",
            _ => throw new UnauthorizedAccessException("Danh sách không khả dụng với tài khoản này.")};
        return Ok(await db.DocBangAsync(sql));
    }

    [HttpPost("{kind}")]
    public Task<IActionResult> Them(string kind, Dictionary<string, JsonElement> input) => Luu(kind, null, input);
    [HttpPut("{kind}/{key}")]
    public Task<IActionResult> Sua(string kind, string key, Dictionary<string, JsonElement> input) => Luu(kind, key, input);
    private async Task<IActionResult> Luu(string kind, string? key, Dictionary<string, JsonElement> input)
    {
        var spec = CauHinh(kind);
        var data = DocDuLieuNhap(spec, input, key == null);
        await db.TrongGiaoDichAsync(async s =>
        {
            var keys = DocKhoa(spec, key, data);
            var where = DieuKienKhoa(spec);
            var old = key == null ? null : (await s.DocBangAsync($"SELECT * FROM [{spec.Bang}] WITH(UPDLOCK,HOLDLOCK) WHERE {where}", keys)).SingleOrDefault();
            if (key != null && old == null)
                throw new KeyNotFoundException("Bản ghi không còn tồn tại.");
            if (kind == "parts")
                await KiemTraPhuTung(s, old, data);
            if (kind == "vehicles" && old != null && await s.DocGiaTriAsync<int>("SELECT COUNT(*) FROM PhieuTiepNhan WHERE MaXe=@id", new { id = old.Chuoi("MaXe") }) > 0)
                throw new InvalidOperationException("Xe đã có phiếu tiếp nhận. Không sửa thông tin lịch sử; tạo lượt xe mới khi tiếp nhận lần tiếp theo.");
            if (kind == "serviceParts")
                await KiemTraDinhMuc(s, old, data);
            if (kind == "employees")
                await KiemTraNhanVien(s, old, data);
            if (kind == "roles")
                await KiemTraChucVu(s, old, data);
            if (kind == "shifts")
                await KiemTraCaLam(s, old, data);
            if (kind == "attendance")
                await KiemTraChamCong(s, old, data);
            if (key == null)
                await s.ThucThiAsync($"INSERT [{spec.Bang}] ({string.Join(',', data.Keys.Select(k => $"[{k}]"))}) VALUES ({string.Join(',', data.Keys.Select(k => "@" + k))})", data);
            else
            {
                foreach (var k in spec.Khoa)
                    data.Remove(k);
                var set = string.Join(',', data.Keys.Select(k => $"[{k}]=@{k}"));
                foreach (var pair in keys)
                    data.Add(pair.Key, pair.Value);
                await s.ThucThiAsync($"UPDATE [{spec.Bang}] SET {set} WHERE {where}", data);
            }

            if (kind is "adjustments" or "roleAdjustments")
                await s.ThucThiAsync("UPDATE c SET TienDieuChinh=COALESCE((SELECT SUM(k.SoTien) FROM ChucVuDieuChinh d JOIN KhoanDieuChinh k ON d.MaKhoanDieuChinh=k.MaKhoanDieuChinh WHERE d.MaChucVu=c.MaChucVu),0) FROM ChucVu c");
            return true;
        });
        return StatusCode(key == null ? 201 : 200, new { message = "Đã lưu dữ liệu." });
    }

    private async Task KiemTraNhanVien(PhienGiaoDich s, Dictionary<string, object?>? old, Dictionary<string, object?> data)
    {
        if (old == null && !data.ContainsKey("MatKhau"))
            throw new ArgumentException("Nhập mật khẩu từ 6 ký tự cho nhân viên mới.");
        var newRole = await s.DocGiaTriAsync<string>("SELECT TenChucVu FROM ChucVu WHERE MaChucVu=@id", new { id = data["MaChucVu"] });
        if (newRole == null)
            throw new ArgumentException("Chọn chức vụ hợp lệ.");
        if (old == null)
            return;
        var oldRole = await s.DocGiaTriAsync<string>("SELECT TenChucVu FROM ChucVu WHERE MaChucVu=@id", new { id = old.Chuoi("MaChucVu") });
        if (!(bool)data["TrangThai"]! || PhanQuyenNguoiDung.XacDinhVaiTro(newRole) != PhanQuyenNguoiDung.XacDinhVaiTro(oldRole ?? ""))
        {
            if (old.Chuoi("MaNhanVien") == PhanQuyenNguoiDung.MaNhanVien(User))
                throw new InvalidOperationException("Không thể tự khóa hoặc đổi nhóm quyền tài khoản đang đăng nhập.");
            if (await s.DocGiaTriAsync<int>("SELECT COUNT(*) FROM ChiTietPhanCong c JOIN PhieuTiepNhan p ON p.MaXe=c.MaXe WHERE c.MaNhanVien=@id AND p.TrangThai IN(N'Chờ sửa',N'Đang sửa')", new { id = old.Chuoi("MaNhanVien") }) > 0)
                throw new InvalidOperationException("Nhân viên còn xe đang xử lý. Hoàn tất hoặc chuyển phân công trước.");
        }
    }

    [HttpDelete("{kind}/{key}")]
    public async Task<IActionResult> Xoa(string kind, string key)
    {
        var spec = CauHinh(kind);
        await db.TrongGiaoDichAsync(async s =>
        {
            var keys = DocKhoa(spec, key, []);
            var where = DieuKienKhoa(spec);
            if (kind == "employees" && key == PhanQuyenNguoiDung.MaNhanVien(User))
                throw new InvalidOperationException("Không thể xóa tài khoản đang đăng nhập.");
            if (kind == "serviceParts")
            {
                if (await s.DocGiaTriAsync<int>("SELECT COUNT(*) FROM ChiTietPhieuTiepNhan d JOIN PhieuTiepNhan p ON p.MaPhieuTiepNhan=d.MaPhieuTiepNhan WHERE d.MaDichVu=@id AND p.TrangThai IN(N'Chờ sửa',N'Đang sửa')", new { id = key.Split('~')[0] }) > 0)
                    throw new InvalidOperationException("Không xóa định mức đang được dùng cho xe chưa hoàn thành.");
            }

            if (kind == "services")
                await s.ThucThiAsync("DELETE WebDichVu WHERE MaDichVu=@k0", keys);
            if (await s.ThucThiAsync($"DELETE [{spec.Bang}] WHERE {where}", keys) == 0)
                throw new KeyNotFoundException("Không tìm thấy bản ghi.");
            if (kind is "adjustments" or "roleAdjustments")
                await s.ThucThiAsync("UPDATE c SET TienDieuChinh=COALESCE((SELECT SUM(k.SoTien) FROM ChucVuDieuChinh d JOIN KhoanDieuChinh k ON k.MaKhoanDieuChinh=d.MaKhoanDieuChinh WHERE d.MaChucVu=c.MaChucVu),0) FROM ChucVu c");
            return true;
        });
        return Ok(new { message = "Đã xóa bản ghi." });
    }

    private static Dictionary<string, object?> DocDuLieuNhap(CauHinhDanhMuc spec, Dictionary<string, JsonElement> input, bool create)
    {
        var result = new Dictionary<string, object?>();
        foreach (var f in spec.CacTruong.Where(f => !f.ChiDoc && (create || !spec.Khoa.Contains(f.Ten))))
        {
            var raw = input.GetValueOrDefault(f.Ten);
            var text = raw.ValueKind == JsonValueKind.Undefined ? "" : raw.ToString().Trim();
            if (f.KhongBatBuoc && text.Length == 0)
                continue;
            if (text.Length == 0)
                throw new ArgumentException($"Vui lòng nhập {f.Nhan.ToLowerInvariant()}.");
            if (f.Kieu is "money" or "factor" or "signedMoney" or "positiveInt" or "int" or "hours")
            {
                if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) || Math.Abs(number) > 100000000000m)
                    throw new ArgumentException($"{f.Nhan} không hợp lệ.");
                if (f.Kieu is "money" or "factor" or "positiveInt" && number <= 0 || f.Kieu == "hours" && (number < 0 || number > 24) || f.Kieu == "factor" && number >= 100 || f.Kieu == "signedMoney" && number == 0)
                    throw new ArgumentException($"{f.Nhan} ngoài khoảng cho phép.");
                if (f.Kieu is "money" or "signedMoney" or "positiveInt" or "int" && number != decimal.Truncate(number))
                    throw new ArgumentException($"{f.Nhan} phải là số nguyên.");
                if (f.Kieu == "positiveInt" && number > 10000)
                    throw new ArgumentException("Số lượng tối đa 10.000.");
                result[f.Ten] = number;
            }
            else if (f.Kieu == "bool")
                result[f.Ten] = text.Equals("true", StringComparison.OrdinalIgnoreCase) || text == "1";
            else if (f.Kieu == "date")
            {
                if (!DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    throw new ArgumentException("Ngày không hợp lệ.");
                result[f.Ten] = date;
            }
            else if (f.Kieu == "time")
            {
                if (!TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var time) || time < TimeSpan.Zero || time >= TimeSpan.FromDays(1))
                    throw new ArgumentException("Giờ không hợp lệ.");
                result[f.Ten] = time;
            }
            else
            {
                if (text.Length > f.DoDaiToiDa)
                    throw new ArgumentException($"{f.Nhan} tối đa {f.DoDaiToiDa} ký tự.");
                if (f.Kieu == "code" && !Regex.IsMatch(text, @"^[A-Za-z0-9_-]{1,10}$"))
                    throw new ArgumentException("Mã chỉ gồm chữ không dấu, số, gạch dưới hoặc gạch ngang.");
                if (f.Kieu == "phone" && !Regex.IsMatch(text, @"^0[0-9]{8,10}$"))
                    throw new ArgumentException("Số điện thoại gồm 9–11 chữ số, bắt đầu bằng 0.");
                if (f.Kieu == "password")
                {
                    if (text.Length < 6)
                        throw new ArgumentException("Mật khẩu tối thiểu 6 ký tự.");
                    text = XuLyDuLieu.BamMatKhau(text);
                }

                result[f.Ten] = text;
            }
        }

        return result;
    }

    private static string DieuKienKhoa(CauHinhDanhMuc spec) => string.Join(" AND ", spec.Khoa.Select((k, i) => $"[{k}]=@k{i}"));
    private static Dictionary<string, object?> DocKhoa(CauHinhDanhMuc spec, string? key, Dictionary<string, object?> data)
    {
        var parts = key?.Split('~');
        if (parts != null && parts.Length != spec.Khoa.Length)
            throw new ArgumentException("Mã bản ghi không hợp lệ.");
        return spec.Khoa.Select((k, i) => new KeyValuePair<string, object?>("k" + i, parts == null ? data[k] : parts[i])).ToDictionary();
    }

    private async Task KiemTraPhuTung(PhienGiaoDich s, Dictionary<string, object?>? old, Dictionary<string, object?> data)
    {
        var hanDung = (DateTime)data["HanSuDung"]!;
        if (old == null)
        {
            // Lô mới luôn bắt đầu với tồn 0; hàng vào kho chỉ qua phiếu nhập.
            data["SoLuongTon"] = 0;
            if (hanDung < XuLyDuLieu.HomNay)
                throw new ArgumentException("Không tạo lô phụ tùng đã hết hạn sử dụng.");
        }
        else
        {
            var doiNhaCungCap = !string.Equals(Convert.ToString(data["MaNhaCungCap"]), old.Chuoi("MaNhaCungCap"), StringComparison.OrdinalIgnoreCase);
            var doiHanDung = hanDung != Convert.ToDateTime(old["HanSuDung"]);
            var doiGiaNhap = (decimal)data["DonGiaNhap"]! != old.SoTien("DonGiaNhap");
            if (doiNhaCungCap || doiHanDung || doiGiaNhap)
            {
                // Lô "đã sử dụng": còn tồn, đã có phiếu nhập, đã kiểm kê hoặc đã xuất cho phiếu sửa.
                var daSuDung = old.SoNguyen("SoLuongTon") > 0 || await s.DocGiaTriAsync<int>("""
                    SELECT CASE WHEN EXISTS(SELECT 1 FROM ChiTietPhieuNhap WHERE MaPhuTung=@id)
                                  OR EXISTS(SELECT 1 FROM DieuChinhTonKho WHERE MaPhuTung=@id)
                                  OR EXISTS(SELECT 1 FROM WebPhieuPhuTung WHERE MaPhuTung=@id)
                                THEN 1 ELSE 0 END
                    """, new { id = old.Chuoi("MaPhuTung") }) == 1;
                if (daSuDung)
                    throw new InvalidOperationException("Lô phụ tùng đã sử dụng (có tồn, nhập kho, kiểm kê hoặc xuất sửa): không đổi nhà cung cấp, hạn dùng hay giá nhập; hãy tạo mã lô mới.");
                if (doiHanDung && hanDung < XuLyDuLieu.HomNay)
                    throw new ArgumentException("Hạn sử dụng mới không được trước hôm nay.");
            }

            if ((decimal)data["DonGiaBan"]! != old.SoTien("DonGiaBan"))
                await s.ThucThiAsync("INSERT DieuChinhGiaBan(MaDieuChinhGia,MaPhuTung,MaNhanVien,NgayDieuChinh,GiaBanCu,GiaBanMoi,LyDo) VALUES(@id,@part,@employee,@date,@old,@next,N'Cập nhật từ website')", new { id = XuLyDuLieu.TaoMaMoi("DG"), part = old.Chuoi("MaPhuTung"), employee = PhanQuyenNguoiDung.MaNhanVien(User), date = XuLyDuLieu.HomNay, old = old.SoTien("DonGiaBan"), next = data["DonGiaBan"] });
        }
    }

    private async Task KiemTraDinhMuc(PhienGiaoDich s, Dictionary<string, object?>? old, Dictionary<string, object?> data)
    {
        var service = old?.Chuoi("MaDichVu") ?? Convert.ToString(data["MaDichVu"]);
        if (await s.DocGiaTriAsync<int>("SELECT COUNT(*) FROM ChiTietPhieuTiepNhan d JOIN PhieuTiepNhan p ON p.MaPhieuTiepNhan=d.MaPhieuTiepNhan WHERE d.MaDichVu=@id AND p.TrangThai IN(N'Chờ sửa',N'Đang sửa')", new { id = service }) > 0)
            throw new InvalidOperationException("Dịch vụ có xe đang sửa/chờ sửa. Chỉ thay đổi định mức khi các phiếu này kết thúc.");
    }

    private async Task KiemTraChucVu(PhienGiaoDich s, Dictionary<string, object?>? old, Dictionary<string, object?> data)
    {
        if (old == null)
            data["TienDieuChinh"] = 0;
        if (old != null && PhanQuyenNguoiDung.XacDinhVaiTro(old.Chuoi("TenChucVu")) != PhanQuyenNguoiDung.XacDinhVaiTro((string)data["TenChucVu"]!) && await s.DocGiaTriAsync<int>("SELECT COUNT(*) FROM NhanVien WHERE MaChucVu=@id", new { id = old.Chuoi("MaChucVu") }) > 0)
            throw new InvalidOperationException("Chức vụ đang được nhân viên sử dụng; không đổi nhóm quyền bằng cách đổi tên chức vụ.");
    }

    private async Task KiemTraCaLam(PhienGiaoDich s, Dictionary<string, object?>? old, Dictionary<string, object?> data)
    {
        var start = (TimeSpan)data["ThoiGianBatDau"]!;
        var end = (TimeSpan)data["ThoiGianKetThuc"]!;
        if (end <= start)
            throw new ArgumentException("Giờ kết thúc phải sau giờ bắt đầu.");
        data["ThoiGianCaLam"] = (decimal)(end - start).TotalHours;
        if (old != null && await s.DocGiaTriAsync<int>("SELECT COUNT(*) FROM ChiTietCaLam WHERE MaCaLam=@id", new { id = old.Chuoi("MaCaLam") }) > 0)
            throw new InvalidOperationException("Ca đã được chấm công. Hãy tạo ca mới để giữ đúng lịch sử.");
    }

    private async Task KiemTraChamCong(PhienGiaoDich s, Dictionary<string, object?>? old, Dictionary<string, object?> data)
    {
        var merged = old == null ? data : new Dictionary<string, object?>(old);
        foreach (var pair in data)
            merged[pair.Key] = pair.Value;
        if ((DateTime)merged["NgayLam"]! > XuLyDuLieu.HomNay)
            throw new ArgumentException("Không chấm công cho ngày tương lai.");
        var hours = await s.DocGiaTriAsync<decimal>("SELECT ThoiGianCaLam FROM CaLam WHERE MaCaLam=@id", new { id = merged["MaCaLam"] });
        if ((decimal)data["TongThoiGianLamThucTe"]! > hours)
            throw new ArgumentException("Số giờ thực tế không được vượt thời lượng ca.");
        if (await s.DocGiaTriAsync<int>("SELECT COUNT(*) FROM ChiTietCaLam a JOIN CaLam c ON c.MaCaLam=a.MaCaLam JOIN CaLam n ON n.MaCaLam=@shift WHERE a.MaNhanVien=@id AND a.NgayLam=@date AND a.MaCaLam<>@shift AND c.ThoiGianBatDau<n.ThoiGianKetThuc AND n.ThoiGianBatDau<c.ThoiGianKetThuc", new { id = merged["MaNhanVien"], date = merged["NgayLam"], shift = merged["MaCaLam"] }) > 0)
            throw new InvalidOperationException("Nhân viên đã chấm công ca trùng giờ.");
    }
}
