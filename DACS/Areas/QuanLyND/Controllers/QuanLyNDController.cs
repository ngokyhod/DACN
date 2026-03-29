// File: Areas/QuanlyND/Controllers/QuanLyNDController.cs
using DACS.Models; // Namespace chứa ApplicationUser
using DACS.Models.ViewModels; // Namespace chứa ViewModels vừa tạo
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore; // Cần cho ToListAsync, Include...
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DACS.Areas.QuanlyND.Controllers // Đảm bảo đúng namespace và Area
{
    [Area("QuanLyND")]
    [Authorize(Roles = SD.Role_Owner + "," + SD.Role_QuanLyND)] // Thêm phân quyền
    public class QuanLyNDController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager; // Inject RoleManager
        private readonly ILogger<QuanLyNDController> _logger;
        private readonly ApplicationDbContext _context;

        public QuanLyNDController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<QuanLyNDController> logger, ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _context = context;
        }


        // GET: QuanLyND/DuyetDoanhNghiep
        public async Task<IActionResult> DuyetDoanhNghiep()
        {
            var dsChoDuyet = await _context.KhachHangs
                .Where(kh => !string.IsNullOrEmpty(kh.TenDoanhNghiep) && kh.IsEnterpriseVerified == false)
                .ToListAsync();
            return View(dsChoDuyet);
        }

        [HttpPost]
        public async Task<IActionResult> XacNhanDoanhNghiep(string id)
        {
            var khachHang = await _context.KhachHangs.FindAsync(id);
            if (khachHang != null)
            {
                khachHang.IsEnterpriseVerified = true;
                _context.Update(khachHang);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xác nhận doanh nghiệp thành công!";
            }
            return RedirectToAction(nameof(DuyetDoanhNghiep));
        }

        [HttpPost]
        public async Task<IActionResult> TuChoiDoanhNghiep(string id)
        {
            var khachHang = await _context.KhachHangs.FindAsync(id);
            if (khachHang != null)
            {
                khachHang.TenDoanhNghiep = null;
                khachHang.LinhVucHoatDong = null;
                khachHang.NhuCauChinh = null;
                khachHang.IsEnterpriseVerified = false;
                _context.Update(khachHang);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã từ chối đăng ký doanh nghiệp!";
            }
            return RedirectToAction(nameof(DuyetDoanhNghiep));
        }
        // GET: QuanlyND/QuanlyND/Index
        public async Task<IActionResult> Index(
            string? searchTerm,
            string? roleFilter,
            string? statusFilter,
            int page = 1)
        {
            int pageSize = 10; // Số lượng user trên mỗi trang
            _logger.LogInformation("Loading User Management Index - Page: {Page}, Search: '{SearchTerm}', Role: '{RoleFilter}', Status: '{StatusFilter}'", page, searchTerm, roleFilter, statusFilter);

            // --- Query cơ sở ---
            var query = _userManager.Users.AsQueryable();

            // --- Áp dụng Filter ---
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string lowerSearch = searchTerm.ToLower().Trim();
                query = query.Where(u => (u.UserName != null && u.UserName.ToLower().Contains(lowerSearch)) ||
                                         (u.Email != null && u.Email.ToLower().Contains(lowerSearch)) ||
                                         (u.PhoneNumber != null && u.PhoneNumber.Contains(lowerSearch)) ||
                                         (u.FullName != null && u.FullName.ToLower().Contains(lowerSearch)));
            }

            if (!string.IsNullOrWhiteSpace(roleFilter) && roleFilter.ToLower() != "all")
            {
                var userIdsInRole = (await _userManager.GetUsersInRoleAsync(roleFilter)).Select(u => u.Id);
                query = query.Where(u => userIdsInRole.Contains(u.Id));
            }

            // Lưu ý: Lọc trạng thái phức tạp hơn nếu bạn không lưu trạng thái trực tiếp
            // Cách tiếp cận dựa trên EmailConfirmed và LockoutEnd
            if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter.ToLower() != "all")
            {
                switch (statusFilter.ToLower())
                {
                    case "active": // Hoạt động
                        query = query.Where(u => u.EmailConfirmed == true && (u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow));
                        break;
                    case "locked": // Bị khóa
                        query = query.Where(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow);
                        break;
                    case "pending": // Chờ xác minh
                        query = query.Where(u => u.EmailConfirmed == false);
                        break;
                }
            }

            // --- Tính toán Thống kê Server-Side ---
            // (Tính trên toàn bộ user để các thẻ stat không thay đổi khi lọc/phân trang)
            var allUsersQuery = _userManager.Users; // Query riêng cho stats
            var stats = new UserStatsViewModel
            {
                TotalUsers = await allUsersQuery.CountAsync(),
                ActiveUsers = await allUsersQuery.CountAsync(u => u.EmailConfirmed && (u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow)),
                PendingUsers = await allUsersQuery.CountAsync(u => !u.EmailConfirmed),
                LockedUsers = await allUsersQuery.CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow)
            };


            // --- Phân trang (Sau khi lọc) ---
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var pagedUsers = await query
                                .OrderBy(u => u.UserName)
                                .Skip((page - 1) * pageSize)
                                .Take(pageSize)
                                .ToListAsync();

            // --- Chuẩn bị ViewModel ---
            var userViewModels = new List<UserViewModel>();
            foreach (var user in pagedUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
                string status = isLocked ? "Bị khóa" : (!user.EmailConfirmed ? "Chờ xác minh" : "Hoạt động");

                // Cần có cột ngày tạo trong ApplicationUser để lấy chính xác
                // DateTimeOffset createdDate = user.CreatedDate; // Ví dụ
                DateTimeOffset createdDate = user.LockoutEnd ?? DateTimeOffset.MinValue; // Tạm thời, không chính xác!

                userViewModels.Add(new UserViewModel
                {
                    UserId = user.Id,
                    UserName = user.UserName ?? "N/A",
                    FullName = user.FullName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Roles = roles,
                    Status = status,
                    CreatedDate = createdDate, // Cần ngày tạo thực tế
                    IsLocked = isLocked,
                    EmailConfirmed = user.EmailConfirmed
                });
            }

            // --- Chuẩn bị Options cho Filter Dropdowns ---
            var roleOptions = await _roleManager.Roles
                                .OrderBy(r => r.Name)
                                .Select(r => new SelectListItem { Value = r.Name, Text = r.Name })
                                .ToListAsync();
            // Thêm tùy chọn "Tất cả" vào đầu
            roleOptions.Insert(0, new SelectListItem { Value = "all", Text = "--- Lọc theo vai trò ---" });

            var statusOptions = new List<SelectListItem>
            {
                 // Đổi value thành chữ thường để dễ xử lý
                new SelectListItem { Value = "all", Text = "--- Lọc theo trạng thái ---" },
                new SelectListItem { Value = "active", Text = "Hoạt động" },
                new SelectListItem { Value = "locked", Text = "Bị khóa" },
                new SelectListItem { Value = "pending", Text = "Chờ xác minh" }
            };


            var viewModel = new UserManagementViewModel
            {
                Users = userViewModels,
                Statistics = stats,
                SearchTerm = searchTerm,
                RoleFilter = roleFilter,
                StatusFilter = statusFilter,
                RoleOptions = roleOptions,
                StatusOptions = statusOptions,
                PageIndex = page,
                PageSize = pageSize, // Gán cả PageSize nếu cần
                TotalPages = totalPages,
                TotalItems = totalItems // *** GÁN GIÁ TRỊ totalItems VÀO ĐÂY ***
            };

            return View(viewModel);
        }


        // POST: QuanlyND/QuanlyND/ToggleLockout/USER_ID
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLockout(string userId) // Giữ nguyên action này
        {
            // ... (Code từ câu trả lời trước) ...
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "User ID không hợp lệ.";
                return RedirectToAction(nameof(Index), new { area = "QuanlyND" }); // Redirect đúng Area
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
                return RedirectToAction(nameof(Index), new { area = "QuanlyND" });
            }

            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == userId)
            {
                TempData["ErrorMessage"] = "Bạn không thể tự khóa tài khoản của mình.";
                return RedirectToAction(nameof(Index), new { area = "QuanlyND" });
            }

            IdentityResult result;
            string actionMessage;
            bool currentlyLocked = await _userManager.IsLockedOutAsync(user);

            if (currentlyLocked)
            {
                _logger.LogInformation("Mở khóa người dùng {UserId}", userId);
                result = await _userManager.SetLockoutEndDateAsync(user, null);
                if (result.Succeeded) { result = await _userManager.ResetAccessFailedCountAsync(user); }
                actionMessage = "mở khóa";
            }
            else
            {
                _logger.LogInformation("Khóa người dùng {UserId}", userId);
                if (!await _userManager.GetLockoutEnabledAsync(user)) { await _userManager.SetLockoutEnabledAsync(user, true); }
                result = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
                actionMessage = "khóa";
            }

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"Đã {actionMessage} tài khoản '{user.UserName}' thành công.";
                _logger.LogInformation("Thực hiện thành công {Action} cho người dùng {UserId}", actionMessage, userId);
            }
            else
            {
                string errors = string.Join(", ", result.Errors.Select(e => e.Description));
                TempData["ErrorMessage"] = $"Lỗi khi {actionMessage} tài khoản '{user.UserName}'. Lỗi: {errors}";
                _logger.LogError("Lỗi khi {Action} người dùng {UserId}. Lỗi: {Errors}", actionMessage, userId, errors);
            }

            return RedirectToAction(nameof(Index), new { area = "QuanlyND" }); // Redirect đúng Area
        }

        // Action để hiển thị form/modal tạo user
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var viewModel = new CreateUserViewModel
            {
                RoleOptions = await _roleManager.Roles
                                    .Select(r => new SelectListItem { Value = r.Name, Text = r.Name })
                                    .OrderBy(r => r.Text)
                                    .ToListAsync()
            };
            // Nếu dùng modal thì trả về PartialView
            // return PartialView("_CreateUserModalPartial", viewModel);
            // Nếu dùng trang riêng:
            return View(viewModel); // Cần tạo View Create.cshtml trong Areas/QuanlyND/Views/QuanlyND
        }


        // POST: QuanlyND/QuanlyND/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            // ... (Phần kiểm tra ModelState và PopulateRoleOptions) ...

            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.UserName,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    FullName = model.FullName,
                    // *** ĐẶT TRẠNG THÁI EMAIL CONFIRMED LÀ TRUE TẠI ĐÂY ***
                    EmailConfirmed = true, // <--- DÒNG QUAN TRỌNG
                    LockoutEnabled = true, // Nên bật sẵn sàng khóa nếu cần
                    // CreatedDate = DateTime.UtcNow // Nếu model ApplicationUser có trường này
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User {Username} created successfully. Adding role {Role}", model.UserName, model.Role);
                    var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
                    // ... (Xử lý kết quả thêm vai trò và Redirect) ...
                }
                else // Nếu CreateAsync thất bại
                {
    
                }
            }
            return View(model); // Hoặc PartialView
        }


        // --- CÁC ACTION KHÁC (Placeholder - Cần triển khai logic) ---

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmEmail(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();
            if (user.EmailConfirmed)
            {
                TempData["InfoMessage"] = "Email này đã được xác minh.";
                return RedirectToAction(nameof(Index), new { area = "QuanlyND" });
            }
            // Cần tạo và gửi token xác nhận, ở đây làm đơn giản là set cờ true
            user.EmailConfirmed = true;
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"Đã xác minh email cho {user.UserName}.";
            }
            else
            {
                TempData["ErrorMessage"] = "Lỗi khi xác minh email.";
            }

            return RedirectToAction(nameof(Index), new { area = "QuanlyND" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                // Ngăn xóa chính mình
                if (_userManager.GetUserId(User) == userId)
                {
                    TempData["ErrorMessage"] = "Không thể xóa tài khoản của chính bạn.";
                    return RedirectToAction(nameof(Index), new { area = "QuanlyND" });
                }

                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded) { TempData["SuccessMessage"] = $"Đã xóa tài khoản '{user.UserName}'."; }
                else { TempData["ErrorMessage"] = $"Lỗi khi xóa tài khoản '{user.UserName}'."; }
            }
            else { TempData["ErrorMessage"] = "Không tìm thấy người dùng để xóa."; }
            return RedirectToAction(nameof(Index), new { area = "QuanlyND" });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string userId)
        {
            TempData["InfoMessage"] = "Chức năng Sửa đang được phát triển.";
            return RedirectToAction(nameof(Index), new { area = "QuanlyND" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            TempData["InfoMessage"] = "Chức năng Sửa đang được phát triển.";
            return RedirectToAction(nameof(Index), new { area = "QuanlyND" });
        }

        // Các actions khác trong Area QuanlyND
        public IActionResult QuanLyPhanHoi() { return View(); }
        public IActionResult QuanLyHoTro() { return View(); }

    }
}