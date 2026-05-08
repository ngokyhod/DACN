using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using DACS.Models;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

public class ChatHub : Hub
{
    private readonly ApplicationDbContext _context;
    private static ConcurrentDictionary<string, string> OnlineUsers = new();

    public ChatHub(ApplicationDbContext context)
    {
        _context = context;
    }

    public override async Task OnConnectedAsync()
    {
        string userId = Context.User?.Identity?.Name ?? Context.ConnectionId;

        // Lưu connection
        OnlineUsers[userId] = Context.ConnectionId;

        // Nếu là admin
        if (Context.User?.IsInRole("Admin") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var kv = OnlineUsers.FirstOrDefault(x => x.Value == Context.ConnectionId);
        if (!string.IsNullOrEmpty(kv.Key))
        {
            OnlineUsers.TryRemove(kv.Key, out _);
        }

        await base.OnDisconnectedAsync(exception);
    }

    [Authorize(Roles = "KhachHang")]
    public async Task SendMessageToAdmin(string senderName, string message = "", string? imageUrl = null)
    {
        string senderId = Context.User?.Identity?.Name ?? Context.ConnectionId;

        if (string.IsNullOrWhiteSpace(message) && string.IsNullOrWhiteSpace(imageUrl))
            return;

        var chat = new ChatMessage
        {
            SenderId = senderId,
            ReceiverId = "Admin",
            SenderName = senderName,
            Message = string.IsNullOrWhiteSpace(message) ? null : message,
            ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl,
            IsFromAdmin = false,
            SentTime = DateTime.Now,

        };

        _context.ChatMessages.Add(chat);
        await _context.SaveChangesAsync();

        // Gửi realtime tới tất cả admin đang online
        await Clients.Group("Admins").SendAsync("ReceiveUserMessage", new
        {
            senderId,
            senderName,
            message = chat.Message,
            imageUrl = chat.ImageUrl,
            sentTime = chat.SentTime.ToString("HH:mm")
        });

        // Gửi lại cho chính user để hiển thị ngay (tránh delay)
        await Clients.Caller.SendAsync("ReceiveUserMessage", new
        {
            senderId,
            senderName,
            message = chat.Message,
            imageUrl = chat.ImageUrl,
            sentTime = chat.SentTime.ToString("HH:mm")
        });
    }

    // ✅ ADMIN gửi tin nhắn (hoặc ảnh) cho KHÁCH
    public async Task SendReplyToUser(string receiverId, string message = "", string? imageUrl = null)
    {
        if (string.IsNullOrWhiteSpace(message) && string.IsNullOrWhiteSpace(imageUrl))
            return;

        var chat = new ChatMessage
        {
            SenderId = "Admin",
            ReceiverId = receiverId,
            SenderName = "Admin",
            Message = string.IsNullOrWhiteSpace(message) ? null : message,
            ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl,
            IsFromAdmin = true,
            M_KhachHang = receiverId,
            SentTime = DateTime.Now
        };

        _context.ChatMessages.Add(chat);
        await _context.SaveChangesAsync();

        // Gửi cho khách nếu đang online
        if (OnlineUsers.TryGetValue(receiverId, out var connId))
        {
            await Clients.Client(connId).SendAsync("ReceiveAdminReply", new
            {
                message = chat.Message,
                imageUrl = chat.ImageUrl,
                sentTime = chat.SentTime.ToString("HH:mm")
            });
        }

        // Gửi lại cho admin để hiển thị ngay
        await Clients.Caller.SendAsync("AdminSentMessage", new
        {
            receiverId,
            message = chat.Message,
            imageUrl = chat.ImageUrl,
            sentTime = chat.SentTime.ToString("HH:mm")
        });
    }
    public async Task SendMessageFromMobile(string firebaseUid, string senderName, string message = "", string? imageUrl = null)
    {
        if (string.IsNullOrWhiteSpace(message) && string.IsNullOrWhiteSpace(imageUrl))
            return;

        // 1. Lưu tin nhắn vào Database (Dùng chính cái UID đã đồng bộ)
        var chat = new ChatMessage
        {
            SenderId = firebaseUid,
            ReceiverId = "Admin",
            SenderName = senderName,
            Message = string.IsNullOrWhiteSpace(message) ? null : message,
            ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl,
            IsFromAdmin = false,
            SentTime = DateTime.Now
        };

        _context.ChatMessages.Add(chat);
        await _context.SaveChangesAsync();

        // 2. Báo cho các Admin đang mở Website biết có tin nhắn mới
        await Clients.Group("Admins").SendAsync("ReceiveUserMessage", new
        {
            senderId = firebaseUid,
            senderName,
            message = chat.Message,
            imageUrl = chat.ImageUrl,
            sentTime = chat.SentTime.ToString("HH:mm")
        });

        // 3. Trả tín hiệu về lại cho Mobile để hiển thị tin nhắn lên màn hình
        await Clients.Caller.SendAsync("ReceiveUserMessage", new
        {
            senderId = firebaseUid,
            senderName,
            message = chat.Message,
            imageUrl = chat.ImageUrl,
            sentTime = chat.SentTime.ToString("HH:mm")
        });
    }

}
