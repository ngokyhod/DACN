// File: Repositories/TonKhoRepository.cs
using DACS.Models;
using DACS.Models.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DACS.Repository
{
    public class TonKhoRepository : ITonKhoRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TonKhoRepository> _logger; // Thêm Logger nếu cần

        public TonKhoRepository(ApplicationDbContext context, ILogger<TonKhoRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<(IEnumerable<LoTonKho> Items, int TotalCount)> GetPagedTonKhoAsync(
     string? searchTerm, string? maKhoFilter, int pageIndex, int pageSize, bool trackChanges = false)
        {
            var query = _context.LoTonKhos.AsQueryable();

            if (!trackChanges)
            {
                query = query.AsNoTracking();
            }

            // <<< SỬA: Thay đổi cách Include
            // Bạn cần Include SanPham, và SAU ĐÓ ThenInclude LoaiSanPham từ SanPham
            query = query.Include(tk => tk.KhoHang)       // (Giả định bạn đã thêm 'KhoHang' vào model LoTonKho)
                         .Include(tk => tk.DonViTinh)
                         .Include(tk => tk.SanPham)       // <<< THÊM: Include SanPham
                            .ThenInclude(sp => sp.LoaiSanPham); // <<< THÊM: Include LoaiSanPham TỪ SanPham

            // --- Áp dụng Bộ lọc ---
            if (!string.IsNullOrEmpty(searchTerm))
            {
                string lowerSearchTerm = searchTerm.ToLower().Trim();

                // <<< SỬA: Thay đổi cách lọc
                // Phải lọc qua tk.SanPham.LoaiSanPham.TenLoai
                query = query.Where(tk =>
                    // Tìm theo Mã Lô
                    (tk.MaLoTonKho != null && tk.MaLoTonKho.ToLower().Contains(lowerSearchTerm)) ||
                    // Tìm theo Tên Sản Phẩm
                    (tk.SanPham != null && tk.SanPham.TenSanPham != null && tk.SanPham.TenSanPham.ToLower().Contains(lowerSearchTerm)) ||
                    // Tìm theo Tên Loại Sản Phẩm
                    (tk.SanPham != null && tk.SanPham.LoaiSanPham != null && tk.SanPham.LoaiSanPham.TenLoai != null && tk.SanPham.LoaiSanPham.TenLoai.ToLower().Contains(lowerSearchTerm))
                );
            }

            // (Phần lọc theo kho này có vẻ đúng, tôi giữ nguyên)
            if (!string.IsNullOrEmpty(maKhoFilter) && maKhoFilter.ToLower() != "all")
            {
                query = query.Where(tk => tk.MaKho == maKhoFilter);
            }

            // --- Lấy Tổng số bản ghi (sau khi lọc) ---
            var totalItems = await query.CountAsync();

            // --- Sắp xếp (Ví dụ: Theo Tên Loại SP, rồi Tên SP) ---
            // <<< SỬA: Thay đổi cách sắp xếp
            query = query.OrderBy(tk => (tk.SanPham != null && tk.SanPham.LoaiSanPham != null) ? tk.SanPham.LoaiSanPham.TenLoai : null)
                         .ThenBy(tk => (tk.SanPham != null) ? tk.SanPham.TenSanPham : tk.M_SanPham)
                         .ThenBy(tk => tk.MaLoTonKho);

            // --- Phân trang ---
            var pagedData = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (pagedData, totalItems);
        }

        public async Task<IEnumerable<SelectListItem>> GetKhoHangOptionsAsync()
        {
            var options = await _context.KhoHangs
                .AsNoTracking()
                .Where(kh => kh.TrangThai != KhoHangTrangThai.NgungSuDung)
                .OrderBy(kh => kh.TenKho)
                .Select(kh => new SelectListItem
                {
                    Value = kh.MaKho,
                    Text = $"{kh.TenKho} ({kh.MaKho})"
                })
                .ToListAsync();

            // Thêm tùy chọn "Tất cả kho" vào đầu danh sách
            options.Insert(0, new SelectListItem { Value = "all", Text = "--- Tất cả kho ---" });

            return options;
        }

        public async Task UpdateTonKhoKhoiLuongAsync(string productId, float newKhoiLuong)
        {
            // 1. Tìm đối tượng TonKho trong database dựa trên productId
            // Sử dụng FirstOrDefaultAsync để tránh lỗi nếu không tìm thấy
            var tonKho = await _context.LoTonKhos
                                       .FirstOrDefaultAsync(tk => tk.M_SanPham == productId);

            // 2. Kiểm tra xem đối tượng có tồn tại không
            if (tonKho == null)
            {
                // Nếu không tìm thấy, ném một ngoại lệ để thông báo lỗi
                // Controller hoặc lớp gọi sẽ bắt ngoại lệ này để xử lý
                throw new InvalidOperationException($"Không tìm thấy tồn kho cho sản phẩm với mã: {productId}");
            }

            // 3. Cập nhật giá trị KhoiLuong mới
            tonKho.KhoiLuongConLai = (decimal)newKhoiLuong;

            // 4. Lưu các thay đổi vào database
            // SaveChangesAsync sẽ phát hiện rằng đối tượng 'tonKho' đã được modified
            // (vì nó đã được theo dõi bởi DbContext sau khi query) và sẽ tạo câu lệnh UPDATE.
            await _context.SaveChangesAsync();
        }

        // Phương thức mới: Lấy tồn kho theo ProductId (Nếu bạn chưa có)
        public async Task<LoTonKho?> GetTonKhoByProductIdAsync(string productId)
        {
            return await _context.LoTonKhos
                                 .FirstOrDefaultAsync(tk => tk.M_SanPham == productId);
        }
      
    }
}