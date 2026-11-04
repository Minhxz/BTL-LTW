using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Models;
public sealed class BanNhapDatLich
{
    [StringLength(100)]
    [JsonPropertyName("customerName")]
    public string? TenKhachHang { get; set; }

    [StringLength(30)]
    [JsonPropertyName("phone")]
    public string? SoDienThoai { get; set; }

    [StringLength(50)]
    [JsonPropertyName("vehicleType")]
    public string? LoaiXe { get; set; }

    [StringLength(80)]
    [JsonPropertyName("brand")]
    public string? HangXe { get; set; }

    [StringLength(20)]
    [JsonPropertyName("licensePlate")]
    public string? BienSo { get; set; }

    [StringLength(150)]
    [JsonPropertyName("service")]
    public string? MaDichVu { get; set; }

    [StringLength(10)]
    [JsonPropertyName("date")]
    public string? Ngay { get; set; }

    [StringLength(5)]
    [JsonPropertyName("time")]
    public string? Gio { get; set; }

    [StringLength(1000)]
    [JsonPropertyName("note")]
    public string? GhiChu { get; set; }
}
