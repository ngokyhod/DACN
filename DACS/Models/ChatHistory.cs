using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace DACS.Models
{
    [Table("ChatHistory")]
    public class ChatHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string CauHoi { get; set; }

        [StringLength(255)]
        public string CauTraLoi { get; set; }

        public string? M_KhachHang { get; set; }

        [ForeignKey("M_KhachHang")]
        public virtual KhachHang KhachHang { get; set; }

        [Required]
        [DataType(DataType.DateTime)]
        public DateTime? NgayChat { get; set; }
    }
}
