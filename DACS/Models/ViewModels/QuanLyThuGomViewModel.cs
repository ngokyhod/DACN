using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DACS.Models.ViewModels
{
    public class AIKhoHangSuggestionViewModel
    {
        public string MaKho { get; set; } = "";
        public string? TenKho { get; set; }
        public double MatchScore { get; set; }
        public string? SuggestedReason { get; set; }
    }

    // ViewModel chính cho View Index
    public class QuanLyThuGomViewModel
    {
        public List<YeuCauListItemViewModel> DanhSachYeuCau { get; set; } = new List<YeuCauListItemViewModel>();
        public ThuGomStatisticsViewModel? Statistics { get; set; }

        // Bộ lọc
        public string? SearchTerm { get; set; }
        public DateTime? DateFilter { get; set; }
        public string? StatusFilter { get; set; }
        public string? CollectorFilter { get; set; } // Lọc theo M_QuanLy (ID người thu gom)

        // Tùy chọn cho bộ lọc dropdown
        public List<SelectListItem> StatusOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> CollectorOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> ActiveKhoHangOptions { get; set; } = new List<SelectListItem>(); // Khởi tạo list trống


        // Phân trang
        public int PageIndex { get; set; }
        public int TotalPages { get; set; }

        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;
    }

    // ViewModel cho mỗi dòng trong bảng danh sách yêu cầu
    public class YeuCauListItemViewModel
    {
        public string M_YeuCau { get; set; } = "";
        public string? TenKhachHang { get; set; }
        public string? SdtKhachHang { get; set; }
        public string? DiaChiTomTat { get; set; }
        public string? TenLoaiSanPham { get; set; } // Lấy từ ChiTietThuGom đầu tiên?
        public int SoLuong { get; set; }           // Lấy từ ChiTietThuGom đầu tiên?
        public string? TenDonViTinh { get; set; }   // Lấy từ ChiTietThuGom đầu tiên?
        public DateTime NgayYeuCau { get; set; }
        public DateTime? NgayThuGom { get; set; } // Có thể là ThoiGianSanSang
        public string? TrangThai { get; set; }
        public string? TenNguoiThuGom { get; set; } // Lấy từ ApplicationUser (QuanLy)
        public AIKhoHangSuggestionViewModel? GoiYKhoHang { get; set; }
    }

    // ViewModel cho phần thống kê
    public class ThuGomStatisticsViewModel
    {
        public int YeuCauMoi { get; set; }      // Ví dụ: Trạng thái "Chờ xử lý"
        public int DaLenLich { get; set; }     // Ví dụ: Trạng thái "Đã lên lịch"
        public int DangThucHien { get; set; }   // Ví dụ: Trạng thái "Đang thu gom"
        public int HoanThanhTrongTuan { get; set; } // Hoàn thành trong 7 ngày qua
    }
}
