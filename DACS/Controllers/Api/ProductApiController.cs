using DACS.Models;
using DACS.Repositories;
using DACS.Repository; // Sử dụng Namespace Repository của bạn
using Microsoft.AspNetCore.Mvc;

namespace DACS.Controllers.Api
{
    [Route("api/[controller]")] // Đường dẫn sẽ là: domain/api/productapi
    [ApiController]
    public class ProductApiController : ControllerBase
    {
        private readonly ISanPhamRepository _sanPhamRepo;
        // Hoặc dùng ApplicationDbContext nếu Repository chưa đủ method
        private readonly ApplicationDbContext _context;

        public ProductApiController(ISanPhamRepository sanPhamRepo, ApplicationDbContext context)
        {
            _sanPhamRepo = sanPhamRepo;
            _context = context;
        }

        // 1. API Lấy tất cả sản phẩm
        // GET: api/productapi
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var products = _context.SanPhams.ToList();

            // Xử lý ảnh: Mobile cần đường dẫn tuyệt đối (VD: http://192.168.1.10:5000/images/sp1.jpg)
            // Bạn cần nối chuỗi domain vào đường dẫn ảnh ở đây nếu lưu đường dẫn tương đối.

            return Ok(products); // Trả về JSON
        }

        // 2. API Tìm kiếm (Dựa trên logic TimKiem cũ của bạn)
        // GET: api/productapi/search?keyword=abc
        [HttpGet("search")]
        public IActionResult Search(string keyword)
        {
            var query = _context.SanPhams.AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(sp => sp.TenSanPham.Contains(keyword));
            }

            var result = query.ToList();
            return Ok(result);
        }

        // 3. API Chi tiết sản phẩm
        // GET: api/productapi/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetDetail(int id)
        {
            var product = await _context.SanPhams.FindAsync(id);
            if (product == null) return NotFound();
            return Ok(product);
        }
    }
}