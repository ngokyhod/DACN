using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace DACS.Models
{
    public class SanPham // Product / Byproduct
    {
        [Key]
        [StringLength(10)]
        public string M_SanPham { get; set; }

        [Required]
        [StringLength(10)]
        public string M_LoaiSP { get; set; } // FK

        [Required]
        [StringLength(10)]
        public string M_DonViTinh { get; set; } // FK

        [Required(ErrorMessage = "Tên sản phẩm là bắt buộc.")]
        [StringLength(100)]
        public string TenSanPham { get; set; }

        [Required]
        // Nên dùng Decimal, cấu hình precision trong DbContext
        public long Gia { get; set; } // Bigint

        [Required]
        [Column(TypeName = "nvarchar(MAX)")] // Thay Ntext
        public string MoTa { get; set; }

        [Required]
        [StringLength(100)]
        public string TrangThai { get; set; }

        [Required]
        public DateTime NgayTao { get; set; } = DateTime.UtcNow;

        [StringLength(500)] // Tăng độ dài cho ảnh
        public string? AnhSanPham { get; set; }

        [Required]
        public DateTime HanSuDung { get; set; }

        // Navigation Properties
        [ForeignKey("M_LoaiSP")]
        public virtual LoaiSanPham? LoaiSanPham { get; set; }

        [ForeignKey("M_DonViTinh")]
        public virtual DonViTinh? DonViTinh { get; set; }

        public int ThangBatDauVu { get; set; } // Ví dụ: 5 (Tháng 5)
        public int ThangKetThucVu { get; set; } // Ví dụ: 10 (Tháng 10)

        // Hệ số giá (1.0 là giữ nguyên, 0.9 là giảm 10%, 1.2 là tăng 20%)
        public double HeSoGiaTrongMua { get; set; } = 0.9; // Vào mùa rẻ hơn
        public double HeSoGiaTraiMua { get; set; } = 1.15; // Trái mùa đắt hơn
        public virtual ICollection<ChiTietDatHang> ChiTietDatHangs { get; set; } = new List<ChiTietDatHang>();
        public virtual ICollection<ChiTietDanhGia> ChiTietDanhGias { get; set; } = new List<ChiTietDanhGia>();
        public virtual ICollection<ChiTietThuGom> ChiTietThuGoms { get; set; } = new List<ChiTietThuGom>();


    }
}
