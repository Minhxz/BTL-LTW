using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.LopDungChung;
public static class PhanTrang
{
    public static void KiemTra(int page, int size, string? q = null)
    {
        if (page < 1 || page > 100000 || size < 1 || size > 100 || q?.Length > 150)
            throw new ArgumentException("Bộ lọc hoặc số trang không hợp lệ.");
    }

    public static (DateTime? From, DateTime? To) KhoangNgay(string? from, string? to)
    {
        DateTime? DocDuLieuNhap(string? v)
        {
            if (string.IsNullOrWhiteSpace(v))
                return null;
            if (!DateTime.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                throw new ArgumentException("Ngày lọc không hợp lệ.");
            return date;
        }

        var a = DocDuLieuNhap(from);
        var b = DocDuLieuNhap(to);
        if (a > b)
            throw new ArgumentException("Ngày bắt đầu không được sau ngày kết thúc.");
        return (a, b);
    }
}
