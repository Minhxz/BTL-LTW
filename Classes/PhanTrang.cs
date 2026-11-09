using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.LopDungChung;

public static class PhanTrang
{
    public static void KiemTra(int trang, int kichThuoc, string? tuKhoa = null)
    {
        if (trang < 1 || trang > 100000 || kichThuoc < 1 || kichThuoc > 100 || tuKhoa?.Length > 150)
            throw new ArgumentException("Bộ lọc hoặc số trang không hợp lệ.");
    }

    public static (DateTime? From, DateTime? To) KhoangNgay(string? ngayBatDau, string? ngayKetThuc)
    {
        DateTime? DocDuLieuNhap(string? giaTri)
        {
            if (string.IsNullOrWhiteSpace(giaTri))
                return null;
            if (
                !DateTime.TryParseExact(
                    giaTri,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date
                )
            )
                throw new ArgumentException("Ngày lọc không hợp lệ.");
            return date;
        }

        var kyTuThuNhat = DocDuLieuNhap(ngayBatDau);
        var kyTuThuHai = DocDuLieuNhap(ngayKetThuc);
        if (kyTuThuNhat > kyTuThuHai)
            throw new ArgumentException("Ngày bắt đầu không được sau ngày kết thúc.");
        return (kyTuThuNhat, kyTuThuHai);
    }
}
