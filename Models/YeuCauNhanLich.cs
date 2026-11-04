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
public sealed record YeuCauNhanLich([property: JsonPropertyName("dueDate")] DateTime NgayHenTra, [Required, StringLength(30)][property: JsonPropertyName("color")] string MauSac, [StringLength(10)][property: JsonPropertyName("mechanicId")] string? MaTho);
