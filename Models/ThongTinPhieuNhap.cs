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
public sealed record ThongTinPhieuNhap([property: JsonPropertyName("date")] DateTime Ngay, [StringLength(255)][property: JsonPropertyName("note")] string? GhiChu, [Required, MinLength(1), MaxLength(100)][property: JsonPropertyName("lines")] List<PhuTungNhap> ChiTiet);
