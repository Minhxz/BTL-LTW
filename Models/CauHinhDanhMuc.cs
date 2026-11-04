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
public sealed record CauHinhDanhMuc([property: JsonPropertyName("table")] string Bang, [property: JsonPropertyName("title")] string TieuDe, [property: JsonPropertyName("keys")] string[] Khoa, [property: JsonPropertyName("fields")] TruongDanhMuc[] CacTruong, [property: JsonPropertyName("roles")] string VaiTro = "Manager");
