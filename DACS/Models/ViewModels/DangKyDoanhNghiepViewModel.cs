using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http; // Thêm thư viện IFormFile

namespace DACS.Models.ViewModels
{
    public class DangKyDoanhNghiepViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên doanh nghiệp")]       
        [StringLength(255)]
        [Display(Name = "Tên Doanh Nghiệp")]
        public string TenDoanhNghiep { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập lĩnh vực hoạt động")]
        [StringLength(255)]
        [Display(Name = "Lĩnh vực hoạt động")]
        public string LinhVucHoatDong { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nhu cầu thiết yếu")]   
        [Display(Name = "Nhu cầu mua hàng / Thu gom")]
        public string NhuCauChinh { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn tỉnh/thành phố doanh nghiệp")]
        [StringLength(10)]
        [Display(Name = "Tỉnh/Thành phố")]
        public string MaTinhDoanhNghiep { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn quận/huyện doanh nghiệp")]
        [StringLength(10)]
        [Display(Name = "Quận/Huyện")]
        public string MaQuanDoanhNghiep { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn xã/phường doanh nghiệp")]
        [StringLength(10)]
        [Display(Name = "Xã/Phường")]
        public string MaXaDoanhNghiep { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số nhà, đường hoặc ấp/thôn")]
        [StringLength(200)]
        [Display(Name = "Số nhà, Đường/Ấp/Thôn")]
        public string DiaChiDuongDoanhNghiep { get; set; }

        [StringLength(255)]
        [Display(Name = "Địa chỉ doanh nghiệp")]
        public string? DiaChiDoanhNghiep { get; set; }

        [Range(-90, 90, ErrorMessage = "Vĩ độ doanh nghiệp không hợp lệ.")]
        public double? EnterpriseLat { get; set; }

        [Range(-180, 180, ErrorMessage = "Kinh độ doanh nghiệp không hợp lệ.")]
        public double? EnterpriseLng { get; set; }

        [Display(Name = "Hình ảnh Giấy Phép Kinh Doanh")]
        public IFormFile? GiayPhepKinhDoanh { get; set; }

        public string? GiayPhepKinhDoanhUrl { get; set; }
    }
}