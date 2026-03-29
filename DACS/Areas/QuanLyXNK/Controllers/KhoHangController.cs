using DACS.Models;
using DACS.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DACS.Areas.QuanLyXNK.Controllers
{
    [Area("QuanLyXNK")]
    [Authorize(Roles = SD.Role_Owner + "," + SD.Role_QuanLyXNK)] // Thêm phân quyền
    public class KhoHangController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly int _pageSize = 10; // Số kho trên mỗi trang

        public KhoHangController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: QuanLyXNK/KhoHang
        public async Task<IActionResult> Index(string? searchTerm, string? statusFilter, int page = 1)
        {
            var query = _context.KhoHangs.AsQueryable();

            // Lọc
            if (!string.IsNullOrEmpty(searchTerm))
            {
                string lowerSearchTerm = searchTerm.ToLower().Trim();
                query = query.Where(kh => (kh.MaKho != null && kh.MaKho.ToLower().Contains(lowerSearchTerm)) ||
                                           (kh.TenKho != null && kh.TenKho.ToLower().Contains(lowerSearchTerm)) ||
                                           (kh.DiaChi != null && kh.DiaChi.ToLower().Contains(lowerSearchTerm)));
            }
            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "all")
            {
                query = query.Where(kh => kh.TrangThai == statusFilter);
            }

            // Sắp xếp
            query = query.OrderBy(kh => kh.TenKho);

            // Thống kê
            var stats = new KhoHangStatsViewModel
            {
                TongKho = await _context.KhoHangs.CountAsync(),
                ConTrong = await _context.KhoHangs.CountAsync(kh => kh.TrangThai == KhoHangTrangThai.ConTrong),
                GanDay = await _context.KhoHangs.CountAsync(kh => kh.TrangThai == KhoHangTrangThai.GanDay),
                BaoTri = await _context.KhoHangs.CountAsync(kh => kh.TrangThai == KhoHangTrangThai.BaoTri),
                NgungSuDung = await _context.KhoHangs.CountAsync(kh => kh.TrangThai == KhoHangTrangThai.NgungSuDung)
                // Thêm các trạng thái khác nếu có
            };


            // Phân trang
            var totalItems = await query.CountAsync();
            var totalPages = (int)System.Math.Ceiling(totalItems / (double)_pageSize);
            var pagedData = await query
                .Skip((page - 1) * _pageSize)
                .Take(_pageSize)
                .ToListAsync();

            // Map sang ViewModel
            var listViewModel = pagedData.Select(kh => new KhoHangListItemViewModel
            {
                MaKho = kh.MaKho,
                TenKho = kh.TenKho,
                DiaChi = kh.DiaChi,
                SucChuaTomTat = kh.SucChuaTomTat, // Giả sử SucChuaTomTat là một thuộc tính string
                TrangThai = kh.TrangThai
            }).ToList();

            // Tạo ViewModel chính
            var viewModel = new KhoHangViewModel
            {
                DanhSachKhoHang = listViewModel,
                Statistics = stats,
                SearchTerm = searchTerm,
                StatusFilter = statusFilter,
                StatusOptions = GetStatusOptions(statusFilter), // Lấy options cho dropdown lọc
                PageIndex = page,
                TotalPages = totalPages
            };

            return View(viewModel);
        }

        // Helper tạo Status Options cho bộ lọc Index
        private List<SelectListItem> GetStatusOptions(string? selectedStatus)
        {
            var options = new List<SelectListItem>
            {
                new SelectListItem { Value = "all", Text = "Tất cả trạng thái" }, // Đổi Text cho rõ nghĩa hơn
                new SelectListItem { Value = KhoHangTrangThai.ConTrong, Text = KhoHangTrangThai.ConTrong },
                new SelectListItem { Value = KhoHangTrangThai.GanDay, Text = KhoHangTrangThai.GanDay },
                new SelectListItem { Value = KhoHangTrangThai.BaoTri, Text = KhoHangTrangThai.BaoTri },
                new SelectListItem { Value = KhoHangTrangThai.NgungSuDung, Text = KhoHangTrangThai.NgungSuDung }
                // Thêm các trạng thái khác nếu có
            };
            var currentSelected = options.FirstOrDefault(o => o.Value == selectedStatus);
            if (currentSelected != null)
            {
                currentSelected.Selected = true;
            }
            else
            {
                // Mặc định chọn "Tất cả trạng thái" nếu không có filter hoặc filter không hợp lệ
                options.First(o => o.Value == "all").Selected = true;
            }
            return options;
        }

        // Helper tạo Status Options cho form Create/Edit (không có "Tất cả")
        private List<SelectListItem> GetEditableStatusOptions(string? selectedStatus)
        {
            var options = new List<SelectListItem>
            {
                // Không có option "all" ở đây
                new SelectListItem { Value = KhoHangTrangThai.ConTrong, Text = KhoHangTrangThai.ConTrong },
                new SelectListItem { Value = KhoHangTrangThai.GanDay, Text = KhoHangTrangThai.GanDay },
                new SelectListItem { Value = KhoHangTrangThai.BaoTri, Text = KhoHangTrangThai.BaoTri },
                new SelectListItem { Value = KhoHangTrangThai.NgungSuDung, Text = KhoHangTrangThai.NgungSuDung }
                // Thêm các trạng thái khác nếu có
            };

            var currentSelected = options.FirstOrDefault(o => o.Value == selectedStatus);
            if (currentSelected != null)
            {
                currentSelected.Selected = true;
            }
            else if (string.IsNullOrEmpty(selectedStatus))
            {
                // Mặc định chọn "Còn trống" khi tạo mới nếu chưa có giá trị nào được chọn
                options.First(o => o.Value == KhoHangTrangThai.ConTrong).Selected = true;
            }
            // Nếu selectedStatus có giá trị nhưng không nằm trong danh sách (ít xảy ra), không chọn gì cả hoặc chọn mặc định

            return options;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUsage(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var khoHang = await _context.KhoHangs.FindAsync(id);
            if (khoHang == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy kho hàng để cập nhật trạng thái sử dụng.";
                return RedirectToAction(nameof(Index));
            }

            khoHang.TrangThai = khoHang.TrangThai == KhoHangTrangThai.NgungSuDung
                ? KhoHangTrangThai.ConTrong
                : KhoHangTrangThai.NgungSuDung;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = khoHang.TrangThai == KhoHangTrangThai.NgungSuDung
                ? $"Đã chuyển kho '{khoHang.TenKho}' sang trạng thái ngưng sử dụng."
                : $"Đã kích hoạt lại kho '{khoHang.TenKho}'.";

            return RedirectToAction(nameof(Index));
        }


        // --- CÁC ACTION CRUD KHÁC ---

        // GET: QuanLyXNK/KhoHang/Details/{id}
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var khoHang = await _context.KhoHangs
                .FirstOrDefaultAsync(m => m.MaKho == id);
            if (khoHang == null)
            {
                return NotFound();
            }
            // TODO: Tạo View Details.cshtml để hiển thị chi tiết kho hàng
            return View(khoHang); // Truyền trực tiếp model KhoHang hoặc tạo ViewModel chi tiết nếu cần
        }

        // GET: QuanLyXNK/KhoHang/Create
        public IActionResult Create()
        {
            // Chuẩn bị danh sách trạng thái cho dropdown trong view Create
            ViewBag.TrangThaiOptions = GetEditableStatusOptions(null); // Truyền null để chọn trạng thái mặc định (Còn trống)
            // Trả về View Create.cshtml (cần tạo file này) với một đối tượng KhoHang mới (rỗng)
            return View(new KhoHang
            {
                TenLoaiKho = "Kho hàng"
            });
        }

        // POST: QuanLyXNK/KhoHang/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MaKho,TenKho,DiaChi,SucChuaTomTat,TrangThai,TenLoaiKho,Lat,Lng")] KhoHang khoHang)
        {
            if (string.IsNullOrWhiteSpace(khoHang.TenLoaiKho))
            {
                khoHang.TenLoaiKho = "Kho hàng";
            }

            // Kiểm tra xem dữ liệu gửi lên có hợp lệ theo các quy tắc validation trong Model KhoHang không
            if (ModelState.IsValid)
            {
                // Kiểm tra xem Mã kho đã tồn tại chưa (quan trọng nếu MaKho do người dùng nhập và là duy nhất)
                if (await _context.KhoHangs.AnyAsync(k => k.MaKho == khoHang.MaKho))
                {
                    // Thêm lỗi vào ModelState để hiển thị cho người dùng
                    ModelState.AddModelError("MaKho", "Mã kho này đã tồn tại. Vui lòng chọn mã khác.");
                    // Chuẩn bị lại danh sách trạng thái và trả về View với dữ liệu đã nhập
                    ViewBag.TrangThaiOptions = GetEditableStatusOptions(khoHang.TrangThai);
                    return View(khoHang);
                }

                // Nếu MaKho chưa tồn tại và các dữ liệu khác hợp lệ
                try
                {
                    _context.Add(khoHang); // Thêm đối tượng KhoHang mới vào DbContext
                    await _context.SaveChangesAsync(); // Lưu các thay đổi vào cơ sở dữ liệu
                    TempData["SuccessMessage"] = "Thêm kho hàng mới thành công!"; // Thông báo thành công (hiển thị ở trang Index)
                    return RedirectToAction(nameof(Index)); // Chuyển hướng về trang danh sách kho hàng
                }
                catch (DbUpdateException ex)
                {
                    // Ghi log lỗi (ví dụ: sử dụng ILogger)
                    // Log.Error(ex, "Lỗi khi thêm kho hàng mới.");
                    ModelState.AddModelError("", "Đã xảy ra lỗi khi lưu dữ liệu. Vui lòng thử lại.");
                    // Có thể thêm các xử lý lỗi cụ thể hơn dựa vào InnerException
                }
            }

            // Nếu ModelState không hợp lệ (dữ liệu nhập vào có lỗi validation)
            // Chuẩn bị lại danh sách trạng thái cho dropdown
            ViewBag.TrangThaiOptions = GetEditableStatusOptions(khoHang.TrangThai);
            // Trả về View Create với đối tượng khoHang chứa dữ liệu người dùng đã nhập và thông tin lỗi validation
            return View(khoHang);
        }


        // GET: QuanLyXNK/KhoHang/Edit/{id}
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var khoHang = await _context.KhoHangs.FindAsync(id);
            if (khoHang == null)
            {
                return NotFound();
            }
            // Chuẩn bị danh sách trạng thái cho dropdown, chọn trạng thái hiện tại của kho hàng
            ViewBag.TrangThaiOptions = GetEditableStatusOptions(khoHang.TrangThai);
            // TODO: Tạo View Edit.cshtml
            return View(khoHang);
        }

        // POST: QuanLyXNK/KhoHang/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("MaKho,TenKho,DiaChi,SucChuaTomTat,TrangThai,TenLoaiKho,Lat,Lng")] KhoHang khoHang)
        {
            if (string.IsNullOrWhiteSpace(khoHang.TenLoaiKho))
            {
                khoHang.TenLoaiKho = "Kho hàng";
            }

            // Đảm bảo ID từ route và ID trong model khớp nhau
            if (id != khoHang.MaKho)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(khoHang); // Đánh dấu đối tượng là đã bị sửa đổi
                    await _context.SaveChangesAsync(); // Lưu thay đổi vào CSDL
                    TempData["SuccessMessage"] = "Cập nhật thông tin kho hàng thành công!";
                    return RedirectToAction(nameof(Index)); // Chuyển về trang danh sách
                }
                catch (DbUpdateConcurrencyException) // Xử lý trường hợp dữ liệu đã bị sửa đổi bởi người khác
                {
                    if (!KhoHangExists(khoHang.MaKho))
                    {
                        return NotFound(); // Không tìm thấy kho để cập nhật
                    }
                    else
                    {
                        ModelState.AddModelError("", "Dữ liệu đã bị thay đổi bởi một người dùng khác. Vui lòng tải lại trang và thử lại.");
                        // Có thể load lại dữ liệu gốc và hiển thị sự khác biệt nếu cần
                    }
                }
                catch (DbUpdateException ex)
                {
                    // Ghi log lỗi
                    ModelState.AddModelError("", "Đã xảy ra lỗi khi cập nhật dữ liệu. Vui lòng thử lại.");
                }
            }
            // Nếu ModelState không hợp lệ hoặc có lỗi xảy ra
            ViewBag.TrangThaiOptions = GetEditableStatusOptions(khoHang.TrangThai); // Chuẩn bị lại dropdown
            return View(khoHang); // Trả về view Edit với dữ liệu hiện tại và lỗi
        }

        // POST: QuanLyXNK/KhoHang/Delete/{id}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var khoHang = await _context.KhoHangs.FindAsync(id);
            if (khoHang == null)
            {
                // Kho đã bị xóa bởi người khác hoặc ID không đúng
                TempData["ErrorMessage"] = "Không tìm thấy kho hàng để xóa.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var soLoTonKho = await _context.LoTonKhos.CountAsync(lo => lo.MaKho == id);
                var soPhieuXuat = await _context.PhieuXuats.CountAsync(px => px.MaKho == id);

                if (soLoTonKho > 0 || soPhieuXuat > 0)
                {
                    var lyDoRangBuoc = new List<string>();

                    if (soLoTonKho > 0)
                    {
                        lyDoRangBuoc.Add($"{soLoTonKho} lô tồn kho");
                    }

                    if (soPhieuXuat > 0)
                    {
                        lyDoRangBuoc.Add($"{soPhieuXuat} phiếu xuất");
                    }

                    TempData["ErrorMessage"] = $"Không thể xóa kho hàng '{khoHang.TenKho}' vì vẫn còn dữ liệu liên quan: {string.Join(", ", lyDoRangBuoc)}.";
                    return RedirectToAction(nameof(Index));
                }

                _context.KhoHangs.Remove(khoHang);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xóa kho hàng thành công.";
            }
            catch (DbUpdateException ex) // Xử lý lỗi liên quan đến ràng buộc CSDL nếu chưa kiểm tra ở trên
            {
                // Ghi log lỗi
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi xóa kho hàng. Có thể do ràng buộc dữ liệu.";
            }
            return RedirectToAction(nameof(Index));
        }
            
            private bool KhoHangExists(string id)
        {
            return _context.KhoHangs.Any(e => e.MaKho == id);
        }
    }
}