using System.Security.Claims;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.LopDungChung;
public sealed class NghiepVuSuaXe(XuLyDuLieu db)
{
    public const string TruyVanPhieu = """
        SELECT p.MaPhieuTiepNhan id,p.MaXe vehicleId,p.NgayLap createdOn,p.NgayHen dueDate,p.TrangThai status,
        x.BienXe licensePlate,x.LoaiXe vehicleType,x.HangSanXuat brand,x.MauSac color,
        k.TenKhachHang customerName,k.SoDienThoai phone,n.HoTen receivedBy,
        COALESCE((SELECT SUM(ThanhTien) FROM ChiTietPhieuTiepNhan d WHERE d.MaPhieuTiepNhan=p.MaPhieuTiepNhan),0) total,
        (SELECT COUNT(*) FROM HoaDon h WHERE h.MaPhieuTiepNhan=p.MaPhieuTiepNhan) paid
        FROM PhieuTiepNhan p JOIN Xe x ON x.MaXe=p.MaXe JOIN KhachHang k ON k.MaKhachHang=x.MaKhachHang JOIN NhanVien n ON n.MaNhanVien=p.MaNhanVien
        """;
    public Task<string> TaoPhieuAsync(ThongTinTiepNhan input, string employee) => db.TrongGiaoDichAsync(s => TaoPhieu(s, input, employee));
    public async Task<string> TaoPhieu(PhienGiaoDich s, ThongTinTiepNhan input, string employee)
    {
        // Các bước cùng dùng một giao dịch: có lỗi thì toàn bộ thay đổi được hoàn tác.
        await KiemTraXe(s, input);
        var (needs, prices) = await TinhDichVu(s, input.LoaiXe.Trim(), input.DichVu);
        await TruKho(s, needs);
        var customer = await LayKhachHang(s, input);
        var plate = input.BienSo.Trim().ToUpperInvariant();
        var vehicle = XuLyDuLieu.TaoMaMoi("XE");
        var receipt = XuLyDuLieu.TaoMaMoi("PTN");
        await s.ThucThiAsync("INSERT Xe(MaXe,MaKhachHang,BienXe,LoaiXe,HangSanXuat,MauSac) VALUES(@id,@customer,@plate,@type,@brand,@color)", new { id = vehicle, customer, plate, type = input.LoaiXe.Trim(), brand = input.HangXe.Trim(), color = input.MauSac.Trim() });
        await s.ThucThiAsync("INSERT PhieuTiepNhan(MaPhieuTiepNhan,MaXe,MaNhanVien,NgayLap,NgayHen,TrangThai) VALUES(@id,@vehicle,@employee,@today,@due,N'Chờ sửa')", new { id = receipt, vehicle, employee, today = XuLyDuLieu.HomNay, due = input.NgayHenTra.Date });
        foreach (var line in input.DichVu)
            await s.ThucThiAsync("INSERT ChiTietPhieuTiepNhan(MaPhieuTiepNhan,MaDichVu,SoLuong,ThanhTien) VALUES(@id,@service,@quantity,@amount)", new { id = receipt, service = line.MaDichVu.Trim(), quantity = line.SoLuong, amount = prices[line.MaDichVu] * line.SoLuong });
        await s.ThucThiAsync("INSERT WebPhieuTiepNhan(MaPhieuTiepNhan) VALUES(@id)", new { id = receipt });
        foreach (var need in needs)
            await s.ThucThiAsync("INSERT WebPhieuPhuTung(MaPhieuTiepNhan,MaPhuTung,SoLuong) VALUES(@id,@part,@quantity)", new { id = receipt, part = need.Key, quantity = need.Value });
        if (!string.IsNullOrWhiteSpace(input.MaTho))
            await PhanCong(s, receipt, new(input.MaTho, XuLyDuLieu.ThoiGianHienTai, input.NgayHenTra.Date.AddDays(1)));
        return receipt;
    }

