using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace QLyCuaHangSuaXe.LopDungChung;

public sealed class XuLyDuLieu(IConfiguration cauHinh)
{
    private static readonly string[] CacBangCuaNhom =
    [
        "NhanVien",
        "KhachHang",
        "Xe",
        "DichVu",
        "PhuTung",
        "PhieuTiepNhan",
        "ChiTietPhieuTiepNhan",
        "HoaDon",
        "ChiTietPhanCong",
        "PhieuNhap",
        "ChiTietPhieuNhap",
        "NhaCungCap",
        "ChucVu",
        "CaLam",
        "ChiTietCaLam",
        "KhoanDieuChinh",
        "ChucVuDieuChinh",
        "ChiTietDichVu",
        "DieuChinhTonKho",
        "DieuChinhGiaBan",
    ];
    public string ChuoiKetNoi =>
        cauHinh.GetConnectionString("QuanLySuaXe")
        ?? throw new InvalidOperationException("Chưa cấu hình kết nối QuanLySuaXe.");
    public static DateTime ThoiGianHienTai =>
        DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime;
    public static DateTime HomNay => ThoiGianHienTai.Date;

    public static string TaoMaMoi(string tienTo)
    {
        string chuoiNgauNhien = Convert.ToHexString(RandomNumberGenerator.GetBytes(5));
        return tienTo + chuoiNgauNhien[..(10 - tienTo.Length)];
    }

    public static string BamMatKhau(string matKhau)
    {
        byte[] maBam = SHA256.HashData(Encoding.UTF8.GetBytes(matKhau));
        return Convert.ToHexString(maBam);
    }

    public async Task<T> DocAsync<T>(Func<PhienGiaoDich, Task<T>> thaoTac)
    {
        await using SqlConnection ketNoi = new SqlConnection(ChuoiKetNoi);
        await ketNoi.OpenAsync();
        return await thaoTac(new PhienGiaoDich(ketNoi, null));
    }

    public Task<List<Dictionary<string, object?>>> DocBangAsync(
        string cauLenhSql,
        object? thamSo = null
    )
    {
        return DocAsync(phien => phien.DocBangAsync(cauLenhSql, thamSo));
    }

    public Task<T?> DocGiaTriAsync<T>(string cauLenhSql, object? thamSo = null)
    {
        return DocAsync(phien => phien.DocGiaTriAsync<T>(cauLenhSql, thamSo));
    }

    public Task<int> ThucThiAsync(string cauLenhSql, object? thamSo = null)
    {
        return DocAsync(phien => phien.ThucThiAsync(cauLenhSql, thamSo));
    }

