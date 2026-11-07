using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace QLyCuaHangSuaXe.LopDungChung;

// Người 3 chủ trì: kết nối SQL Server, truy vấn có tham số và giao dịch dùng chung cho cả 5 phần.
public sealed class XuLyDuLieu(IConfiguration configuration)
{
    // 20 bảng gốc của bài BTL-LTTQ; web chỉ dùng lại, không tạo hay xóa các bảng này.
    private static readonly string[] CacBangCuaNhom =
    [
        "NhanVien", "KhachHang", "Xe", "DichVu", "PhuTung",
        "PhieuTiepNhan", "ChiTietPhieuTiepNhan", "HoaDon", "ChiTietPhanCong", "PhieuNhap",
        "ChiTietPhieuNhap", "NhaCungCap", "ChucVu", "CaLam", "ChiTietCaLam",
        "KhoanDieuChinh", "ChucVuDieuChinh", "ChiTietDichVu", "DieuChinhTonKho", "DieuChinhGiaBan"
    ];

    public string ChuoiKetNoi =>
        configuration.GetConnectionString("QuanLySuaXe")
        ?? throw new InvalidOperationException("Chưa cấu hình kết nối QuanLySuaXe.");

    // Giờ Việt Nam (UTC+7), không phụ thuộc múi giờ của máy chạy web.
    public static DateTime ThoiGianHienTai => DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime;

    public static DateTime HomNay => ThoiGianHienTai.Date;

    // Mã mới dài đúng 10 ký tự: tiền tố + chuỗi hex ngẫu nhiên. Ví dụ: PN3FA91C2B.
    public static string TaoMaMoi(string tienTo)
    {
        string chuoiNgauNhien = Convert.ToHexString(RandomNumberGenerator.GetBytes(5));

        return tienTo + chuoiNgauNhien[..(10 - tienTo.Length)];
    }

    // SHA-256 để tương thích mật khẩu với ứng dụng BTL-LTTQ.
    public static string BamMatKhau(string matKhau)
    {
        byte[] maBam = SHA256.HashData(Encoding.UTF8.GetBytes(matKhau));

        return Convert.ToHexString(maBam);
    }

    // Mở một kết nối, chạy thao tác đọc rồi đóng kết nối.
    public async Task<T> DocAsync<T>(Func<PhienGiaoDich, Task<T>> thaoTac)
    {
        await using SqlConnection ketNoi = new SqlConnection(ChuoiKetNoi);
        await ketNoi.OpenAsync();

        return await thaoTac(new PhienGiaoDich(ketNoi, null));
    }

    public Task<List<Dictionary<string, object?>>> DocBangAsync(string sql, object? thamSo = null)
    {
        return DocAsync(s => s.DocBangAsync(sql, thamSo));
    }

    public Task<T?> DocGiaTriAsync<T>(string sql, object? thamSo = null)
    {
        return DocAsync(s => s.DocGiaTriAsync<T>(sql, thamSo));
    }

    public Task<int> ThucThiAsync(string sql, object? thamSo = null)
    {
        return DocAsync(s => s.ThucThiAsync(sql, thamSo));
    }

    // Chạy nhiều câu lệnh trong một giao dịch: lỗi ở bất kỳ bước nào thì hoàn tác toàn bộ.
    public async Task<T> TrongGiaoDichAsync<T>(Func<PhienGiaoDich, Task<T>> thaoTac)
    {
        await using SqlConnection ketNoi = new SqlConnection(ChuoiKetNoi);
        await ketNoi.OpenAsync();
        await using SqlTransaction giaoDich = (SqlTransaction)await ketNoi.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            T ketQua = await thaoTac(new PhienGiaoDich(ketNoi, giaoDich));
            await giaoDich.CommitAsync();

            return ketQua;
        }
        catch
        {
            await giaoDich.RollbackAsync();
            throw;
        }
    }

    // Kiểm tra CSDL của nhóm khi khởi động, rồi tạo các bảng mở rộng của web nếu chưa có.
    public async Task KhoiTaoAsync(string thuMucGoc)
    {
        string sqlDemBang = "Select Count(*) " +
            "From sys.tables " +
            "Where name In ('" + string.Join("','", CacBangCuaNhom) + "') And schema_id = Schema_Id('dbo')";

        int soBang = await DocGiaTriAsync<int>(sqlDemBang);

        if (soBang != CacBangCuaNhom.Length)
        {
            throw new InvalidOperationException("QuanLySuaXe chưa có đủ 20 bảng của nhóm. Xem README để chọn đúng SQL Server; ứng dụng không tạo lại hoặc xóa CSDL.");
        }

        string sqlBoSung = await File.ReadAllTextAsync(Path.Combine(thuMucGoc, "database", "BoSungChoWeb.sql"));

        await TrongGiaoDichAsync(s => s.ThucThiAsync(sqlBoSung));
    }
}