    private static async Task KiemTraXe(PhienGiaoDich s, ThongTinTiepNhan input)
    {
        if (input.NgayHenTra.Date < XuLyDuLieu.HomNay || input.NgayHenTra.Date > XuLyDuLieu.HomNay.AddDays(90))
            throw new ArgumentException("Ngày hẹn trả phải từ hôm nay đến 90 ngày tới.");
        var plate = BienSoXe.ChuanHoa(input.BienSo);
        if (await s.DocGiaTriAsync<int>($"SELECT COUNT(*) FROM Xe x WITH(UPDLOCK,HOLDLOCK) JOIN PhieuTiepNhan p ON p.MaXe=x.MaXe WHERE {BienSoXe.KhoaSql("x.BienXe")}=@plate AND p.TrangThai IN(N'Chờ sửa',N'Đang sửa')", new { plate }) > 0)
            throw new InvalidOperationException("Xe đang có phiếu chưa hoàn thành.");
    }

    public Task<object> XemTruocPhieu(YeuCauXemTruocPhieu input) => db.TrongGiaoDichAsync<object>(async s =>
    {
        var (needs, prices) = await TinhDichVu(s, input.LoaiXe.Trim(), input.DichVu);
        var parts = new List<object>(needs.Count);
        var today = XuLyDuLieu.HomNay;
        foreach (var need in needs)
        {
            var part = (await s.DocBangAsync("SELECT TenPhuTung,SoLuongTon,TrangThai,HanSuDung FROM PhuTung WHERE MaPhuTung=@id", new { id = need.Key })).Single();
            var stock = part.SoNguyen("SoLuongTon");
            var issue = !(bool)part["TrangThai"]! ? "Ngừng sử dụng"
                : (DateTime)part["HanSuDung"]! < today ? "Hết hạn"
                : stock < need.Value ? "Thiếu tồn"
                : "";
            parts.Add(new { id = need.Key, name = part.Chuoi("TenPhuTung"), quantity = need.Value, stock, issue });
        }
        return new { total = input.DichVu.Sum(line => prices[line.MaDichVu] * line.SoLuong), parts };
    });

