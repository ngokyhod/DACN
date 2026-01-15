namespace DACS.Controllers.Api
{
    public class RegisterRequestDto
    {
        public string FirebaseUid { get; set; } // ID từ Firebase Auth
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Password { get; set; }
        public string Role { get; set; } = "KhachHang";
    }

    // DTO hứng yêu cầu thu gom từ Flutter
    public class ThuGomRequestDto
    {
        public string UserId { get; set; } // Firebase UID hoặc ID của User
        public string HoTen { get; set; }
        public string SoDienThoai { get; set; }
        public DateTime? ThoiGianSanSang { get; set; } // Ngày giờ khách rảnh để thu gom

        // Lưu ý: Trường GhiChu này sẽ được dùng cho YeuCauThuGom (Master)
       
        public string DiaChiCuThe { get; set; }
        public string MaTinh { get; set; }
        public string MaQuan { get; set; }
        public string MaXa { get; set; }

        // Thông tin hàng
        public string DonViTinh { get; set; } = "KG"; // Cho phép mobile gửi lên, mặc định KG
        public List<string> HinhAnh { get; set; } // Danh sách link ảnh hoặc Base64
        public string TenSanPham { get; set; }
        public string LoaiSanPham { get; set; }
        public double KhoiLuong { get; set; }
        public decimal GiaMongMuon { get; set; }
        public double DoAm { get; set; }
        public string GhiChu { get; set; }
        public bool IsCongKenh { get; set; }
        public bool IsAmUot { get; set; }
        public bool IsTapChat { get; set; }
    }
    public class DonHangRequestDto
    {
        public string UserId { get; set; } // Firebase UID
        public string Tendathang { get; set; }
        public string SoDienThoaidathang { get; set; }
        public string ShippingAddress { get; set; }
        public string Notes { get; set; }
        public string M_PhuongThuc { get; set; } // Ví dụ: PT001
                                                 // public double TongTien { get; set; } // Không cần gửi, Server tự tính lại cho an toàn
        public List<ChiTietDonHangDto> ChiTietDonHangs { get; set; }
    }

    public class ChiTietDonHangDto
    {
        public string M_SanPham { get; set; }
        public string TenSanPham { get; set; }
        public float SoLuong { get; set; } // Mobile gửi double (do logic kg)
        public long DonGia { get; set; }
    }
    public class ProductUpdateDto
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public long Price { get; set; }
        public string Description { get; set; }
    }
    public class OrderStatusDto
    {
        public string MaDonHang { get; set; }
        public string TrangThai { get; set; }
        public string? DonViVanChuyen { get; set; }
        // Thêm trường này (nếu bạn muốn gửi tổng khối lượng thực tế khi giao)
        public double? KhoiLuongThucTe { get; set; }
    }
    public class ScrapStatusDto
    {
        public string RequestId { get; set; }   // M_YeuCau (Quan trọng: Sửa từ Id thành RequestId cho rõ)
        public string Status { get; set; }      // TrangThai (Của bảng YeuCauThuGom)
        public string? Date { get; set; }       // Ngày thực tế
        public int? ActualWeight { get; set; } // Khối lượng thực tế (dùng để cộng kho)
    }
}
