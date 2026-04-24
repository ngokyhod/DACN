using DACS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DACS.Controllers
{
    public class HistoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HistoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. API Lưu tin nhắn (Đã sửa thành AIMessages)
        [HttpPost]
        public async Task<IActionResult> SaveMessage([FromBody] AIMessageDTO req)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Lưu vào bảng AIMessages
                var msg = new AIMessages
                {
                    SessionId = req.SessionId,
                    Content = req.Content,
                    IsAi = req.IsAi,
                    UserId = userId,
                    CreatedAt = DateTime.Now
                };

                _context.AIMessages.Add(msg);
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // 2. API "Sổ ra" danh sách Session (Thay thế cho @foreach)
        [HttpGet]
        public async Task<IActionResult> GetSessions()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Ok(new List<object>());

            // Logic: Lấy các tin nhắn của user -> Gom nhóm theo SessionId -> Chọn tin mới nhất
            var sessions = await _context.AIMessages
                .Where(x => x.UserId == userId)
                .GroupBy(x => x.SessionId)
                .Select(g => new
                {
                    SessionId = g.Key,
                    // Lấy thời gian chat cuối cùng để sắp xếp
                    LastMessageTime = g.Max(x => x.CreatedAt),
                    // Lấy câu chat đầu tiên để làm Tiêu đề hiển thị cho đẹp
                    Preview = g.OrderBy(x => x.CreatedAt).FirstOrDefault().Content
                })
                .OrderByDescending(x => x.LastMessageTime) // Mới nhất lên đầu
                .Take(15) // Lấy 15 cuộc hội thoại gần nhất
                .ToListAsync();

            return Ok(sessions);
        }

        // 3. API Lấy nội dung chi tiết khi click vào
        [HttpGet]
        public async Task<IActionResult> GetConversation(string sessionId)
        {
            var messages = await _context.AIMessages
                .Where(x => x.SessionId == sessionId)
                .OrderBy(x => x.CreatedAt)
                .Select(x => new { x.Content, x.IsAi })
                .ToListAsync();

            return Ok(messages);
        }
    }

    // Class hứng dữ liệu
    public class AIMessageDTO
    {
        public string SessionId { get; set; }
        public string Content { get; set; }
        public bool IsAi { get; set; }
    }
}