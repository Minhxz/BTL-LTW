using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace QLyCuaHangSuaXe.Models;

// Người 2: nhân viên nhận lịch hẹn online và chuyển thành phiếu tiếp nhận.
public sealed class YeuCauNhanLich
{
    [JsonPropertyName("dueDate")]
    public DateTime NgayHenTra { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập màu xe."), StringLength(30)]
    [JsonPropertyName("color")]
    public string MauSac { get; set; } = "";

    [StringLength(10)]
    [JsonPropertyName("mechanicId")]
    public string? MaTho { get; set; }
}