    private static async Task<(SortedDictionary<string, int> PhuTung, Dictionary<string, decimal> Gia)> TinhDichVu(
        PhienGiaoDich s, string mauXe, List<DichVuChon> dichVu)
    {
        if (dichVu.Count == 0 || dichVu.Select(x => x.MaDichVu.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != dichVu.Count)
            throw new ArgumentException("Chọn ít nhất một dịch vụ, không chọn trùng.");
        var needs = new SortedDictionary<string, int>();
        var prices = new Dictionary<string, decimal>();
        foreach (var line in dichVu.OrderBy(x => x.MaDichVu, StringComparer.OrdinalIgnoreCase))
        {
            if (line.SoLuong is < 1 or > 1000)
                throw new ArgumentException("Số lượng dịch vụ từ 1 đến 1000.");
            var service = (await s.DocBangAsync("SELECT TenDichVu,ThanhTienDichVu FROM DichVu WITH(UPDLOCK,HOLDLOCK) WHERE MaDichVu=@id AND TrangThai=1", new { id = line.MaDichVu.Trim() })).SingleOrDefault()
                ?? throw new InvalidOperationException("Dịch vụ không tồn tại hoặc đã ngừng áp dụng: " + line.MaDichVu);
            PhanLoaiXeMay.KiemTraDichVu(mauXe, service.Chuoi("TenDichVu"));
            prices[line.MaDichVu] = service.SoTien("ThanhTienDichVu");
            foreach (var part in await s.DocBangAsync("SELECT MaPhuTung,SoLuong FROM ChiTietDichVu WITH(HOLDLOCK) WHERE MaDichVu=@id", new { id = line.MaDichVu.Trim() }))
            {
                var id = part.Chuoi("MaPhuTung");
                var required = (long)needs.GetValueOrDefault(id) + (long)part.SoNguyen("SoLuong") * line.SoLuong;
                if (required > int.MaxValue)
                    throw new ArgumentException($"Số lượng phụ tùng {id} vượt giới hạn.");
                needs[id] = (int)required;
            }
        }

        return (needs, prices);
    }

    private static async Task TruKho(PhienGiaoDich s, SortedDictionary<string, int> needs)
    {
        foreach (var need in needs)
        {
            var updated = await s.ThucThiAsync("UPDATE PhuTung WITH(UPDLOCK) SET SoLuongTon=SoLuongTon-@quantity WHERE MaPhuTung=@id AND TrangThai=1 AND HanSuDung>=@today AND SoLuongTon>=@quantity", new { id = need.Key, quantity = need.Value, today = XuLyDuLieu.HomNay });
            if (updated != 1)
                throw new InvalidOperationException($"Phụ tùng {need.Key} không đủ tồn, hết hạn hoặc ngừng sử dụng. Phiếu chưa được lưu và kho được giữ nguyên.");
        }
    }

    private static async Task<string> LayKhachHang(PhienGiaoDich s, ThongTinTiepNhan input)
    {
        var customers = await s.DocBangAsync("SELECT MaKhachHang,TenKhachHang FROM KhachHang WITH(UPDLOCK,HOLDLOCK) WHERE SoDienThoai=@phone", new { phone = input.SoDienThoai });
        string customer;
        if (customers.Count == 0)
        {
            customer = XuLyDuLieu.TaoMaMoi("KH");
            await s.ThucThiAsync("INSERT KhachHang(MaKhachHang,TenKhachHang,SoDienThoai) VALUES(@id,@name,@phone)", new { id = customer, name = input.TenKhachHang.Trim(), phone = input.SoDienThoai });
        }
        else
        {
            if (!customers[0].Chuoi("TenKhachHang").Trim().Equals(input.TenKhachHang.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Số điện thoại đã có trong danh sách khách hàng. Hãy nhập đúng họ tên đã đăng ký.");
            customer = customers[0].Chuoi("MaKhachHang");
        }

        return customer;
    }

    public async Task PhanCong(PhienGiaoDich s, string id, YeuCauPhanCong input)
    {
        if (input.KetThuc <= input.BatDau || input.KetThuc - input.BatDau > TimeSpan.FromDays(90))
            throw new ArgumentException("Thời gian kết thúc phải sau bắt đầu, tối đa 90 ngày.");
        var jobs = await s.DocBangAsync("SELECT MaXe,TrangThai,NgayLap FROM PhieuTiepNhan WITH(UPDLOCK,HOLDLOCK) WHERE MaPhieuTiepNhan=@id", new { id });
        var job = jobs.SingleOrDefault() ?? throw new KeyNotFoundException("Không tìm thấy phiếu.");
        if (job.Chuoi("TrangThai")is not ("Chờ sửa" or "Đang sửa"))
            throw new InvalidOperationException("Chỉ phân công phiếu chưa hoàn tất.");
        if (input.BatDau.Date < (DateTime)job["NgayLap"]!)
            throw new ArgumentException("Thời gian phân công không được trước ngày lập phiếu.");
        var staff = await s.DocBangAsync("SELECT c.TenChucVu FROM NhanVien n WITH(UPDLOCK,HOLDLOCK) JOIN ChucVu c ON c.MaChucVu=n.MaChucVu WHERE n.MaNhanVien=@employee AND n.TrangThai=1 AND c.TenChucVu LIKE N'Thợ%'", new { employee = input.MaNhanVien });
        if (staff.Count != 1)
            throw new InvalidOperationException("Chọn thợ đang làm việc.");
        var vehicle = job.Chuoi("MaXe");
        var count = await s.DocGiaTriAsync<int>("SELECT COUNT(DISTINCT c.MaXe) FROM ChiTietPhanCong c WITH(UPDLOCK,HOLDLOCK) JOIN PhieuTiepNhan p ON p.MaXe=c.MaXe WHERE c.MaNhanVien=@employee AND c.MaXe<>@vehicle AND p.TrangThai IN(N'Chờ sửa',N'Đang sửa')", new { employee = input.MaNhanVien, vehicle });
        if (count >= (staff[0].Chuoi("TenChucVu").Contains("chính", StringComparison.OrdinalIgnoreCase) ? 3 : 2))
            throw new InvalidOperationException("Thợ đã nhận đủ số xe đang xử lý. Vui lòng chọn thợ khác.");
        await s.ThucThiAsync("IF EXISTS(SELECT 1 FROM ChiTietPhanCong WHERE MaNhanVien=@employee AND MaXe=@vehicle) UPDATE ChiTietPhanCong SET ThoiGianBatDau=@start,ThoiGianHoanThanh=@end WHERE MaNhanVien=@employee AND MaXe=@vehicle ELSE INSERT ChiTietPhanCong(MaNhanVien,MaXe,ThoiGianBatDau,ThoiGianHoanThanh) VALUES(@employee,@vehicle,@start,@end)", new { employee = input.MaNhanVien, vehicle, start = input.BatDau, end = input.KetThuc });
    }

    public Task<bool> DoiTrangThaiAsync(string id, string next, ClaimsPrincipal user) => db.TrongGiaoDichAsync(async s =>
    {
        var rows = await s.DocBangAsync("SELECT MaXe,TrangThai FROM PhieuTiepNhan WITH(UPDLOCK,HOLDLOCK) WHERE MaPhieuTiepNhan=@id", new { id });
        var job = rows.SingleOrDefault() ?? throw new KeyNotFoundException("Không tìm thấy phiếu tiếp nhận.");
        if (user.IsInRole("Mechanic") && await s.DocGiaTriAsync<int>("SELECT COUNT(*) FROM ChiTietPhanCong WHERE MaXe=@vehicle AND MaNhanVien=@employee", new { vehicle = job.Chuoi("MaXe"), employee = PhanQuyenNguoiDung.MaNhanVien(user) }) == 0)
            throw new UnauthorizedAccessException("Bạn chỉ được cập nhật xe được phân công.");
        var old = job.Chuoi("TrangThai");
        if (!(old == "Chờ sửa" && next == "Đang sửa" || old == "Đang sửa" && next == "Hoàn thành" || old == "Chờ sửa" && next == "Đã hủy" && !user.IsInRole("Mechanic")))
            throw new InvalidOperationException("Chuyển trạng thái không hợp lệ. Luồng: Chờ sửa → Đang sửa → Hoàn thành; chỉ hủy khi Chờ sửa.");
        if (next == "Đang sửa" && await s.DocGiaTriAsync<int>("SELECT COUNT(*) FROM ChiTietPhanCong WHERE MaXe=@id", new { id = job.Chuoi("MaXe") }) == 0)
            throw new InvalidOperationException("Cần phân công thợ trước khi bắt đầu sửa.");
        if (next == "Đã hủy")
        {
            if (await s.DocGiaTriAsync<int>("SELECT COUNT(*) FROM WebPhieuTiepNhan WHERE MaPhieuTiepNhan=@id", new { id }) == 0)
                throw new InvalidOperationException("Phiếu tạo từ ứng dụng khác chưa có bản chốt xuất kho. Hãy hủy tại ứng dụng đã tạo phiếu để tránh hoàn kho sai.");
            var parts = await s.DocBangAsync("SELECT MaPhuTung,SoLuong FROM WebPhieuPhuTung WHERE MaPhieuTiepNhan=@id", new { id });
            foreach (var part in parts)
                await s.ThucThiAsync("UPDATE PhuTung SET SoLuongTon=SoLuongTon+@quantity WHERE MaPhuTung=@part", new { quantity = part.SoNguyen("SoLuong"), part = part.Chuoi("MaPhuTung") });
        }

        await s.ThucThiAsync("UPDATE PhieuTiepNhan SET TrangThai=@next WHERE MaPhieuTiepNhan=@id", new { id, next });
        return true;
    });
}
