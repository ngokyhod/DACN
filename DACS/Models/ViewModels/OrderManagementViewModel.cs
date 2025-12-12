// File: Areas/Owner/Models/OrderViewModel.cs (hoặc gộp vào OrderManagementViewModel.cs)
using System;
using System.Collections.Generic;
using DACS.Models.Blockchain;
using Microsoft.AspNetCore.Mvc.Rendering; // Cần cho SelectListItem

namespace DACS.Areas.Owner.Models
{
    public class OrderViewModel // Dùng cho mỗi dòng trong bảng đơn hàng
    {
        public string M_DonHang { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhoneNumber { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; } // Controller sẽ ép kiểu từ DonHang.TotalPrice (float)

        // Cho cột "Trạng Thái TT"
        public string PaymentStatusText { get; set; }
        public string PaymentStatusBadgeClass { get; set; }

        // Cho cột "Trạng Thái ĐH"
        public string OrderStatusText { get; set; } // Sẽ là DonHang.TrangThai
        public string OrderStatusBadgeClass { get; set; } // Tính toán bởi helper của controller
        public string PaymentMethod { get; set; }
    }

    public class OrderManagementViewModel // Model cho toàn bộ trang
    {
        // Statistics cho các thẻ card
        public int PendingConfirmationOrders { get; set; }
        public int ProcessingOrShippingOrders { get; set; }
        public int CompletedOrdersThisMonth { get; set; }
        public int CancelledOrdersThisMonth { get; set; }

        // Giá trị filter hiện tại (để điền lại vào form)
        public string SearchInput { get; set; } // khớp với id="search-input"
        public DateTime? DateFromFilter { get; set; } // khớp với id="date-from-filter"
        public DateTime? DateToFilter { get; set; } // khớp với id="date-to-filter"
        public string SelectedOrderStatus { get; set; } // Giá trị từ <select name="statusFilterSelect">
        public string SelectedPaymentStatus { get; set; } // Giá trị từ <select name="paymentStatusFilter">

        // Options cho các dropdown filter
        public List<SelectListItem> OrderStatusOptions { get; set; }
        public List<SelectListItem> PaymentStatusOptions { get; set; } // Cho dropdown Trạng Thái TT

        // Dữ liệu bảng & Phân trang
        public List<OrderViewModel> Orders { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }

        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages && TotalPages > 0;

        public OrderManagementViewModel()
        {
            Orders = new List<OrderViewModel>();
            OrderStatusOptions = new List<SelectListItem>();
            PaymentStatusOptions = new List<SelectListItem>();

        }
    }
}