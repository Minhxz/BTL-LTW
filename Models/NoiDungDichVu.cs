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
public sealed record NoiDungDichVu([StringLength(8000)][property: JsonPropertyName("description")] string? MoTa, [StringLength(250)][property: JsonPropertyName("imageUrl")] string? DuongDanAnh);
