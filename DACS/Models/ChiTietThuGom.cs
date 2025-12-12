using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DACS.Models
{
    public class ChiTietThuGom // Collection Detail
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public String? M_ChiTiet { get; set; }

        [Required]
        [StringLength(10)]
        public string M_YeuCau { get; set; } // PFK
        [ForeignKey("M_YeuCau")]
        public virtual YeuCauThuGom YeuCauThuGom { get; set; }

        // --- CÁC TRƯỜNG NÊN BỎ ĐI ---
        // [StringLength(10)]
        // public string M_KhachHang { get; set; } // <<< BỎ: Đã có trong YeuCauThuGom
        // [ForeignKey("M_KhachHang")]
        // public virtual KhachHang KhachHang { get; set; } // <<< BỎ
        // ...
        // public string MaTinh { get; set; } // <<< BỎ
        // public string MaQuan { get; set; } // <<< BỎ
        // public string MaXa { get; set; } // <<< BỎ
        // public string? DiaChi_DuongApThon { get; set; } // <<< BỎ
        // ... (Bỏ luôn các navigation property TinhThanhPho, QuanHuyen, XaPhuong)
        // --- KẾT THÚC PHẦN BỎ ĐI ---

        [Required]
        [StringLength(10)]
        public string M_SanPham { get; set; } // <<< Đưa lên trên cho rõ ràng
        [ForeignKey("M_SanPham")]
        public virtual SanPham SanPham { get; set; } = null!;

        [Required]
        [StringLength(10)]
        public string M_LoaiSP { get; set; } // <<< Đưa lên trên cho rõ ràng
        [ForeignKey("M_LoaiSP")]
        public virtual LoaiSanPham LoaiSanPham { get; set; }

        [Required]
        public int SoLuong { get; set; } // Số lượng thu gom

        [Required]
        [StringLength(10)]
        public string M_DonViTinh { get; set; } // FK
        [ForeignKey("M_DonViTinh")]
        public virtual DonViTinh DonViTinh { get; set; }

        public decimal? GiaTriMongMuon { get; set; }
        public string? MoTa { get; set; }

        // --- Đặc tính sản phẩm ---
        [Display(Name = "Cồng Kềnh")]
        public bool DacTinh_CongKenh { get; set; } = false;
        [Display(Name = "Ẩm/Ướt (Dễ hỏng)")]
        public bool DacTinh_DoAmCao { get; set; } = false;
        [Display(Name = "Am Uot")]
        public bool DacTinh_AmUot { get; set; } = false;
        [Display(Name ="Kho")]
        public bool DacTinh_Kho { get; set; } = false;
        [Display(Name = "TapChat")]
        public bool DacTinh_TapChat { get; set; } = false;
        [Display(Name = "Đã Qua Xử Lý")]
        public bool DacTinh_DaXuLy { get; set; } = false;

        [Display(Name = "Hình Ảnh")]
        public string? DanhSachHinhAnh { get; set; }

        // --- Liên kết Lô Tồn Kho (RẤT QUAN TRỌNG) ---
        [StringLength(20)]
        [Display(Name = "Mã Lô Tồn Kho")]
        public string? MaLoTonKho { get; set; } // Khóa ngoại đến LoTonKho
        [ForeignKey("MaLoTonKho")]
        public virtual LoTonKho? LoTonKho { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Trạng thái xử lý")]
        public string TrangThaiXuLy { get; set; } = "MoiYeuCau";
        [Display(Name = "Độ ẩm thực tế (%)")]
        [Range(0, 100, ErrorMessage = "Độ ẩm phải nằm trong khoảng từ 0 đến 100.")]
        public double DoAmThucTe { get; set; }

        public double HeSoDoAm { get; set; }

        public double HeSoMuaVu { get; set; }

        public double PhiVanChuyen { get; set; }

        public double DonGiaThuMua { get; set; }
    }
}