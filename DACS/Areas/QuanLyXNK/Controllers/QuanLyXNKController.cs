using DACS.Models;
using DACS.Models.ViewModels;
using DACS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DACS.Services;

namespace DACS.Areas.QuanLyXNK.Controllers
{
    [Area("QuanLyXNK")] // Chỉ định Area
    [Authorize(Roles = "Owner,QuanLyXNK")]
    public class QuanLyXNKController : Controller
    {

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<QuanLyXNKController> _logger;
        private readonly BlockchainService _blockchainService;
        private readonly AIMatchingService _aiMatchingService;

        public QuanLyXNKController(ApplicationDbContext context,
                                     UserManager<ApplicationUser> userManager,
                                     ILogger<QuanLyXNKController> logger, BlockchainService blockchainService,
                                     AIMatchingService aiMatchingService
                                     )
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _blockchainService = blockchainService;
            _aiMatchingService = aiMatchingService;
        }

        public async Task<IActionResult> Index(string? searchTerm, DateTime? dateFilter, string? statusFilter, string? collectorFilter, int page = 1)
        {
            int pageSize = 10;

            // <<< SỬA: Logic Include Địa chỉ và Sản phẩm
            var query = _context.YeuCauThuGoms
                .Include(yc => yc.KhachHang) // Giữ lại để lấy Tên, SDT
                .Include(yc => yc.XaPhuong)  // <<< SỬA: Lấy địa chỉ từ YeuCauThuGom
                .Include(yc => yc.QuanHuyen) // <<< SỬA
                .Include(yc => yc.TinhThanhPho) // <<< SỬA
                .Include(yc => yc.QuanLy)
                .Include(yc => yc.ChiTietThuGoms)
                    .ThenInclude(ct => ct.SanPham) // <<< SỬA: Phải qua SanPham
                        .ThenInclude(sp => sp.LoaiSanPham) // <<< SỬA: Rồi mới tới LoaiSanPham
                .Include(yc => yc.ChiTietThuGoms)
                    .ThenInclude(ct => ct.DonViTinh)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                string lowerSearchTerm = searchTerm.ToLower().Trim();
                query = query.Where(yc => yc.M_YeuCau.ToLower().Contains(lowerSearchTerm) ||
                                         (yc.KhachHang != null && yc.KhachHang.Ten_KhachHang != null && yc.KhachHang.Ten_KhachHang.ToLower().Contains(lowerSearchTerm)) ||
                                         (yc.KhachHang != null && yc.KhachHang.SDT_KhachHang != null && yc.KhachHang.SDT_KhachHang.Contains(lowerSearchTerm)));
            }

