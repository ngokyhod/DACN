// DACS/Models/ViewModels/KhachHangDashboardViewModel.cs
using System.Collections.Generic; // Thêm using này

namespace DACS.Models.ViewModels
{
    public class KhachHangDashboardViewModel
    {
        // Thông tin cơ bản lấy từ profile NguoiMua
        public string? TenNguoiMua { get; set; }
        public string? EmailNguoiMua { get; set; }
        public string? SdtNguoiMua { get; set; }
        public string? DiaChiMacDinh { get; set; } // Hoặc một ViewModel địa chỉ phức tạp hơn

        // Danh sách các đơn hàng gần đây
        public List<OrderSummaryViewModel>? RecentOrders { get; set; }
        public List<YeuCauThuGom> RecentCollectionRequests { get; internal set; }

        public Models.KhachHang KhachHangInfo { get; set; }
        public List<AIGoiYThuMuaViewModel> AIGoiYThuMua { get; set; } = new List<AIGoiYThuMuaViewModel>();

        public KhachHangDashboardViewModel()
        {
            RecentOrders = new List<OrderSummaryViewModel>(); // Khởi tạo list
        }
    }
}