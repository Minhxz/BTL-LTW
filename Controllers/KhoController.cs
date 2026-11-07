using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Controllers;

// Người 4: nhập kho, kiểm kê và lịch sử điều chỉnh.
[ApiController, Authorize(Roles = "Manager"), Route("api/workshop/stock")]
public sealed class KhoController(XuLyDuLieu db) : ControllerBase
{
    [HttpGet("purchases")]
    public async Task<IActionResult> DanhSach(string? q = null, int page = 1, int pageSize = 12)
    {
        PhanTrang.KiemTra(page, pageSize, q);
        var args = new
        {
            q = "%" + q + "%",
            skip = (page - 1) * pageSize,
            size = pageSize
        };
        var total = await db.DocGiaTriAsync<int>("SELECT COUNT(*) FROM PhieuNhap WHERE MaPhieuNhap LIKE @q OR GhiChu LIKE @q", args);
        var items = await db.DocBangAsync("SELECT p.MaPhieuNhap id,p.NgayNhap date,n.HoTen employee,p.GhiChu note,p.TongTienNhap amount FROM PhieuNhap p JOIN NhanVien n ON n.MaNhanVien=p.MaNhanVienNhap WHERE p.MaPhieuNhap LIKE @q OR p.GhiChu LIKE @q ORDER BY p.NgayNhap DESC,p.MaPhieuNhap DESC OFFSET @skip ROWS FETCH NEXT @size ROWS ONLY", args);
        return Ok(new { items, total, page, totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)) });
    }

    [HttpGet("purchases/{id}")]
    public async Task<IActionResult> ChiTiet(string id)
    {
        if (await db.DocGiaTriAsync<int>("SELECT COUNT(*) FROM PhieuNhap WHERE MaPhieuNhap=@id", new { id }) == 0)
            throw new KeyNotFoundException("Không tìm thấy phiếu nhập.");
        return Ok(new { lines = await db.DocBangAsync("SELECT c.MaPhuTung partId,p.TenPhuTung name,c.SoLuongNhap quantity,c.ThanhTien/c.SoLuongNhap price,c.ThanhTien amount FROM ChiTietPhieuNhap c JOIN PhuTung p ON p.MaPhuTung=c.MaPhuTung WHERE c.MaPhieuNhap=@id ORDER BY c.MaPhuTung", new { id }) });
    }

    [HttpPost("purchases")]
    public async Task<IActionResult> NhapKho(ThongTinPhieuNhap input)
    {
        if (input.Ngay.Date > XuLyDuLieu.HomNay || input.Ngay.Year < 2000)
            throw new ArgumentException("Ngày nhập không được sau hôm nay và phải từ năm 2000.");
        // SQL so khớp mã không phân biệt hoa/thường nên kiểm tra trùng cũng phải bỏ qua hoa/thường.
        if (input.ChiTiet.Select(x => x.MaPhuTung.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != input.ChiTiet.Count)
            throw new ArgumentException("Không chọn trùng phụ tùng.");
        var id = await db.TrongGiaoDichAsync(async s =>
        {
            var code = XuLyDuLieu.TaoMaMoi("PN");
            var total = 0m;
            await s.ThucThiAsync("INSERT PhieuNhap(MaPhieuNhap,MaNhanVienNhap,NgayNhap,GhiChu,TongTienNhap) VALUES(@id,@employee,@date,@note,0)", new { id = code, employee = PhanQuyenNguoiDung.MaNhanVien(User), date = input.Ngay.Date, note = input.GhiChu?.Trim() ?? "" });
            foreach (var line in input.ChiTiet.OrderBy(x => x.MaPhuTung.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                var row = (await s.DocBangAsync("SELECT MaPhuTung,DonGiaNhap,TrangThai,HanSuDung FROM PhuTung WITH(UPDLOCK,HOLDLOCK) WHERE MaPhuTung=@id", new { id = line.MaPhuTung.Trim() })).SingleOrDefault()
                    ?? throw new KeyNotFoundException($"Không tìm thấy phụ tùng {line.MaPhuTung.Trim()}.");
                var partId = row.Chuoi("MaPhuTung");
                if (!Convert.ToBoolean(row["TrangThai"]))
                    throw new InvalidOperationException($"Phụ tùng {partId} đã ngừng sử dụng, không nhập thêm.");
                if (Convert.ToDateTime(row["HanSuDung"]) < XuLyDuLieu.HomNay)
                    throw new InvalidOperationException($"Phụ tùng {partId} đã hết hạn sử dụng, không nhận nhập kho.");
                // Thành tiền = giá nhập của lô × số lượng; không nhận giá từ trình duyệt.
                var amount = row.SoTien("DonGiaNhap") * line.SoLuong;
                total += amount;
                if (await s.ThucThiAsync("UPDATE PhuTung SET SoLuongTon=SoLuongTon+@qty WHERE MaPhuTung=@id AND SoLuongTon+@qty<=1000000", new { id = partId, qty = line.SoLuong }) != 1)
                    throw new InvalidOperationException($"Tồn kho phụ tùng {partId} vượt mức 1.000.000.");
                await s.ThucThiAsync("INSERT ChiTietPhieuNhap(MaPhieuNhap,MaPhuTung,SoLuongNhap,ThanhTien) VALUES(@id,@part,@qty,@amount)", new { id = code, part = partId, qty = line.SoLuong, amount });
            }

            await s.ThucThiAsync("UPDATE PhieuNhap SET TongTienNhap=@total WHERE MaPhieuNhap=@id", new { id = code, total });
            return code;
        });
        return StatusCode(201, new { id, message = "Đã lưu phiếu nhập và cộng kho." });
    }

    [HttpPost("adjust")]
    public async Task<IActionResult> DieuChinhTon(YeuCauDieuChinhTon input)
    {
        // Lý do bắt buộc: chặn cả trường hợp chỉ thêm khoảng trắng cho đủ 5 ký tự.
        var reason = input.LyDo.Trim();
        if (reason.Length < 5)
            throw new ArgumentException("Nhập lý do điều chỉnh tối thiểu 5 ký tự.");
        await db.TrongGiaoDichAsync(async s =>
        {
            var part = (await s.DocBangAsync("SELECT MaPhuTung,SoLuongTon,DonGiaNhap FROM PhuTung WITH(UPDLOCK,HOLDLOCK) WHERE MaPhuTung=@id", new { id = input.MaPhuTung.Trim() })).SingleOrDefault() ?? throw new KeyNotFoundException("Không tìm thấy phụ tùng.");
            var partId = part.Chuoi("MaPhuTung");
            if (part.SoNguyen("SoLuongTon") == input.TonKho)
                throw new ArgumentException("Tồn mới trùng tồn hiện tại.");
            await s.ThucThiAsync("INSERT DieuChinhTonKho(MaDieuChinh,MaPhuTung,MaNhanVien,NgayDieuChinh,SoLuongCu,SoLuongMoi,DonGiaNhap,LyDo) VALUES(@id,@part,@employee,@date,@old,@next,@price,@reason)", new { id = XuLyDuLieu.TaoMaMoi("DC"), part = partId, employee = PhanQuyenNguoiDung.MaNhanVien(User), date = XuLyDuLieu.HomNay, old = part.SoNguyen("SoLuongTon"), next = input.TonKho, price = part.SoTien("DonGiaNhap"), reason });
            await s.ThucThiAsync("UPDATE PhuTung SET SoLuongTon=@stock WHERE MaPhuTung=@id", new { id = partId, stock = input.TonKho });
            return true;
        });
        return Ok(new { message = "Đã điều chỉnh tồn và lưu lịch sử." });
    }

    [HttpGet("history")]
    public async Task<IActionResult> LichSuDieuChinh(string? q = null, int page = 1, int pageSize = 12)
    {
        PhanTrang.KiemTra(page, pageSize, q);
        var args = new
        {
            q = "%" + q + "%",
            skip = (page - 1) * pageSize,
            size = pageSize
        };
        const string union = """
        SELECT t.MaDieuChinh id,t.MaPhuTung partId,N'Tồn kho' kind,t.NgayDieuChinh date,CAST(t.SoLuongCu AS decimal(18,0)) oldValue,CAST(t.SoLuongMoi AS decimal(18,0)) newValue,t.LyDo reason,n.HoTen employee FROM DieuChinhTonKho t JOIN NhanVien n ON n.MaNhanVien=t.MaNhanVien
        UNION ALL
        SELECT t.MaDieuChinhGia,t.MaPhuTung,N'Giá bán',t.NgayDieuChinh,t.GiaBanCu,t.GiaBanMoi,t.LyDo,n.HoTen FROM DieuChinhGiaBan t JOIN NhanVien n ON n.MaNhanVien=t.MaNhanVien
        """;
        var total = await db.DocGiaTriAsync<int>("SELECT COUNT(*) FROM (" + union + ")h WHERE partId LIKE @q OR reason LIKE @q", args);
        var items = await db.DocBangAsync("SELECT * FROM (" + union + ")h WHERE partId LIKE @q OR reason LIKE @q ORDER BY date DESC,id DESC OFFSET @skip ROWS FETCH NEXT @size ROWS ONLY", args);
        return Ok(new { items, total, page, totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)) });
    }
}
