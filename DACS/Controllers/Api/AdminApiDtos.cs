using System;
using System.Collections.Generic;

namespace DACS.Models.DTOs
{
    // 1. DTO cho Dashboard (Tổng quan)
    public class OwnerDashboardDto
    {
        public decimal MonthlyRevenue { get; set; }
        public int NewOrdersThisWeek { get; set; }
        public int NewUsersThisMonth { get; set; }
        public int PendingCollectionRequests { get; set; } // Yêu cầu thu gom chờ xử lý
        public List<string> ChartLabels { get; set; }
        public List<decimal> ChartData { get; set; }
    }

    // 2. DTO cho Đơn hàng
    public class AdminOrderDto
    {
        public int Id { get; set; }
        public string MaDonHang { get; set; } // M_DonHang
        public DateTime NgayDat { get; set; }
        public string TenNguoiNhan { get; set; }
        public decimal TongTien { get; set; }
        public string TrangThai { get; set; } // Pending, Confirmed, etc.
        public string TrangThaiThanhToan { get; set; }
        public int SoLuongSanPham { get; set; }
    }

    // DTO Cập nhật trạng thái đơn
    public class UpdateStatusDto
    {
        public string MaDonHang { get; set; }
        public string TrangThaiMoi { get; set; }
    }

    // 3. DTO cho Yêu cầu Thu Gom (QuanLyXNK)
    public class CollectionRequestDto
    {
        public string Id { get; set; } // M_YeuCau
        public string TenNguoiYeuCau { get; set; }
        public string SoDienThoai { get; set; }
        public string DiaChi { get; set; } // Ghép từ XaPhuong, QuanHuyen
        public string LoaiSanPham { get; set; }
        public double KhoiLuong { get; set; }
        public DateTime NgayYeuCau { get; set; }
        public string TrangThai { get; set; }
        public string AnhMinhHoa { get; set; } // Full URL
    }

    // 4. DTO cho Sản phẩm
    public class AdminProductDto
    {
        public string MaSanPham { get; set; }
        public string TenSanPham { get; set; }
        public decimal Gia { get; set; }
        public double TonKho { get; set; }
        public string HinhAnh { get; set; }
    }
}