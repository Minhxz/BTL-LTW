using QLyCuaHangSuaXe.LopDungChung;
using QLyCuaHangSuaXe.Models;

namespace QLyCuaHangSuaXe.Models
{
    public class ThongTinLoi
    {
        public string? MaYeuCau { get; set; }
        public bool HienMaYeuCau => !string.IsNullOrEmpty(MaYeuCau);
    }
}
