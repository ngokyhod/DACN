using DACS.Models;
using DACS.Models.ViewModels;
using DACS.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DACS.Areas.QuanLyXNK.Controllers
{
    [Area("QuanLyXNK")] // Chỉ định Area
    [Authorize(Roles = "Owner,QuanLyXNK")] // Ví dụ phân quyền: Chỉ Admin hoặc người có role QuanLyXNK mới vào được

    public class QuanLyDSPXController : Controller
    {
        private readonly IPhieuXuatRepository _phieuXuatRepo;
        private readonly ILogger<QuanLyDSPXController> _logger; // Inject logger
        private readonly ApplicationDbContext _context;

        public QuanLyDSPXController(ApplicationDbContext context, IPhieuXuatRepository phieuXuatRepo, ILogger<QuanLyDSPXController> logger)
        {
            _context = context;
            _phieuXuatRepo = phieuXuatRepo;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? searchTerm, string? statusFilter, DateTime? dateFilter, int page = 1)
        {
            _logger.LogInformation("PhieuXuat Index: Search='{SearchTerm}', Status='{StatusFilter}', Date='{DateFilter}', Page={Page}",
                searchTerm, statusFilter, dateFilter, page);

            int pageSize = 10; // Số item trên mỗi trang

            var query = _context.PhieuXuats
                                .Include(px => px.KhoHang) // Để lấy tên kho
                                .Include(px => px.ChiTietPhieuXuats) // Để tính tổng số lượng/giá trị
                                .AsQueryable();

            // Lọc dữ liệu
            if (!string.IsNullOrEmpty(searchTerm))
            {
                string lowerSearchTerm = searchTerm.ToLower().Trim();
                query = query.Where(px => (px.NguoiNhan != null && px.NguoiNhan.ToLower().Contains(lowerSearchTerm)) ||
                                          px.MaPhieuXuat.ToString().Contains(lowerSearchTerm) // Tìm theo mã phiếu nếu mã là số
                                                                                              // Thêm các trường tìm kiếm khác nếu cần
                                          );
            }

            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "all")
            {
                query = query.Where(px => px.TrangThai == statusFilter);
            }

            if (dateFilter.HasValue)
            {
                query = query.Where(px => px.NgayXuat.Date == dateFilter.Value.Date);
            }

            // Sắp xếp (ví dụ: mới nhất lên đầu)
            query = query.OrderByDescending(px => px.NgayXuat);

            // Tính toán số liệu cho Stat Cards (tính trên toàn bộ dữ liệu hoặc query đã lọc tùy yêu cầu)
            // Ví dụ tính trên toàn bộ dữ liệu hiện có trong DB:
            var allPhieuXuatsForStats = _context.PhieuXuats.AsNoTracking();
            var statsModel = new PhieuXuatIndexViewModel
            {
                TongPhieuXuat = await allPhieuXuatsForStats.CountAsync(),
                DaGiaoCount = await allPhieuXuatsForStats.CountAsync(px => px.TrangThai == PhieuXuatTrangThai.DaXuat),
                DangXuLyCount = await allPhieuXuatsForStats.CountAsync(px => px.TrangThai == PhieuXuatTrangThai.DangXuLy || px.TrangThai == PhieuXuatTrangThai.MoiTao),
                DaHuyCount = await allPhieuXuatsForStats.CountAsync(px => px.TrangThai == PhieuXuatTrangThai.DaHuy)
            };


            // Phân trang
            var totalItems = await query.CountAsync();
            statsModel.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            statsModel.PageIndex = page;

            var pagedData = await query
                                  .Skip((page - 1) * pageSize)
                                  .Take(pageSize)
                                  .ToListAsync();

            statsModel.PhieuXuatItems = pagedData.Select(px => new PhieuXuatListItemViewModel
            {
                MaPhieuXuat = px.MaPhieuXuat,
                NguoiNhan = px.NguoiNhan, // Cần đảm bảo trường này có dữ liệu
                NgayXuat = px.NgayXuat,
                TenKhoXuat = px.KhoHang?.TenKho,
                TongSoLuongItems = px.ChiTietPhieuXuats.Sum(ct => ct.SoLuong),
                // TongGiaTri = px.ChiTietPhieuXuats.Sum(ct => ct.SoLuong * ct.DonGia), // Nếu có đơn giá
                TrangThai = px.TrangThai
            }).ToList();

            // Chuẩn bị Status Options cho Filter Dropdown
            // Bạn có thể lấy từ DB hoặc định nghĩa cứng như trong PhieuXuatTrangThai
            statsModel.StatusOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "all", Text = "Tất cả trạng thái" },
                new SelectListItem { Value = PhieuXuatTrangThai.MoiTao, Text = PhieuXuatTrangThai.MoiTao },
                new SelectListItem { Value = PhieuXuatTrangThai.DangXuLy, Text = PhieuXuatTrangThai.DangXuLy },
                new SelectListItem { Value = PhieuXuatTrangThai.DaXuat, Text = PhieuXuatTrangThai.DaXuat },
                new SelectListItem { Value = PhieuXuatTrangThai.DaHuy, Text = PhieuXuatTrangThai.DaHuy }
            };
            // Đặt selected cho status filter hiện tại
            var currentStatusFilter = statsModel.StatusOptions.FirstOrDefault(o => o.Value == statusFilter);
            if (currentStatusFilter != null) currentStatusFilter.Selected = true;
            else statsModel.StatusOptions.First(o => o.Value == "all").Selected = true;


            statsModel.SearchTerm = searchTerm;
            statsModel.StatusFilter = statusFilter;
            statsModel.DateFilter = dateFilter;

            return View(statsModel);
        }

        // Action GET để hiển thị form tạo phiếu
        private async Task PopulateDropdownsAsync(PhieuXuatCreateViewModel viewModel)
        {
            viewModel.KhoHangOptions = await _context.KhoHangs
                .Where(kh => kh.TrangThai != KhoHangTrangThai.BaoTri && kh.TrangThai != KhoHangTrangThai.NgungSuDung) // Chỉ kho hoạt động
                .OrderBy(kh => kh.TenKho)
                .Select(kh => new SelectListItem { Value = kh.MaKho, Text = kh.TenKho + " (" + kh.MaKho + ")" })
                .ToListAsync();

            viewModel.SanPhamOptions = await _context.SanPhams
                .OrderBy(lsp => lsp.TenSanPham)
                .Select(lsp => new SelectListItem { Value = lsp.M_SanPham, Text = lsp.TenSanPham + " (" + lsp.M_SanPham + ")" })
                .ToListAsync();

            // Đơn vị tính có thể được lọc dựa trên sản phẩm được chọn (nếu cần, phức tạp hơn)
            // Hoặc hiển thị tất cả đơn vị tính.
            viewModel.DonViTinhOptions = await _context.DonViTinhs
                .OrderBy(dvt => dvt.TenLoaiTinh)
                .Select(dvt => new SelectListItem { Value = dvt.M_DonViTinh, Text = dvt.TenLoaiTinh })
                .ToListAsync();
        }


        // GET: QuanLyXNK/PhieuXuat/Create
        public async Task<IActionResult> Create()
        {
            var viewModel = new PhieuXuatCreateViewModel();
            // Thêm một dòng chi tiết mặc định nếu muốn
            viewModel.ChiTietItems.Add(new ChiTietPhieuXuatItemViewModel());
            await PopulateDropdownsAsync(viewModel);
            return View(viewModel);
        }

        // POST: QuanLyXNK/PhieuXuat/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PhieuXuatCreateViewModel viewModel)
        {
            if (viewModel.ChiTietItems == null || !viewModel.ChiTietItems.Any(ct => !string.IsNullOrEmpty(ct.M_LoaiSP) && ct.SoLuong > 0))
            {
                ModelState.AddModelError("ChiTietItems", "Vui lòng thêm ít nhất một sản phẩm hợp lệ vào phiếu xuất.");
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState không hợp lệ khi tạo Phiếu Xuất.");
                await PopulateDropdownsAsync(viewModel); // Load lại dropdowns cho view
                return View(viewModel);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            _logger.LogInformation("Bắt đầu transaction tạo Phiếu Xuất cho kho {MaKho}.", viewModel.MaKho);

            try
            {
                // 1. KIỂM TRA TỒN KHO CHO TẤT CẢ SẢN PHẨM
                foreach (var ctItemVM in viewModel.ChiTietItems.Where(ct => !string.IsNullOrEmpty(ct.M_LoaiSP) && ct.SoLuong > 0))
                {
                    // Giả sử MaDonViTinh trong TonKho.cs là FK và đã được sửa đúng
                    var tonKhoHienTai = await _context.LoTonKhos
                        .AsNoTracking() // Chỉ đọc, không theo dõi thay đổi ở bước này
                        .FirstOrDefaultAsync(tk =>
                            tk.MaKho == viewModel.MaKho &&
                            tk.M_SanPham == ctItemVM.M_LoaiSP &&
                            tk.M_DonViTinh == ctItemVM.M_DonViTinh); // Sử dụng MaDonViTinh

                    if (tonKhoHienTai == null || tonKhoHienTai.KhoiLuongConLai < ctItemVM.SoLuong)
                    {
                        await transaction.RollbackAsync();
                        string tenSP = _context.SanPhams.FirstOrDefault(l => l.M_SanPham == ctItemVM.M_LoaiSP)?.M_SanPham ?? ctItemVM.M_LoaiSP;
                        _logger.LogWarning("Rollback transaction do không đủ tồn kho cho SP {MaSP} ({TenSP}) tại Kho {MaKho}. Tồn: {Ton}, Xuất: {Xuat}",
                            ctItemVM.M_LoaiSP, tenSP, viewModel.MaKho, tonKhoHienTai?.KhoiLuongConLai ?? 0, ctItemVM.SoLuong);
                        ModelState.AddModelError("", $"Không đủ tồn kho cho sản phẩm '{tenSP}'. Tồn hiện tại: {tonKhoHienTai?.KhoiLuongConLai ?? 0}, Yêu cầu xuất: {ctItemVM.SoLuong}.");
                        await PopulateDropdownsAsync(viewModel);
                        return View(viewModel);
                    }
                }
                _logger.LogInformation("Kiểm tra tồn kho thành công cho tất cả sản phẩm trong phiếu xuất.");

                // 2. TẠO PHIẾU XUẤT VÀ CÁC CHI TIẾT
                var phieuXuat = new PhieuXuat
                {
                    // MaPhieuXuat sẽ tự tăng (int) hoặc bạn cần cơ chế tạo mã riêng nếu là string
                    NgayXuat = viewModel.NgayXuat,
                    MaKho = viewModel.MaKho,
                    LyDoXuat = viewModel.LyDoXuat,
                    ChiTietPhieuXuats = new List<ChiTietPhieuXuat>()
                };

                foreach (var ctItemVM in viewModel.ChiTietItems.Where(ct => !string.IsNullOrEmpty(ct.M_LoaiSP) && ct.SoLuong > 0))
                {
                    var chiTiet = new ChiTietPhieuXuat
                    {
                        M_SanPham = ctItemVM.M_LoaiSP,
                        M_DonViTinh = ctItemVM.M_DonViTinh, // Đảm bảo M_DonViTinh này khớp với TonKho.MaDonViTinh
                        SoLuong = ctItemVM.SoLuong
                        // PhieuXuatId sẽ được EF Core tự gán khi Add phieuXuat
                    };
                    phieuXuat.ChiTietPhieuXuats.Add(chiTiet);

                    // 3. CHUẨN BỊ CẬP NHẬT (TRỪ) TỒN KHO
                    // Lấy lại bản ghi TonKho để theo dõi và cập nhật (không dùng AsNoTracking)
                    var tonKhoItemToUpdate = await _context.LoTonKhos
                        .FirstOrDefaultAsync(tk =>
                            tk.MaKho == phieuXuat.MaKho &&
                            tk.M_SanPham == chiTiet.M_SanPham &&
                            tk.M_DonViTinh == chiTiet.M_DonViTinh); // Sử dụng MaDonViTinh

                    if (tonKhoItemToUpdate != null) // Điều này luôn đúng do đã kiểm tra ở trên
                    {
                        tonKhoItemToUpdate.KhoiLuongConLai -= chiTiet.SoLuong;
                        _context.Update(tonKhoItemToUpdate);
                        _logger.LogInformation("Chuẩn bị trừ kho cho SP {MaSP} tại Kho {MaKho}, số lượng trừ: {SoLuongTru}. Tồn mới dự kiến: {TonMoi}",
                            tonKhoItemToUpdate.M_SanPham, tonKhoItemToUpdate.MaKho, chiTiet.SoLuong, tonKhoItemToUpdate.KhoiLuongConLai);
                    }
                }
                _context.PhieuXuats.Add(phieuXuat); // Thêm phiếu xuất (và các chi tiết của nó) vào context
                _logger.LogInformation("Đã chuẩn bị tạo PhieuXuat Mã Kho {MaKho} và các chi tiết, đồng thời chuẩn bị cập nhật TonKho.", phieuXuat.MaKho);

                // 4. LƯU TẤT CẢ THAY ĐỔI VÀO DATABASE
                await _context.SaveChangesAsync();
                _logger.LogInformation("SaveChangesAsync thành công cho Phiếu Xuất.");

                // 5. COMMIT TRANSACTION
                await transaction.CommitAsync();
                _logger.LogInformation("Commit transaction thành công. Phiếu xuất đã được tạo và tồn kho đã cập nhật.");

                TempData["SuccessMessage"] = $"Phiếu xuất (ID: {phieuXuat.MaPhieuXuat}) đã được tạo thành công và tồn kho đã được cập nhật.";
                return RedirectToAction("Index", "TonKho"); // Hoặc một trang khác, ví dụ: chi tiết phiếu xuất vừa tạo

            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi trong quá trình tạo Phiếu Xuất. Transaction đã được rollback.");
                ModelState.AddModelError("", "Đã xảy ra lỗi nghiêm trọng khi tạo phiếu xuất: " + ex.Message + (ex.InnerException != null ? " Inner Error: " + ex.InnerException.Message : ""));
                await PopulateDropdownsAsync(viewModel);
                return View(viewModel);
            }
        }
    }
}
