using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DACS.Models
{
    public class AIMessages
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string SessionId { get; set; } // Mã định danh cuộc hội thoại (UUID)

        public string? UserId { get; set; } // Lưu ID người dùng (nếu đã đăng nhập)

        [Required]
        public string Content { get; set; } // Nội dung tin nhắn

        public bool IsAi { get; set; } // true: AI nói, false: Người nói

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}