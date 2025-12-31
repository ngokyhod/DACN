using Microsoft.AspNetCore.Mvc.Rendering; // Cần thêm using này cho SelectListItem

using System.ComponentModel.DataAnnotations;

using System.ComponentModel.DataAnnotations.Schema; // Cho [NotMapped] (tùy chọn nhưng nên có)



namespace DACS.Models.ViewModels // <<< Thay namespace cho đúng với project của bạn

{

    public class ThuGomViewModel

    {

        public string? M_YeuCau { get; set; }



        // --- Các thuộc tính hiện có để lưu giá trị người dùng nhập ---

        [Display(Name = "Tên người cung cấp")]

        [Required(ErrorMessage = "Vui lòng nhập tên người cung cấp.")]

        public string SupplierName { get; set; }



        [Display(Name = "Số điện thoại")]

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]

        public string SupplierPhone { get; set; }



        [Display(Name = "Tỉnh/Thành phố")]

        [Required(ErrorMessage = "Vui lòng chọn Tỉnh/Thành phố.")]

        public string SupplierProvince { get; set; } // Lưu Mã Tỉnh được chọn



        [Display(Name = "Quận/Huyện")]

        [Required(ErrorMessage = "Vui lòng chọn Quận/Huyện.")]

        public string SupplierDistrict { get; set; } // Lưu Mã Quận được chọn



        [Display(Name = "Xã/Phường")]

        [Required(ErrorMessage = "Vui lòng chọn Xã/Phường.")]

        public string SupplierWard { get; set; }     // Lưu Mã Xã được chọn



        [Display(Name = "Địa chỉ chi tiết (Số nhà, đường, thôn...)")]

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ chi tiết.")]

        public string SupplierStreet { get; set; }



        [Display(Name = "Thời gian sẵn sàng lấy")]

        [Required(ErrorMessage = "Vui lòng chọn thời gian.")]

        public DateTime? PickupReadyTime { get; set; } // Dùng nullable DateTime



        [Display(Name = "Ghi chú thêm")]

        public string? SupplierNotes { get; set; } // Dùng nullable string



        [Display(Name = "Loại sản phẩm")]
        [Required(ErrorMessage = "Vui lòng chọn loại sản phẩm.")]
        public string M_LoaiSP { get; set; }

        [Display(Name = "Mô tả chi tiết phụ phẩm")]

        public string? ByproductDescription { get; set; }



        [Display(Name = "Số lượng ước tính")]

        [Required(ErrorMessage = "Vui lòng nhập số lượng.")]

        [Range(0.01, double.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0.")]

        public double? ByproductQuantity { get; set; } // Dùng nullable double/decimal



        [Display(Name = "Đơn vị tính")]

        [Required(ErrorMessage = "Vui lòng chọn đơn vị tính.")]

        public string ByproductUnit { get; set; } // Lưu Mã ĐVT được chọn



        [Display(Name = "Giá trị mong muốn (ước tính)")]

        [Range(0, double.MaxValue, ErrorMessage = "Giá trị phải lớn hơn hoặc bằng 0.")]

        public decimal? ByproductValue { get; set; } // Dùng nullable decimal



        [Display(Name = "Cồng kềnh / Khó vận chuyển")]

        public bool CharBulky { get; set; }

        public bool CharWet { get; set; }

        public bool CharDry { get; set; }

        public bool CharMoisture { get; set; }

        public bool CharImpure { get; set; }

        public bool CharProcessed { get; set; }

        [Display(Name = "Sản phẩm cụ thể")]
        [Required(ErrorMessage = "Vui lòng chọn sản phẩm.")]
        public string M_SanPham { get; set; } // <<< THÊM MỚI

        [Display(Name = "Ảnh chụp phụ phẩm")]

        public List<IFormFile>? ByproductImages { get; set; } // Danh sách file ản

        // --- THÊM CÁC THUỘC TÍNH ĐỂ CHỨA DANH SÁCH DROPDOWN ---

        [NotMapped] // Không ánh xạ vào DB

        public IEnumerable<SelectListItem>? LoaiSanPhamOptions { get; set; }

        [NotMapped]
        public IEnumerable<SelectListItem>? SanPhamOptions { get; set; } // <<< THÊM MỚI

        [NotMapped]

        public IEnumerable<SelectListItem>? DonViTinhOptions { get; set; }



        [NotMapped]

        public IEnumerable<SelectListItem>? ProvinceOptions { get; set; }



        [NotMapped]

        public IEnumerable<SelectListItem>? DistrictOptions { get; set; }



        [NotMapped]

        public IEnumerable<SelectListItem>? WardOptions { get; set; }
        [Display(Name = "Tôi đã đọc và đồng ý với các Điều Khoản Cam Kết Cung Cấp Phụ Phẩm.")]
        [Compare(nameof(DieuKienPhaiBangTrue), ErrorMessage = "Bạn phải đồng ý với cam kết để gửi yêu cầu.")]
        public bool DaDongYCamKet { get; set; }
        public double? DoAmThucTe { get; set; } // Map với input độ ẩm
        public double? HeSoMuaVu { get; set; }
        public double? HeSoDoAm { get; set; }
        public double? PhiVanChuyen { get; set; }
        public double? DonGiaThuMua { get; set; }

        // 2. Thêm thuộc tính "giả" này
        // Thuộc tính này luôn trả về 'true' để so sánh
        [NotMapped]
        public bool DieuKienPhaiBangTrue
        {
            get { return true; }
        }
    }

}