    public async Task<T> TrongGiaoDichAsync<T>(Func<PhienGiaoDich, Task<T>> thaoTac)
    {
        await using SqlConnection ketNoi = new SqlConnection(ChuoiKetNoi);
        await ketNoi.OpenAsync();
        await using SqlTransaction giaoDich = (SqlTransaction)
            await ketNoi.BeginTransactionAsync(IsolationLevel.Serializable);
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

    public async Task KhoiTaoAsync(string thuMucGoc)
    {
        string sqlDemBang =
            "Select Count(*) "
            + "From sys.tables "
            + "Where name In ('"
            + string.Join("','", CacBangCuaNhom)
            + "') And schema_id = Schema_Id('dbo')";
        int soBang = await DocGiaTriAsync<int>(sqlDemBang);
        if (soBang != CacBangCuaNhom.Length)
        {
            throw new InvalidOperationException(
                "QuanLySuaXe chưa có đủ 20 bảng của nhóm. Xem README để chọn đúng SQL Server; ứng dụng không tạo lại hoặc xóa CSDL."
            );
        }

        string sqlBoSung = await File.ReadAllTextAsync(
            Path.Combine(thuMucGoc, "database", "BoSungChoWeb.sql")
        );
        await TrongGiaoDichAsync(phien => phien.ThucThiAsync(sqlBoSung));
    }
}

public sealed class PhienGiaoDich(SqlConnection ketNoi, SqlTransaction? giaoDich)
{
    public async Task<string> TaoMaTuDongAsync(string tenBang, string tenCot, string tienTo)
    {
        int doDaiPhanSo = 10 - tienTo.Length;
        string tenBoDem = "WebMa_" + tenBang;
        string sqlLaySo = $"""
            DECLARE @ketQuaKhoa int;
            EXEC @ketQuaKhoa = sys.sp_getapplock
                @Resource = @tenBoDem, @LockMode = 'Exclusive',
                @LockOwner = 'Transaction', @LockTimeout = 10000;
            IF @ketQuaKhoa < 0
                THROW 50001, N'Chưa thể cấp mã. Vui lòng thử lại.', 1;

            DECLARE @soDaDung bigint = (
                SELECT COALESCE(MAX(TRY_CONVERT(bigint, SUBSTRING([{tenCot}], @viTri, 10))), 0)
                FROM [{tenBang}] WITH (TABLOCKX, HOLDLOCK)
                WHERE LEFT([{tenCot}], @doDaiTienTo) = @tienTo
            );
            DECLARE @soBatDau varchar(20) = CONVERT(varchar(20), @soDaDung + 1);
            IF OBJECT_ID(N'dbo.{tenBoDem}', N'SO') IS NULL
                EXEC('CREATE SEQUENCE dbo.[{tenBoDem}] AS bigint START WITH ' + @soBatDau + ' INCREMENT BY 1 NO CYCLE');
            ELSE IF (SELECT CONVERT(bigint, current_value) FROM sys.sequences WHERE object_id = OBJECT_ID(N'dbo.{tenBoDem}')) <= @soDaDung
                EXEC('ALTER SEQUENCE dbo.[{tenBoDem}] RESTART WITH ' + @soBatDau);

            EXEC('SELECT NEXT VALUE FOR dbo.[{tenBoDem}]');
            """;
        long soTiepTheo = await DocGiaTriAsync<long>(
            sqlLaySo,
            new
            {
                viTri = tienTo.Length + 1,
                doDaiTienTo = tienTo.Length,
                tienTo,
                tenBoDem,
            }
        );
        string phanSo = soTiepTheo.ToString(CultureInfo.InvariantCulture);
        if (phanSo.Length > doDaiPhanSo)
        {
            throw new InvalidOperationException("Đã hết mã tự động cho danh mục này.");
        }

        return tienTo + phanSo.PadLeft(doDaiPhanSo, '0');
    }

    private SqlCommand TaoLenh(string cauLenhSql, object? thamSo)
    {
        SqlCommand lenh = new SqlCommand(cauLenhSql, ketNoi, giaoDich) { CommandTimeout = 30 };
        IDictionary<string, object?>? danhSachThamSo = thamSo is IDictionary<string, object?> tuDien
            ? tuDien
            : thamSo
                ?.GetType()
                .GetProperties()
                .ToDictionary(thuocTinh => thuocTinh.Name, thuocTinh => thuocTinh.GetValue(thamSo));
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
                _ => cap.Value!,
            };
            lenh.Parameters.AddWithValue("@" + cap.Key.TrimStart('@'), giaTri);
        }

        return lenh;
    }

    public async Task<List<Dictionary<string, object?>>> DocBangAsync(
        string cauLenhSql,
        object? thamSo = null
    )
    {
        using SqlCommand lenh = TaoLenh(cauLenhSql, thamSo);
        await using SqlDataReader boDoc = await lenh.ExecuteReaderAsync();
        List<Dictionary<string, object?>> danhSachDong = new List<Dictionary<string, object?>>();
        while (await boDoc.ReadAsync())
        {
            Dictionary<string, object?> dong = new Dictionary<string, object?>(
                StringComparer.OrdinalIgnoreCase
            );
            for (int chiSo = 0; chiSo < boDoc.FieldCount; chiSo++)
            {
                dong[boDoc.GetName(chiSo)] = boDoc.IsDBNull(chiSo) ? null : boDoc.GetValue(chiSo);
            }

            danhSachDong.Add(dong);
        }

        return danhSachDong;
    }

    public async Task<T?> DocGiaTriAsync<T>(string cauLenhSql, object? thamSo = null)
    {
        using SqlCommand lenh = TaoLenh(cauLenhSql, thamSo);
        object? giaTri = await lenh.ExecuteScalarAsync();
        if (giaTri is null or DBNull)
        {
            return default;
        }

        Type kieuDich = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Convert.ChangeType(giaTri, kieuDich, CultureInfo.InvariantCulture);
    }

    public async Task<int> ThucThiAsync(string cauLenhSql, object? thamSo = null)
    {
        using SqlCommand lenh = TaoLenh(cauLenhSql, thamSo);
        return await lenh.ExecuteNonQueryAsync();
    }
}

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
