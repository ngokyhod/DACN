using DACS.Models;
using DACS.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DACS.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    // [Authorize(Roles = "Owner,Admin")] // Bật lại khi đã cấu hình JWT xong
    public class AdminApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AdminApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // 1. DASHBOARD (Lấy logic từ OwnerController.cs)
        // GET: api/AdminApi/dashboard
        // ============================================================
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek + 1); // Thứ 2 đầu tuần

            // 1. Doanh thu tháng này (Chỉ tính đơn hoàn thành)
            var monthlyRevenue = await _context.DonHangs
                .Where(dh => dh.NgayDat >= startOfMonth && dh.TrangThai == "Hoàn thành")
                .SumAsync(dh => dh.TotalPrice); // Lưu ý: Check lại tên field TongTien hay TotalPrice trong DB của bạn

            // 2. Đơn hàng mới trong tuần
            var newOrdersThisWeek = await _context.DonHangs
                .CountAsync(dh => dh.NgayDat >= startOfWeek);

            // 3. User mới trong tháng
            var newUsersThisMonth = await _context.Users
                .CountAsync(); // Logic đơn giản, có thể filter theo CreatedDate nếu User có field đó

            // 4. Yêu cầu thu gom đang chờ (Logic từ QuanLyXNK)
            var pendingCollection = await _context.YeuCauThuGoms
                .CountAsync(yc => yc.TrangThai == "Chờ duyệt" || yc.TrangThai == "Pending");

            // 5. Biểu đồ 7 ngày (Logic từ OwnerController)
            var chartLabels = new List<string>();
            var chartData = new List<decimal>();
            for (int i = 6; i >= 0; i--)
            {
                var date = today.AddDays(-i);
                chartLabels.Add(date.ToString("dd/MM"));

                var dailyRev = await _context.DonHangs
                    .Where(dh => dh.NgayDat.Date == date && dh.TrangThai == "Hoàn thành")
                    .SumAsync(dh => dh.TotalPrice);

                chartData.Add((decimal)dailyRev); // Đơn vị gốc (VND), App sẽ tự format hiển thị 'triệu' hay 'nghìn'
            }

            return Ok(new OwnerDashboardDto
            {
                MonthlyRevenue = (decimal)monthlyRevenue,
                NewOrdersThisWeek = newOrdersThisWeek,
                NewUsersThisMonth = newUsersThisMonth,
                PendingCollectionRequests = pendingCollection,
                ChartLabels = chartLabels,
                ChartData = chartData
            });
        }

        // ============================================================
        // 2. QUẢN LÝ ĐƠN HÀNG (Logic từ QuanLyDonHangController.cs)
        // GET: api/AdminApi/orders?status=Chờ xác nhận
        // ============================================================
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders(string status = "All", int page = 1)
        {
            int pageSize = 20;
            var query = _context.DonHangs.AsQueryable();

            if (status != "All")
            {
                query = query.Where(x => x.TrangThai == status);
            }

            var orders = await query
                .OrderByDescending(x => x.NgayDat)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AdminOrderDto
                {
                    MaDonHang = x.M_DonHang,
                    NgayDat = x.NgayDat,
                    TenNguoiNhan = x.Tendathang,
                    TongTien = (decimal)x.TotalPrice,
                    TrangThai = x.TrangThai,
                    TrangThaiThanhToan = x.TrangThaiThanhToan ?? "Chưa thanh toán",
                    SoLuongSanPham = x.ChiTietDatHangs.Count()
                })
                .ToListAsync();

            return Ok(orders);
        }

        // API Cập nhật trạng thái đơn (Duyệt đơn / Giao hàng)
        [HttpPost("orders/update-status")]
        public async Task<IActionResult> UpdateOrderStatus([FromBody] UpdateStatusDto model)
        {
            var order = await _context.DonHangs.FirstOrDefaultAsync(x => x.M_DonHang == model.MaDonHang);
            if (order == null) return NotFound("Không tìm thấy đơn hàng");

            // Logic giống QuanLyDonHangController
            order.TrangThai = model.TrangThaiMoi;

            // Ví dụ: Nếu chuyển sang "Đang giao hàng", có thể update thêm logic kho (tùy code gốc)

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật thành công", newStatus = order.TrangThai });
        }

        // ============================================================
        // 3. QUẢN LÝ THU GOM (Logic từ QuanLyXNKController.cs)
        // GET: api/AdminApi/collections
        // ============================================================
        [HttpGet("collections")]
        public async Task<IActionResult> GetCollections(string status = "Chờ duyệt")
        {
            var list = await _context.YeuCauThuGoms
                .Include(y => y.XaPhuong) // Include để lấy địa chỉ
                .Where(y => y.TrangThai == status)
                .OrderByDescending(y => y.NgayYeuCau)
                .Select(y => new CollectionRequestDto
                {
                    Id = y.M_YeuCau.ToString(), // Hoặc M_YeuCau nếu có
                    TenNguoiYeuCau = y.KhachHang.Ten_KhachHang,
                    SoDienThoai = y.KhachHang.SDT_KhachHang,
                    DiaChi = y.DiaChi_DuongApThon + ", " + (y.XaPhuong != null ? y.XaPhuong.TenXa : ""),
                    NgayYeuCau = y.NgayYeuCau,
                    TrangThai = y.TrangThai,
                    // Xử lý ảnh (nếu có logic ảnh trong model này)
                    AnhMinhHoa = ""
                })
                .ToListAsync();

            return Ok(list);
        }

        // ============================================================
        // 4. QUẢN LÝ SẢN PHẨM (Logic từ QuanLySPController.cs)
        // GET: api/AdminApi/products
        // ============================================================
        [HttpGet("products")]
        public async Task<IActionResult> GetProducts()
        {
            // Cần lấy URL gốc để ghép vào ảnh
            string baseUrl = $"{Request.Scheme}://{Request.Host}";

            var products = await _context.SanPhams
                .Include(p => p.ChiTietThuGoms)
                .Select(p => new AdminProductDto
                {
                    MaSanPham = p.M_SanPham,
                    TenSanPham = p.TenSanPham,
                    Gia = p.Gia,
                    // Tính tổng tồn kho từ các lô (Logic từ QuanLySPController)
                    TonKho = (double)p.ChiTietThuGoms.Sum(l => l.LoTonKho.KhoiLuongConLai),
                    HinhAnh = string.IsNullOrEmpty(p.AnhSanPham) ? "" : baseUrl + "/images/products/" + p.AnhSanPham
                })
                .ToListAsync();

            return Ok(products);
        }
    }
}