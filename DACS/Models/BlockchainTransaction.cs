using System;
using System.ComponentModel.DataAnnotations;

namespace DACS.Models
{
    public class BlockchainTransaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string MaDonHang { get; set; }

        [MaxLength(200)]
        public string TrangThai { get; set; }

        [MaxLength(500)]
        public string DiaChiGiaoHang { get; set; }

        [MaxLength(500)]
        public string Metadata { get; set; }

        [Required]
        [MaxLength(100)]
        public string TxHash { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
