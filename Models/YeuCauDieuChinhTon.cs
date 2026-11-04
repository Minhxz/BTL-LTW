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
public sealed record YeuCauDieuChinhTon([Required, StringLength(10)][property: JsonPropertyName("partId")] string MaPhuTung, [Range(0, 1000000)][property: JsonPropertyName("stock")] int TonKho, [Required, StringLength(255, MinimumLength = 5)][property: JsonPropertyName("reason")] string LyDo);
