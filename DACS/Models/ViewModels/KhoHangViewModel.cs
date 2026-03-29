using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations; // Cần cho DisplayName

namespace DACS.Models.ViewModels // Hoặc namespace phù hợp
{
    // ViewModel chính cho trang Index Kho Hàng
    public class KhoHangViewModel
    {
        public List<KhoHangListItemViewModel> DanhSachKhoHang { get; set; } = new List<KhoHangListItemViewModel>();
        public KhoHangStatsViewModel? Statistics { get; set; }

        // --- Dữ liệu cho Bộ lọc ---
        [Display(Name = "Tìm kiếm")]
        public string? SearchTerm { get; set; } // Liên kết với ô tìm kiếm

        [Display(Name = "Trạng thái")]
        public string? StatusFilter { get; set; } // Liên kết với dropdown trạng thái

        // Danh sách các tùy chọn cho dropdown trạng thái
        public List<SelectListItem> StatusOptions { get; set; } = new List<SelectListItem>();

        // --- Dữ liệu cho Phân trang ---
        public int PageIndex { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;
    }

    // ViewModel cho mỗi dòng trong bảng danh sách kho
    public class KhoHangListItemViewModel
    {
        [Display(Name = "Mã Kho")]
        public string MaKho { get; set; } = "";
        [Display(Name = "Tên Kho")]
        public string TenKho { get; set; } = "";
        [Display(Name = "Địa chỉ")]
        public string DiaChi { get; set; } = "";
        [Display(Name = "Sức chứa")]
        public string? SucChuaTomTat { get; set; }
        [Display(Name = "Trạng thái")]
        public string TrangThai { get; set; } = "";
    }

    // ViewModel cho phần thống kê
    public class KhoHangStatsViewModel
    {
        public int TongKho { get; set; }
        public int ConTrong { get; set; }
        public int GanDay { get; set; }
        public int BaoTri { get; set; }
        public int NgungSuDung { get; set; }
    }
}