// Một phiên làm việc trên kết nối đang mở (có hoặc không có giao dịch).
public sealed class PhienGiaoDich(SqlConnection ketNoi, SqlTransaction? giaoDich)
{
    // Tạo câu lệnh có tham số. thamSo là đối tượng ẩn danh new { id = ... } hoặc Dictionary.
    private SqlCommand TaoLenh(string sql, object? thamSo)
    {
        SqlCommand lenh = new SqlCommand(sql, ketNoi, giaoDich)
        {
            CommandTimeout = 30
        };

        IDictionary<string, object?>? danhSachThamSo = thamSo is IDictionary<string, object?> tuDien
            ? tuDien
            : thamSo?.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(thamSo));

        if (danhSachThamSo == null)
        {
            return lenh;
        }

        foreach (KeyValuePair<string, object?> cap in danhSachThamSo)
        {
            object giaTri = cap.Value switch
            {
                DateOnly ngay => ngay.ToDateTime(TimeOnly.MinValue),
                null => DBNull.Value,
                _ => cap.Value!
            };

            lenh.Parameters.AddWithValue("@" + cap.Key.TrimStart('@'), giaTri);
        }

        return lenh;
    }

    // Đọc nhiều dòng; mỗi dòng là Dictionary tên cột → giá trị (không phân biệt hoa/thường).
    public async Task<List<Dictionary<string, object?>>> DocBangAsync(string sql, object? thamSo = null)
    {
        using SqlCommand lenh = TaoLenh(sql, thamSo);
        await using SqlDataReader boDoc = await lenh.ExecuteReaderAsync();

        List<Dictionary<string, object?>> danhSachDong = new List<Dictionary<string, object?>>();

        while (await boDoc.ReadAsync())
        {
            Dictionary<string, object?> dong = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < boDoc.FieldCount; i++)
            {
                dong[boDoc.GetName(i)] = boDoc.IsDBNull(i) ? null : boDoc.GetValue(i);
            }

            danhSachDong.Add(dong);
        }

        return danhSachDong;
    }

    // Đọc một giá trị (ô đầu tiên). Không có dữ liệu thì trả về giá trị mặc định của kiểu T.
    public async Task<T?> DocGiaTriAsync<T>(string sql, object? thamSo = null)
    {
        using SqlCommand lenh = TaoLenh(sql, thamSo);
        object? giaTri = await lenh.ExecuteScalarAsync();

        if (giaTri is null or DBNull)
        {
            return default;
        }

        Type kieuDich = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        return (T)Convert.ChangeType(giaTri, kieuDich, CultureInfo.InvariantCulture);
    }

    // Chạy Insert/Update/Delete, trả về số dòng bị ảnh hưởng.
    public async Task<int> ThucThiAsync(string sql, object? thamSo = null)
    {
        using SqlCommand lenh = TaoLenh(sql, thamSo);

        return await lenh.ExecuteNonQueryAsync();
    }
}

// Hàm tiện ích đọc giá trị từ một dòng kết quả.
public static class DocGiaTri
{
    public static string Chuoi(this Dictionary<string, object?> dong, string tenCot)
    {
        return Convert.ToString(dong.GetValueOrDefault(tenCot), CultureInfo.InvariantCulture) ?? "";
    }

    public static decimal SoTien(this Dictionary<string, object?> dong, string tenCot)
    {
        return Convert.ToDecimal(dong.GetValueOrDefault(tenCot) ?? 0);
    }

    public static int SoNguyen(this Dictionary<string, object?> dong, string tenCot)
    {
        return Convert.ToInt32(dong.GetValueOrDefault(tenCot) ?? 0);
    }
}
