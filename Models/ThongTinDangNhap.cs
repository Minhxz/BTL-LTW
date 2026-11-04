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
public sealed record ThongTinDangNhap([Required, StringLength(50)][property: JsonPropertyName("username")] string TenDangNhap, [Required, StringLength(200)][property: JsonPropertyName("password")] string MatKhau);
