using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.LopDungChung;
public sealed class XuLyDuLieu(IConfiguration configuration)
{
    public string ChuoiKetNoi => configuration.GetConnectionString("QuanLySuaXe") ?? throw new InvalidOperationException("Chưa cấu hình kết nối QuanLySuaXe.");
    public static DateTime ThoiGianHienTai => DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime;
    public static DateTime HomNay => ThoiGianHienTai.Date;

    public static string TaoMaMoi(string prefix) => prefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(5))[..(10 - prefix.Length)];
    public static string BamMatKhau(string password) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
    public async Task<T> DocAsync<T>(Func<PhienGiaoDich, Task<T>> action)
    {
        await using var connection = new SqlConnection(ChuoiKetNoi);
        await connection.OpenAsync();
        return await action(new PhienGiaoDich(connection, null));
    }

    public Task<List<Dictionary<string, object?>>> DocBangAsync(string sql, object? args = null) => DocAsync(s => s.DocBangAsync(sql, args));
    public Task<T?> DocGiaTriAsync<T>(string sql, object? args = null) => DocAsync(s => s.DocGiaTriAsync<T>(sql, args));
    public Task<int> ThucThiAsync(string sql, object? args = null) => DocAsync(s => s.ThucThiAsync(sql, args));
    public async Task<T> TrongGiaoDichAsync<T>(Func<PhienGiaoDich, Task<T>> action)
    {
        await using var connection = new SqlConnection(ChuoiKetNoi);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var value = await action(new PhienGiaoDich(connection, transaction));
            await transaction.CommitAsync();
            return value;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task KhoiTaoAsync(string root)
    {
        var count = await DocGiaTriAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE name IN ('NhanVien','KhachHang','Xe','DichVu','PhuTung','PhieuTiepNhan','ChiTietPhieuTiepNhan','HoaDon','ChiTietPhanCong','PhieuNhap','ChiTietPhieuNhap','NhaCungCap','ChucVu','CaLam','ChiTietCaLam','KhoanDieuChinh','ChucVuDieuChinh','ChiTietDichVu','DieuChinhTonKho','DieuChinhGiaBan') AND schema_id=SCHEMA_ID('dbo')");
        if (count != 20)
            throw new InvalidOperationException("QuanLySuaXe chưa có đủ 20 bảng của nhóm. Xem README để chọn đúng SQL Server; ứng dụng không tạo lại hoặc xóa CSDL.");
        await TrongGiaoDichAsync(async s => await s.ThucThiAsync(await File.ReadAllTextAsync(Path.Combine(root, "database", "BoSungChoWeb.sql"))));
    }
}

public sealed class PhienGiaoDich(SqlConnection connection, SqlTransaction? transaction)
{
    private SqlCommand TaoLenh(string sql, object? args)
    {
        var command = new SqlCommand(sql, connection, transaction)
        {
            CommandTimeout = 30
        };
        var values = args is IDictionary<string, object?> dictionary ? dictionary : args?.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(args));
        if (values != null)
            foreach (var pair in values)
                command.Parameters.AddWithValue("@" + pair.Key.TrimStart('@'), pair.Value is DateOnly d ? d.ToDateTime(TimeOnly.MinValue) : pair.Value ?? DBNull.Value);
        return command;
    }

    public async Task<List<Dictionary<string, object?>>> DocBangAsync(string sql, object? args = null)
    {
        using var command = TaoLenh(sql, args);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }

        return rows;
    }

    public async Task<T?> DocGiaTriAsync<T>(string sql, object? args = null)
    {
        using var command = TaoLenh(sql, args);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T), CultureInfo.InvariantCulture);
    }

    public async Task<int> ThucThiAsync(string sql, object? args = null)
    {
        using var command = TaoLenh(sql, args);
        return await command.ExecuteNonQueryAsync();
    }
}

public static class DocGiaTri
{
    public static string Chuoi(this Dictionary<string, object?> row, string name) => Convert.ToString(row.GetValueOrDefault(name), CultureInfo.InvariantCulture) ?? "";
    public static decimal SoTien(this Dictionary<string, object?> row, string name) => Convert.ToDecimal(row.GetValueOrDefault(name) ?? 0);
    public static int SoNguyen(this Dictionary<string, object?> row, string name) => Convert.ToInt32(row.GetValueOrDefault(name) ?? 0);
}
