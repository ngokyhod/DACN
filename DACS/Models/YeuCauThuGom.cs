using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DACS.Models
{
    public class YeuCauThuGom // Collection Request
    {
        [Key]
        [StringLength(10)]
        public string M_YeuCau { get; set; }

        [Required]
        public DateTime NgayYeuCau { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(50)]
        public string TrangThai { get; set; }

        [Required]
        [StringLength(10)]
        public string M_KhachHang { get; set; }
        [ForeignKey("M_KhachHang")]
        public virtual KhachHang KhachHang { get; set; }

        // --- CÁC TRƯỜNG ĐỊA CHỈ NÊN Ở ĐÂY ---
        [Required(ErrorMessage = "Vui lòng chọn Tỉnh/Thành phố.")]
        [StringLength(10)]
        [Display(Name = "Tỉnh/Thành phố")]
        public string MaTinh { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn Quận/Huyện.")]
        [StringLength(10)]
        [Display(Name = "Quận/Huyện")]
        public string MaQuan { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn Xã/Phường.")]
        [StringLength(10)]
        [Display(Name = "Xã/Phường")]
        public string MaXa { get; set; }

        [StringLength(200)]
        [Display(Name = "Số nhà, Đường/Ấp/Thôn")]
        public string? DiaChi_DuongApThon { get; set; }

        [Range(-90, 90, ErrorMessage = "Vĩ độ (Lat) không hợp lệ.")]
        public double? Lat { get; set; }

        [Range(-180, 180, ErrorMessage = "Kinh độ (Lng) không hợp lệ.")]
        public double? Lng { get; set; }

        [ForeignKey("MaTinh")]
        public virtual TinhThanhPho? TinhThanhPho { get; set; }
        [ForeignKey("MaQuan")]
        public virtual QuanHuyen? QuanHuyen { get; set; }
        [ForeignKey("MaXa")]
        public virtual XaPhuong? XaPhuong { get; set; }
        // --- KẾT THÚC PHẦN ĐỊA CHỈ ---

        [Required] // <<< ĐẢM BẢO THÊM [Required] NẾU CẦN
        public DateTime ThoiGianSanSang { get; set; }
        public string? GhiChu { get; set; }

        [Column("Thoi_Gian_HT")]
        public DateTime ThoiGianHoanThanh { get; set; }

        public string? M_QuanLy { get; set; }
        [ForeignKey("M_QuanLy")]
        public virtual ApplicationUser? QuanLy { get; set; }

        [Display(Name = "Mã Giao Dịch Blockchain")]
        [StringLength(100)]
        public string? BlockchainTransactionHash { get; set; }

        // Navigation Property
        public virtual ICollection<ChiTietThuGom> ChiTietThuGoms { get; set; } = new List<ChiTietThuGom>();
    }
}