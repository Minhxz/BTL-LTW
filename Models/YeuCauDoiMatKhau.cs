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
public sealed record YeuCauDoiMatKhau([Required][property: JsonPropertyName("oldPassword")] string MatKhauCu, [Required, StringLength(100, MinimumLength = 6)][property: JsonPropertyName("newPassword")] string MatKhauMoi);
