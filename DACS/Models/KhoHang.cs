using System.ComponentModel.DataAnnotations;
using System.Collections.Generic; // Cần cho ICollection nếu có quan hệ

namespace DACS.Models // Hoặc namespace phù hợp
{
    public class KhoHang
    {
        [Key] // Khóa chính
        [StringLength(10, ErrorMessage = "Mã kho không được vượt quá 10 ký tự.")]
        [Required(ErrorMessage = "Vui lòng nhập mã kho.")]
        [Display(Name = "Mã Kho")]
        public string MaKho { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên kho.")]
        [StringLength(100, ErrorMessage = "Tên kho không được vượt quá 100 ký tự.")]
        [Display(Name = "Tên Kho")]
        public string TenKho { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ.")]
        [StringLength(250, ErrorMessage = "Địa chỉ không được vượt quá 250 ký tự.")]
        [Display(Name = "Địa chỉ")]
        public string DiaChi { get; set; } // Giữ đơn giản hoặc tách chi tiết nếu cần

        [StringLength(50)]
        [Display(Name = "Sức chứa")]
        public string? SucChuaTomTat { get; set; } // Ví dụ: "~10 tấn", "~50 m³"

        [Required(ErrorMessage = "Vui lòng chọn trạng thái.")]
        [StringLength(50)]
        [Display(Name = "Trạng thái")]
        public string TrangThai { get; set; } // Nên dùng các giá trị từ KhoHangTrangThai
        [Required]
        [StringLength(100)]
        public string TenLoaiKho { get; set; }
        [Range(-90, 90, ErrorMessage = "Vĩ độ (Lat) không hợp lệ.")]
        public double Lat { get; set; }

        [Range(-180, 180, ErrorMessage = "Kinh độ (Lng) không hợp lệ.")]
        public double Lng { get; set; }
    }

    // --- Định nghĩa các hằng số trạng thái kho ---
    // (Bạn có thể đã đặt cái này ở file khác, đảm bảo nó tồn tại)
    public static class KhoHangTrangThai
    {
        public const string ConTrong = "Còn trống";
        public const string GanDay = "Gần đầy";
        public const string BaoTri = "Bảo trì";
        // Thêm các trạng thái khác nếu có
    }
}