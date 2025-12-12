using System.ComponentModel.DataAnnotations;

namespace DACS.Models.AI
{
    public class PriceRequest
    {
        // Địa chỉ để tìm tọa độ
        public string Province { get; set; }
        public string District { get; set; }
        public string Ward { get; set; }
        public string Street { get; set; }

        // Thông tin sản phẩm
        public string NhomSanPham { get; set; } // Ví dụ: "Lua", "Ngo", "CaPhe"
        public double BaseValue { get; set; } // Giá cơ sở (VD: 500đ/kg)
        public double KhoiLuong { get; set; } // Kg
        public double DoAmThucTe { get; set; } // % (VD: 15, 20)
        public double DoAmChuan { get; set; } = 15; // % độ ẩm tiêu chuẩn
    }
}