            if (dateFilter.HasValue)
            {
                query = query.Where(yc => yc.ThoiGianSanSang.Date == dateFilter.Value.Date);
            }

            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "all")
            {
                query = query.Where(yc => yc.TrangThai == statusFilter);
            }

            if (!string.IsNullOrEmpty(collectorFilter) && collectorFilter != "all")
            {
                query = query.Where(yc => yc.M_QuanLy == collectorFilter);
            }

            query = query.OrderByDescending(yc => yc.NgayYeuCau);

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var pagedData = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var stats = await CalculateStatisticsAsync(query);

            var statusOptions = await GetStatusOptionsAsync(statusFilter);
            var collectorOptions = await GetCollectorOptionsAsync(collectorFilter);

            var activeWarehouseEntities = await _context.KhoHangs
                .Where(kh => kh.TrangThai != KhoHangTrangThai.BaoTri && kh.TrangThai != KhoHangTrangThai.NgungSuDung)
                .OrderBy(kh => kh.TenKho)
                .ToListAsync();

            var activeWarehouses = activeWarehouseEntities
                .Select(kh => new SelectListItem
                {
                    Value = kh.MaKho,
                    Text = $"{kh.TenKho} ({kh.MaKho})"
                })
                .ToList();

            var warehouseSuggestions = await GetWarehouseSuggestionsAsync(pagedData, activeWarehouseEntities);
            var listViewModel = pagedData.Select(yc => MapToListItemViewModel(yc, warehouseSuggestions)).ToList();

            var viewModel = new QuanLyThuGomViewModel
            {
                DanhSachYeuCau = listViewModel,
                Statistics = stats,
                SearchTerm = searchTerm,
                DateFilter = dateFilter,
                StatusFilter = statusFilter,
                CollectorFilter = collectorFilter,
                StatusOptions = statusOptions,
                CollectorOptions = collectorOptions,
                ActiveKhoHangOptions = activeWarehouses,
                PageIndex = page,
                TotalPages = totalPages
            };

            return View(viewModel);
        }

        private YeuCauListItemViewModel MapToListItemViewModel(
            YeuCauThuGom yc,
            IReadOnlyDictionary<string, AIKhoHangSuggestionViewModel>? warehouseSuggestions = null)
        {
            var firstDetail = yc.ChiTietThuGoms?.FirstOrDefault();
            var kh = yc.KhachHang;

            // <<< SỬA: Xây dựng địa chỉ tóm tắt từ YeuCauThuGom
            var diaChiParts = new List<string?>();
            if (!string.IsNullOrWhiteSpace(yc.DiaChi_DuongApThon)) diaChiParts.Add(yc.DiaChi_DuongApThon);
            if (!string.IsNullOrWhiteSpace(yc.XaPhuong?.TenXa)) diaChiParts.Add(yc.XaPhuong.TenXa);
            if (!string.IsNullOrWhiteSpace(yc.QuanHuyen?.TenQuan)) diaChiParts.Add(yc.QuanHuyen.TenQuan);
            if (!string.IsNullOrWhiteSpace(yc.TinhThanhPho?.TenTinh)) diaChiParts.Add(yc.TinhThanhPho.TenTinh);
            string diaChiTomTat = string.Join(", ", diaChiParts.Where(s => !string.IsNullOrEmpty(s)));


            return new YeuCauListItemViewModel
            {
                M_YeuCau = yc.M_YeuCau,
                TenKhachHang = kh?.Ten_KhachHang,
                SdtKhachHang = kh?.SDT_KhachHang,
                DiaChiTomTat = diaChiTomTat, // <<< SỬA
                TenLoaiSanPham = firstDetail?.SanPham?.LoaiSanPham?.TenLoai, // <<< SỬA
                SoLuong = firstDetail?.SoLuong ?? 0,
                TenDonViTinh = firstDetail?.DonViTinh?.TenLoaiTinh,
                NgayYeuCau = yc.NgayYeuCau,
                NgayThuGom = yc.ThoiGianSanSang != default ? yc.ThoiGianSanSang : (DateTime?)null,
                TrangThai = yc.TrangThai,
                TenNguoiThuGom = yc.QuanLy?.FullName
                                ?? yc.QuanLy?.UserName
                                ?? "Chưa gán",
                GoiYKhoHang = warehouseSuggestions != null && warehouseSuggestions.TryGetValue(yc.M_YeuCau, out var suggestion)
                    ? suggestion
                    : null
            };
        }

        private async Task<Dictionary<string, AIKhoHangSuggestionViewModel>> GetWarehouseSuggestionsAsync(
            List<YeuCauThuGom> requests,
            List<KhoHang> warehouses)
        {
            if (!requests.Any() || !warehouses.Any())
            {
                return new Dictionary<string, AIKhoHangSuggestionViewModel>(StringComparer.OrdinalIgnoreCase);
            }

            var eligibleRequests = requests
                .Where(yc => yc.TrangThai != "Hoàn thành" && yc.TrangThai != "Thu gom thành công" && yc.TrangThai != "Đã hủy")
                .Select(yc => new
                {
                    m_yeucau = yc.M_YeuCau,
                    ten_san_pham = yc.ChiTietThuGoms.FirstOrDefault()?.SanPham?.TenSanPham ?? string.Empty,
                    so_luong = yc.ChiTietThuGoms.FirstOrDefault()?.SoLuong ?? 0,
                    thoi_gian_san_sang = yc.ThoiGianSanSang != default ? yc.ThoiGianSanSang.ToString("O") : null,
                    tinh_thanh = yc.TinhThanhPho?.TenTinh,
                    quan_huyen = yc.QuanHuyen?.TenQuan,
                    xa_phuong = yc.XaPhuong?.TenXa,
                    lat = yc.Lat,
                    lng = yc.Lng
                })
                .Cast<object>()
                .ToList();

            if (!eligibleRequests.Any())
            {
                return new Dictionary<string, AIKhoHangSuggestionViewModel>(StringComparer.OrdinalIgnoreCase);
            }

            var warehouseInputs = warehouses
                .Select(kh =>
                {
                    var segments = SplitAddressSegments(kh.DiaChi);
                    return new
                    {
                        ma_kho = kh.MaKho,
                        ten_kho = kh.TenKho,
                        dia_chi = kh.DiaChi,
                        trang_thai = kh.TrangThai,
                        xa_phuong = segments.ward,
                        quan_huyen = segments.district,
                        tinh_thanh = segments.province,
                        lat = kh.Lat,
                        lng = kh.Lng
                    };
                })
                .Cast<object>()
                .ToList();

            return await _aiMatchingService.GetWarehouseSuggestionsAsync(eligibleRequests, warehouseInputs);
        }

        private static (string? ward, string? district, string? province) SplitAddressSegments(string? address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return (null, null, null);
            }

            var segments = address.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var province = segments.Length >= 1 ? segments[^1] : null;
            var district = segments.Length >= 2 ? segments[^2] : null;
            var ward = segments.Length >= 3 ? segments[^3] : null;

            return (ward, district, province);
        }

        private async Task<ThuGomStatisticsViewModel> CalculateStatisticsAsync(IQueryable<YeuCauThuGom> baseQuery)
        {
            // (Logic này của bạn đã đúng)
            var allRequests = _context.YeuCauThuGoms;
            var today = DateTime.UtcNow.Date;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            if (today.DayOfWeek == DayOfWeek.Sunday)
            {
                startOfWeek = startOfWeek.AddDays(-7);
            }

            return new ThuGomStatisticsViewModel
            {
                YeuCauMoi = await allRequests.CountAsync(yc => yc.TrangThai == "Chờ xử lý"),
                DaLenLich = await allRequests.CountAsync(yc => yc.TrangThai == "Đã lên lịch"),
                DangThucHien = await allRequests.CountAsync(yc => yc.TrangThai == "Đang thu gom"),
                HoanThanhTrongTuan = await allRequests.CountAsync(yc =>
                    (yc.TrangThai == "Hoàn thành" || yc.TrangThai == "Thu gom thành công") &&
                    yc.ThoiGianHoanThanh >= startOfWeek && yc.ThoiGianHoanThanh < startOfWeek.AddDays(7))
            };
        }

        private async Task<List<SelectListItem>> GetStatusOptionsAsync(string? selectedStatus)
        {
            // (Logic này của bạn đã đúng)
            var statuses = await _context.YeuCauThuGoms
                                    .Select(yc => yc.TrangThai)
                                    .Distinct()
                                    .Where(s => !string.IsNullOrEmpty(s))
                                    .OrderBy(s => s)
                                    .ToListAsync();

            var options = new List<SelectListItem>
            {
                new SelectListItem { Value = "all", Text = "Tất cả trạng thái" }
            };

            options.AddRange(statuses.Select(s => new SelectListItem
            {
                Value = s,
                Text = s,
                Selected = s == selectedStatus
            }));

            return options;
        }

        private async Task<List<SelectListItem>> GetCollectorOptionsAsync(string? selectedCollectorId)
        {
            // (Logic này của bạn đã đúng)
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var managers = await _userManager.GetUsersInRoleAsync("QuanLyXNK"); // Giả sử Owner cũng có thể là manager
            var owner = await _userManager.GetUsersInRoleAsync("Owner");
            var collectors = admins.Union(managers).Union(owner).DistinctBy(u => u.Id).OrderBy(u => u.UserName).ToList();


            var options = new List<SelectListItem>
            {
                new SelectListItem { Value = "all", Text = "Tất cả người thu gom" }
            };

            options.AddRange(collectors.Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = u.FullName ?? u.UserName ?? u.Email,
                Selected = u.Id == selectedCollectorId
            }));

            options.Add(new SelectListItem { Value = "unassigned", Text = "Chưa gán", Selected = selectedCollectorId == "unassigned" });

            return options;
        }

        public async Task<IActionResult> Details(string id)
        {
            if (id == null) return NotFound();

            // <<< SỬA: Logic Include
            var yeuCau = await _context.YeuCauThuGoms
                .Include(yc => yc.KhachHang)
                .Include(yc => yc.XaPhuong) // <<< SỬA
                .Include(yc => yc.QuanHuyen) // <<< SỬA
                .Include(yc => yc.TinhThanhPho) // <<< SỬA
                .Include(yc => yc.QuanLy)
                .Include(yc => yc.ChiTietThuGoms)
                    .ThenInclude(ct => ct.SanPham) // <<< SỬA
                        .ThenInclude(sp => sp.LoaiSanPham) // <<< SỬA
                .Include(yc => yc.ChiTietThuGoms)
                    .ThenInclude(ct => ct.DonViTinh)
                .FirstOrDefaultAsync(m => m.M_YeuCau == id);

            if (yeuCau == null) return NotFound();
            return View(yeuCau);
        }

        public async Task<IActionResult> Schedule(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            // <<< SỬA: Include địa chỉ
            var yeuCau = await _context.YeuCauThuGoms
                                        .Include(yc => yc.KhachHang)
                                        .Include(yc => yc.XaPhuong)
                                        .Include(yc => yc.QuanHuyen)
                                        .Include(yc => yc.TinhThanhPho)
                                        .FirstOrDefaultAsync(yc => yc.M_YeuCau == id);

            if (yeuCau == null || yeuCau.TrangThai != "Chờ xử lý")
            {
                TempData["ErrorMessage"] = "Yêu cầu không tồn tại hoặc không ở trạng thái 'Chờ xử lý'.";
                return RedirectToAction(nameof(Index));
            }

            var viewModel = new ScheduleViewModel
            {
                M_YeuCau = yeuCau.M_YeuCau,
                TenKhachHang = yeuCau.KhachHang?.Ten_KhachHang,
                DiaChiTomTat = FormatAddress(yeuCau), // <<< SỬA: Truyền YeuCau
                ThoiGianSanSang = DateTime.Now.AddDays(1)
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Schedule(ScheduleViewModel model)
        {
            // <<< SỬA: Include địa chỉ
            var yeuCauContext = await _context.YeuCauThuGoms
                                    .Include(yc => yc.KhachHang)
                                    .Include(yc => yc.XaPhuong)
                                    .Include(yc => yc.QuanHuyen)
                                    .Include(yc => yc.TinhThanhPho)
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(yc => yc.M_YeuCau == model.M_YeuCau);

            if (!ModelState.IsValid)
            {
                if (yeuCauContext != null)
                {
                    model.TenKhachHang = yeuCauContext.KhachHang?.Ten_KhachHang;
                    model.DiaChiTomTat = FormatAddress(yeuCauContext); // <<< SỬA
                }
                return View(model);
            }

            var yeuCau = await _context.YeuCauThuGoms.FindAsync(model.M_YeuCau);

            if (yeuCau == null || yeuCau.TrangThai != "Chờ xử lý")
            {
                TempData["ErrorMessage"] = "Yêu cầu không tồn tại hoặc đã được xử lý.";
                return RedirectToAction(nameof(Index));
            }

            yeuCau.ThoiGianSanSang = model.ThoiGianSanSang;
            yeuCau.TrangThai = "Đã lên lịch";

            try
            {
                _context.Update(yeuCau);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã lên lịch thành công cho yêu cầu {yeuCau.M_YeuCau}.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                ModelState.AddModelError("", "Lỗi khi lưu lịch hẹn: " + ex.Message);
                if (yeuCauContext != null)
                {
                    model.TenKhachHang = yeuCauContext.KhachHang?.Ten_KhachHang;
                }
                return View(model);
            }
        }

        // <<< SỬA: Hàm này phải nhận YeuCauThuGom
        private string? FormatAddress(YeuCauThuGom? yeuCau)
        {
            if (yeuCau == null) return "N/A";
            var parts = new List<string?>();
            if (!string.IsNullOrWhiteSpace(yeuCau.DiaChi_DuongApThon)) parts.Add(yeuCau.DiaChi_DuongApThon);
            if (!string.IsNullOrWhiteSpace(yeuCau.XaPhuong?.TenXa)) parts.Add(yeuCau.XaPhuong.TenXa);
            if (!string.IsNullOrWhiteSpace(yeuCau.QuanHuyen?.TenQuan)) parts.Add(yeuCau.QuanHuyen.TenQuan);
            if (!string.IsNullOrWhiteSpace(yeuCau.TinhThanhPho?.TenTinh)) parts.Add(yeuCau.TinhThanhPho.TenTinh);
            if (!parts.Any()) return "Chưa rõ địa chỉ chi tiết";
            return string.Join(", ", parts.Where(s => !string.IsNullOrEmpty(s)));
        }

        public async Task<IActionResult> EditSchedule(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            // <<< SỬA: Include địa chỉ
            var yeuCau = await _context.YeuCauThuGoms
                                        .Include(yc => yc.KhachHang)
                                        .Include(yc => yc.XaPhuong)
                                        .Include(yc => yc.QuanHuyen)
                                        .Include(yc => yc.TinhThanhPho)
                                        .FirstOrDefaultAsync(yc => yc.M_YeuCau == id);

            if (yeuCau == null || yeuCau.TrangThai != "Đã lên lịch")
            {
                TempData["ErrorMessage"] = "Yêu cầu không tồn tại hoặc không ở trạng thái cho phép sửa lịch.";
                return RedirectToAction(nameof(Index));
            }

            var viewModel = new ScheduleViewModel
            {
                M_YeuCau = yeuCau.M_YeuCau,
                TenKhachHang = yeuCau.KhachHang?.Ten_KhachHang,
                DiaChiTomTat = FormatAddress(yeuCau), // <<< SỬA
                ThoiGianSanSang = yeuCau.ThoiGianSanSang
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSchedule(ScheduleViewModel model)
        {
            // <<< SỬA: Include địa chỉ
            var yeuCauContext = await _context.YeuCauThuGoms
                                    .Include(yc => yc.KhachHang)
                                    .Include(yc => yc.XaPhuong)
                                    .Include(yc => yc.QuanHuyen)
                                    .Include(yc => yc.TinhThanhPho)
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(yc => yc.M_YeuCau == model.M_YeuCau);

            if (!ModelState.IsValid)
            {
                if (yeuCauContext != null)
                {
                    model.TenKhachHang = yeuCauContext.KhachHang?.Ten_KhachHang;
                    model.DiaChiTomTat = FormatAddress(yeuCauContext); // <<< SỬA
                }
                return View(model);
            }

            var yeuCau = await _context.YeuCauThuGoms.FindAsync(model.M_YeuCau);

            if (yeuCau == null || yeuCau.TrangThai != "Đã lên lịch")
            {
                TempData["ErrorMessage"] = "Yêu cầu không tồn tại hoặc trạng thái đã thay đổi, không thể cập nhật lịch.";
                return RedirectToAction(nameof(Index));
            }

            yeuCau.ThoiGianSanSang = model.ThoiGianSanSang;

            try
            {
                _context.Update(yeuCau);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã cập nhật lịch thành công cho yêu cầu {yeuCau.M_YeuCau}.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                ModelState.AddModelError("", "Lỗi khi cập nhật lịch: " + ex.Message);
                if (yeuCauContext != null)
                {
                    model.TenKhachHang = yeuCauContext.KhachHang?.Ten_KhachHang;
                }
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminCancel(string id)
        {
            if (id == null) return NotFound();

            var yeuCau = await _context.YeuCauThuGoms.FindAsync(id);
            if (yeuCau == null || yeuCau.TrangThai != "Chờ xử lý")
            {
                return RedirectToAction(nameof(Index));
            }

            yeuCau.TrangThai = "Đã hủy";

            try
            {
                _context.Update(yeuCau);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Lỗi khi Hủy YC {YeuCauId}", id);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartCollection(string id)
        {
            if (id == null) return NotFound();
            var yeuCau = await _context.YeuCauThuGoms.FindAsync(id);
            if (yeuCau == null || yeuCau.TrangThai != "Đã lên lịch")
            {
                return RedirectToAction(nameof(Index));
            }

            yeuCau.TrangThai = "Đang thu gom";

            try
            {
                _context.Update(yeuCau);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) { _logger.LogError(ex, "Lỗi khi Bắt đầu YC {YeuCauId}", id); }
            return RedirectToAction(nameof(Index));
        }

        // =========================================================================
        // ================= SỬA LỖI LOGIC NHẬP KHO (MARKCOMPLETE) =================
        // =========================================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkComplete(string id, [FromQuery] string targetMaKho)
        {
            if (string.IsNullOrEmpty(id)) return NotFound("Thiếu ID yêu cầu.");
            if (string.IsNullOrEmpty(targetMaKho))
            {
                TempData["ErrorMessage"] = $"Vui lòng chọn kho đích để hoàn thành yêu cầu {id}.";
                _logger.LogWarning("MarkComplete được gọi cho YC {YeuCauId} nhưng thiếu targetMaKho.", id);
                return RedirectToAction(nameof(Index));
            }

            var khoDicExists = await _context.KhoHangs.AnyAsync(kh => kh.MaKho == targetMaKho && kh.TrangThai != KhoHangTrangThai.BaoTri && kh.TrangThai != KhoHangTrangThai.NgungSuDung);
            if (!khoDicExists)
            {
                TempData["ErrorMessage"] = $"Kho đích '{targetMaKho}' không tồn tại.";
                _logger.LogWarning("MarkComplete được gọi cho YC {YeuCauId} với targetMaKho không hợp lệ: {targetMaKho}.", id, targetMaKho);
                return RedirectToAction(nameof(Index));
            }

            _logger.LogInformation("Bắt đầu xử lý hoàn thành YC {YeuCauId} vào Kho {targetMaKho}.", id, targetMaKho);

            using var transaction = await _context.Database.BeginTransactionAsync();
            _logger.LogInformation("Bắt đầu transaction cho YC {YeuCauId}.", id);

            try
            {
                // <<< SỬA: Logic Include
                var yeuCau = await _context.YeuCauThuGoms
                                        .Include(yc => yc.ChiTietThuGoms)
                                            .ThenInclude(ct => ct.SanPham)
                                        .Include(yc => yc.ChiTietThuGoms)
                                            .ThenInclude(ct => ct.DonViTinh)
                                        .FirstOrDefaultAsync(yc => yc.M_YeuCau == id);

                if (yeuCau == null || yeuCau.TrangThai != "Đang thu gom")
                {
                    TempData["ErrorMessage"] = "Không thể đánh dấu hoàn thành (không tồn tại hoặc không ở trạng thái 'Đang thu gom').";
                    _logger.LogWarning("Thất bại khi MarkComplete YC {YeuCauId}: Không tìm thấy hoặc trạng thái không hợp lệ ({TrangThai}).", id, yeuCau?.TrangThai ?? "Không tìm thấy");
                    await transaction.RollbackAsync();
                    _logger.LogInformation("Đã rollback transaction do YC {YeuCauId} không hợp lệ.", id);
                    return RedirectToAction(nameof(Index));
                }

                yeuCau.TrangThai = "Hoàn thành";
                yeuCau.ThoiGianHoanThanh = DateTime.UtcNow;
                _context.Update(yeuCau);
                _logger.LogInformation("Đã chuẩn bị cập nhật trạng thái YC {YeuCauId} thành 'Hoàn thành'.", id);

                // <<< SỬA: BỎ LOGIC CỘNG DỒN, THAY BẰNG TẠO MỚI
                if (yeuCau.ChiTietThuGoms != null && yeuCau.ChiTietThuGoms.Any())
                {
                    foreach (var ct in yeuCau.ChiTietThuGoms)
                    {
                        // <<< SỬA: Dùng M_SanPham
                        if (ct.SoLuong <= 0 || string.IsNullOrEmpty(ct.M_SanPham) || string.IsNullOrEmpty(ct.M_DonViTinh))
                        {
                            _logger.LogWarning("Bỏ qua ChiTietThuGom Id {ChiTietId} ...", ct.M_ChiTiet);
                            continue;
                        }

                        string maSanPham = ct.M_SanPham; // <<< SỬA
                        decimal khoiLuongCollected = (decimal)ct.SoLuong; // <<< SỬA: Dùng decimal
                        string maDonViTinh = ct.M_DonViTinh;

                        // <<< SỬA: LUÔN TẠO LÔ MỚI
                        string newMaLo = await GenerateLotCodeAsync(); // Tạo mã lô mới
                        var newLoTonKho = new LoTonKho
                        {
                            MaLoTonKho = newMaLo,
                            MaKho = targetMaKho,
                            M_SanPham = maSanPham,
                            M_DonViTinh = maDonViTinh,
                            NgayNhapKho = DateTime.UtcNow,
                            KhoiLuongBanDau = khoiLuongCollected, // <<< SỬA: Dùng đúng thuộc tính
                            KhoiLuongConLai = khoiLuongCollected  // <<< SỬA: Dùng đúng thuộc tính
                        };
                        _context.Add(newLoTonKho);
                        _logger.LogInformation("Chuẩn bị TẠO MỚI LoTonKho: MaLo={MaLo}, Kho={MaKho}, SP={MaSP}, KL={KhoiLuong}", newMaLo, targetMaKho, maSanPham, khoiLuongCollected);
                        // <<< ================= BẮT ĐẦU GỌI BLOCKCHAIN ================= >>>
                        try
                        {
                            dynamic result1 = await _blockchainService.GhiNhatKyAsync(
    newMaLo,
    "NHẬP KHO",
    targetMaKho,
    $"Từ Yêu cầu: {yeuCau}"
);

                            string txHash1 = result1.TxHash;

                            _logger.LogInformation("ĐÃ GHI BLOCKCHAIN (Nhập Kho) cho Lô {MaLo}, TxHash: {TxHash}", newMaLo, txHash1);

                            // (Bạn có thể thêm 1 cột [BlockchainHashNhap] vào LoTonKho để lưu txHash này nếu muốn)
                            // newLoTonKho.BlockchainHashNhap = txHash; 
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "LỖI GHI BLOCKCHAIN (Nhập Kho) cho Lô {MaLo}. Giao dịch CSDL sẽ tiếp tục.", newMaLo);
                            // Không Rollback transaction, chấp nhận việc ghi CSDL thành công nhưng Blockchain thất bại
                        }
                        // <<< ================= KẾT THÚC GỌI BLOCKCHAIN ================= >>>
                        // <<< THÊM: Liên kết ChiTietThuGom với Lô vừa tạo
                        ct.MaLoTonKho = newMaLo;
                        ct.TrangThaiXuLy = "Đã nhập kho";
                        _context.Update(ct);
                    }
                }
                else
                {
                    _logger.LogWarning("YC {YeuCauId} không có ChiTietThuGom hợp lệ để cập nhật TonKho.", id);
                }

                await _context.SaveChangesAsync(); // *** Chỉ gọi SaveChangesAsync một lần ***
                _logger.LogInformation("Đã lưu thành công các thay đổi vào DB cho YC {YeuCauId}.", id);

                await transaction.CommitAsync();
                _logger.LogInformation("Đã commit transaction thành công cho YC {YeuCauId}.", id);
                TempData["SuccessMessage"] = $"Yêu cầu {id} đã hoàn thành và đã tạo lô mới trong kho {targetMaKho}.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi trong quá trình MarkComplete và cập nhật LoTonKho cho YC {YeuCauId}. Transaction đã được rollback.", id);
                if (ex.InnerException != null)
                {
                    _logger.LogError(ex.InnerException, "Inner Exception details for YC {YeuCauId}:", id);
                }
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi cập nhật trạng thái và tồn kho: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // <<< THÊM: HÀM HELPER TẠO MÃ LÔ (Tương tự GenerateRequestCode) ---
        private async Task<string> GenerateLotCodeAsync()
        {
            string prefix = "LOT" + DateTime.Now.ToString("yyMMdd");

            var todayCodes = await _context.LoTonKhos
                .Where(y => y.MaLoTonKho.StartsWith(prefix))
                .Select(y => y.MaLoTonKho)
                .ToListAsync(); // Lấy về RAM

            int nextNumber = 1;

            if (todayCodes.Any())
            {
                var lastNumber = todayCodes
                    .Select(code => {
                        string numberPart = code.Substring(prefix.Length);
                        int.TryParse(numberPart, out int parsed);
                        return parsed;
                    })
                    .Max();
                nextNumber = lastNumber + 1;
            }

            return prefix + nextNumber.ToString("D3"); // D3 = 001, 002
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkFailed(string id, string? failureReason)
        {
            if (id == null) return NotFound();
            var yeuCau = await _context.YeuCauThuGoms.FindAsync(id);

            if (yeuCau == null || (yeuCau.TrangThai != "Đang thu gom" && yeuCau.TrangThai != "Đã lên lịch"))
            {
                return RedirectToAction(nameof(Index));
            }

            yeuCau.TrangThai = "Thu gom thất bại";
            yeuCau.ThoiGianHoanThanh = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(failureReason))
            {
                yeuCau.GhiChu = $"Thất bại: {failureReason}";
            }

            try
            {
                _context.Update(yeuCau);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) { _logger.LogError(ex, "Lỗi khi đánh dấu thất bại YC {YeuCauId}", id); }
            return RedirectToAction(nameof(Index));
        }

        public IActionResult CreateManualEntry()
        {
            return View();
        }
    }
}