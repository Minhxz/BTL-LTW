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
public sealed record PhuTungNhap([Required, StringLength(10)][property: JsonPropertyName("partId")] string MaPhuTung, [Range(1, 100000)][property: JsonPropertyName("quantity")] int SoLuong);
