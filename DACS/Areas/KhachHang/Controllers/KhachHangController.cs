using System;
using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using DACS.Models;
using DACS.Models.ViewModels;
using DACS.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
namespace DACS.Areas.KhachHang.Controllers

{
    [Area("KhachHang")]
    [Authorize(Roles = SD.Role_KhachHang)]

    public class KhachHangController : Controller

    {

        private readonly ApplicationDbContext _context; // <<< Giữ lại DbContext để truy vấn DonHang (trừ khi bạn tạo OrderRepository)
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly INguoiMuaRepository _nguoiMuaRepo; // <<< Inject Repository NguoiMua
        private readonly ILogger<KhachHangController> _logger;
        private readonly ISanPhamRepository _sanPhamRepo; // <<< Inject Repository NguoiMu
        private readonly IThuGomRepository _thuGomRepository;
        public KhachHangController(
      ApplicationDbContext context, // <<< Thêm lại tham số context
         UserManager<ApplicationUser> userManager,
      IWebHostEnvironment webHostEnvironment,
      INguoiMuaRepository nguoiMuaRepository,
      ILogger<KhachHangController> logger,
      ISanPhamRepository sanPhamRepo,
      IThuGomRepository thuGomRepository
      )
        {
            _context = context; // <<< Gán lại context
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
            _nguoiMuaRepo = nguoiMuaRepository;
            _logger = logger;
            _sanPhamRepo = sanPhamRepo;
            _thuGomRepository = thuGomRepository;
        }

        public async Task<IActionResult> Index()

        {
            var userId = _userManager.GetUserId(User);
            if (userId == null)
            {
                return Challenge(); // Chưa đăng nhập
            }

            // Tạo ViewModel để chứa dữ liệu
            var dashboardViewModel = new KhachHangDashboardViewModel(); // Renamed variable to avoid conflict
            var khachHang = await _context.KhachHangs.FirstOrDefaultAsync(kh => kh.UserId == userId);

            var nguoiMuaProfile = await _context.KhachHangs
                                     .Include(kh => kh.TinhThanhPho) // Tải TinhThanhPho
                                     .Include(kh => kh.QuanHuyen)    // Tải QuanHuyen
                                     .Include(kh => kh.XaPhuong)     // Tải XaPhuong
                                     .AsNoTracking() // Vẫn có thể dùng AsNoTracking nếu chỉ đọc
                                     .FirstOrDefaultAsync(kh => kh.UserId == userId);

            if (nguoiMuaProfile != null)
            {
                dashboardViewModel.TenNguoiMua = nguoiMuaProfile.Ten_KhachHang;
                dashboardViewModel.EmailNguoiMua = nguoiMuaProfile.Email_KhachHang; // Email liên hệ
                dashboardViewModel.SdtNguoiMua = nguoiMuaProfile.SDT_KhachHang;
                // Lấy địa chỉ mặc định (ví dụ lấy từ DiaChi_NguoiMua, hoặc từ bảng Sổ Địa Chỉ nếu có)
                List<string> addressParts = new List<string>();
                // Giữ lại phần đường/ấp/thôn vì nó vẫn là string trực tiếp
                if (!string.IsNullOrWhiteSpace(nguoiMuaProfile.DiaChi_DuongApThon))
                {
                    addressParts.Add(nguoiMuaProfile.DiaChi_DuongApThon);
                }

                // SỬA LẠI: Lấy tên từ navigation property XaPhuong
                // Kiểm tra cả nguoiMuaProfile.XaPhuong và nguoiMuaProfile.XaPhuong.TenXa
                string? tenXa = nguoiMuaProfile.XaPhuong?.TenXa; // Giả sử tên thuộc tính là TenXa
                if (!string.IsNullOrWhiteSpace(tenXa))
                {
                    addressParts.Add(tenXa);
                }

                // SỬA LẠI: Lấy tên từ navigation property QuanHuyen
                // Kiểm tra cả nguoiMuaProfile.QuanHuyen và nguoiMuaProfile.QuanHuyen.TenQuan
                string? tenQuan = nguoiMuaProfile.QuanHuyen?.TenQuan; // Giả sử tên thuộc tính là TenQuan
                if (!string.IsNullOrWhiteSpace(tenQuan))
                {
                    addressParts.Add(tenQuan);
                }

                // SỬA LẠI: Lấy tên từ navigation property TinhThanhPho
                // Kiểm tra cả nguoiMuaProfile.TinhThanhPho và nguoiMuaProfile.TinhThanhPho.TenTinh
                string? tenTinh = nguoiMuaProfile.TinhThanhPho?.TenTinh; // Giả sử tên thuộc tính là TenTinh
                if (!string.IsNullOrWhiteSpace(tenTinh))
                {
                    addressParts.Add(tenTinh);
                }

                // Phần Join giữ nguyên
                if (addressParts.Any())
                {
                    dashboardViewModel.DiaChiMacDinh = string.Join(", ", addressParts);
                }
                else
                {
                    dashboardViewModel.DiaChiMacDinh = "Chưa cập nhật địa chỉ";
                }
            }
            else
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)

                {

                    dashboardViewModel.TenNguoiMua = user.FullName ?? user.UserName;

                    dashboardViewModel.EmailNguoiMua = user.Email; // Email đăng nhập

                    dashboardViewModel.SdtNguoiMua = user.PhoneNumber;

                    dashboardViewModel.DiaChiMacDinh = user.Address ?? "Chưa cập nhật";

                }

            }

            // 2. Lấy danh sách đơn hàng gần đây

            var recentOrdersData = await _context.ChiTietDatHangs

                .Where(dh => dh.KhachHang.UserId == userId)

                .OrderByDescending(dh => dh.NgayTao)

                .Take(3)

                .Select(dh => new OrderSummaryViewModel

                {

                    OrderId = dh.M_CTDatHang,

                    OrderDate = (DateTime)dh.NgayTao,

                    Status = dh.TrangThaiDonHang,

                    TotalAmount = dh.TongTien

                })

                .ToListAsync();



            // 3. Lấy đơn thu gom gần đây (nếu có)
            List<YeuCauThuGom> recentCollectionRequestsData = new List<YeuCauThuGom>(); // Initialize empty
            if (khachHang != null) // Check if khachHang was found
            {
                recentCollectionRequestsData = await _context.YeuCauThuGoms
                    .Where(yc => yc.M_KhachHang == khachHang.M_KhachHang) // Now safe to access M_KhachHang
                    .Include(yc => yc.ChiTietThuGoms)
                        .ThenInclude(ct => ct.LoaiSanPham) // Corrected property name? Check LoaiSanPham model
                    .OrderByDescending(yc => yc.NgayYeuCau)
                    .Take(5)
                    .ToListAsync();
            }

            // Assign data to the ViewModel

            dashboardViewModel.RecentOrders = recentOrdersData;

            dashboardViewModel.RecentCollectionRequests = recentCollectionRequestsData.ToList();

            // 4. Truyền ViewModel tới View Index.cshtml
            return View(dashboardViewModel);

        }

        // GET: /KhachHang/KhachHang/HoSoCaNhan
        public async Task<IActionResult> HoSoCaNhan()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) { return Challenge(); } // Đảm bảo người dùng đăng nhập

            // Lấy hồ sơ từ Repo (Giả sử ban đầu không Include)
            var nguoiMuaProfileBasic = await _nguoiMuaRepo.GetByUserIdAsync(userId);

            // --- Tùy chọn: Tự động tạo hồ sơ nếu chưa có ---
            if (nguoiMuaProfileBasic == null)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null) { return NotFound("Lỗi: Không tìm thấy tài khoản người dùng."); }

                nguoiMuaProfileBasic = new Models.KhachHang
                {
                    M_KhachHang = Guid.NewGuid().ToString("N").Substring(0, 10),
                    UserId = userId,
                    Ten_KhachHang = user.FullName ?? user.UserName ?? "Chưa đặt tên",
                    Email_KhachHang = user.Email ?? "Chưa có email",
                    SDT_KhachHang = user.PhoneNumber ?? "Chưa có SĐT",
                    MaTinh = "T00",
                    MaQuan = "Q0100",
                    MaXa = "X010100",
                    DiaChi_DuongApThon = "chua cap nhat"
                };

                try
                {
                    await _nguoiMuaRepo.AddAsync(nguoiMuaProfileBasic);
                    await _nguoiMuaRepo.SaveChangesAsync();
                    _logger.LogInformation($"Auto-created NguoiMua profile for User ID: {userId}");
                    TempData["InfoMessage"] = "Hồ sơ của bạn đã được khởi tạo. Vui lòng cập nhật thông tin chi tiết.";
                    // nguoiMuaProfileBasic bây giờ đã có ID nhưng chưa có thông tin địa chỉ đi kèm
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error auto-creating NguoiMua profile for User ID: {userId}");
                    TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tạo hồ sơ Người mua của bạn.";
                    return View("HoSoCaNhan", null); // Trả về view lỗi
                }
            }
            // --- Kết thúc Tùy chọn tự động tạo ---

            // === BẮT BUỘC: Truy vấn lại hoặc truy vấn ban đầu với Include để lấy tên Tỉnh/Huyện/Xã ===
            // Sử dụng _context trực tiếp hoặc đảm bảo Repo có phương thức hỗ trợ Include
            var nguoiMuaProfile = await _context.KhachHangs // <-- Dùng DbContext hoặc sửa Repo
                                          .Include(kh => kh.TinhThanhPho) // <<< THÊM INCLUDE
                                          .Include(kh => kh.QuanHuyen)    // <<< THÊM INCLUDE
                                          .Include(kh => kh.XaPhuong)     // <<< THÊM INCLUDE
                                          .AsNoTracking() // Dùng AsNoTracking vì chỉ để hiển thị
                                          .FirstOrDefaultAsync(kh => kh.UserId == userId); // Lấy theo UserId

            // Nếu không tìm thấy hồ sơ (kể cả sau khi cố tạo) -> báo lỗi
            if (nguoiMuaProfile == null)
            {
                // Có thể TempData đã được set ở phần tạo lỗi
                if (TempData["ErrorMessage"] == null)
                {
                    TempData["ErrorMessage"] = "Không thể tải thông tin hồ sơ Người mua.";
                }
                return View("HoSoCaNhan", null); // Trả về View với model null
            }

            // --- Xây dựng chuỗi địa chỉ đầy đủ ---
            var addressParts = new List<string>();
            // Thêm đường/ấp/thôn trước nếu có
            if (!string.IsNullOrWhiteSpace(nguoiMuaProfile.DiaChi_DuongApThon) && nguoiMuaProfile.DiaChi_DuongApThon != "Chưa cập nhật")
            {
                addressParts.Add(nguoiMuaProfile.DiaChi_DuongApThon);
            }
            // Thêm tên Xã (dùng ?. để kiểm tra null an toàn)
            if (nguoiMuaProfile.XaPhuong?.TenXa != null)
            {
                addressParts.Add(nguoiMuaProfile.XaPhuong.TenXa);
            }
            // Thêm tên Huyện (dùng ?. để kiểm tra null an toàn)
            if (nguoiMuaProfile.QuanHuyen?.TenQuan != null)
            {
                addressParts.Add(nguoiMuaProfile.QuanHuyen.TenQuan);
            }
            // Thêm tên Tỉnh (dùng ?. để kiểm tra null an toàn)
            if (nguoiMuaProfile.TinhThanhPho?.TenTinh != null)
            {
                addressParts.Add(nguoiMuaProfile.TinhThanhPho.TenTinh);
            }
            // Ghép các phần lại, nếu không có phần nào thì hiển thị thông báo
            string fullAddress = addressParts.Any() ? string.Join(", ", addressParts) : "Chưa cập nhật địa chỉ";
            // ------------------------------------


            // Map dữ liệu từ NguoiMua (đã Include) sang ViewModel để hiển thị
            var viewModel = new EditNguoiMuaProfile
            {
                Ten_NguoiMua = nguoiMuaProfile.Ten_KhachHang,
                Email_NguoiMua = nguoiMuaProfile.Email_KhachHang,
                SDT_NguoiMua = nguoiMuaProfile.SDT_KhachHang,
                Edit_DiaChi_TinhTP = nguoiMuaProfile.MaTinh,
                Edit_DiaChi_QuanHuyen = nguoiMuaProfile.MaQuan,
                Edit_DiaChi_XaPhuong = nguoiMuaProfile.MaXa,
                Edit_DiaChi_DuongApThon = nguoiMuaProfile.DiaChi_DuongApThon,
                Gender = nguoiMuaProfile.Gender,
                AvatarUrl = nguoiMuaProfile.AvatarUrl,

                // <<< GÁN CHUỖI ĐỊA CHỈ ĐÃ TẠO VÀO VIEWMODEL >>>
                HienThiDiaChiDayDu = fullAddress
            };

            return View("HoSoCaNhan", viewModel); // Truyền viewModel đã được chuẩn bị đầy đủ
        }

        // GET: /KhachHang/KhachHang/Edit
        public async Task<IActionResult> Edit()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) { return Challenge(); }
            var nguoiMuaProfile = await _nguoiMuaRepo.GetByUserIdAsync(userId);

            // Nếu không tìm thấy, chuyển hướng về HoSoCaNhan
            // HoSoCaNhan sẽ xử lý việc hiển thị lỗi hoặc tự động tạo hồ sơ
            if (nguoiMuaProfile == null)
            {
                TempData["InfoMessage"] = "Không tìm thấy hồ sơ, đang kiểm tra..."; // Thông báo nhẹ nhàng
               return RedirectToAction(nameof(HoSoCaNhan));
            }

            // Map sang ViewModel để hiển thị form Edit
            var viewModel = new EditNguoiMuaProfile
            {
                // Gán các thuộc tính từ nguoiMuaProfile sang viewModel
                Ten_NguoiMua = nguoiMuaProfile.Ten_KhachHang,
                Email_NguoiMua = nguoiMuaProfile.Email_KhachHang,
                SDT_NguoiMua = nguoiMuaProfile.SDT_KhachHang,
                Edit_DiaChi_TinhTP = nguoiMuaProfile.MaTinh,
                Edit_DiaChi_QuanHuyen = nguoiMuaProfile.MaQuan,
                Edit_DiaChi_XaPhuong = nguoiMuaProfile.MaXa,
                Edit_DiaChi_DuongApThon = nguoiMuaProfile.DiaChi_DuongApThon,
                Gender = nguoiMuaProfile.Gender,
                AvatarUrl = nguoiMuaProfile.AvatarUrl
            };
            var provinces = await _context.TinhThanhPhos.OrderBy(p => p.TenTinh).ToListAsync();
            // Truyền MaTinh hiện tại của người dùng làm selected value
            ViewBag.ProvinceOptions = new SelectList(provinces, "MaTinh", "TenTinh", viewModel.Edit_DiaChi_TinhTP);
            // 2. Tải Quận/Huyện DỰA VÀO Tỉnh/TP hiện tại (nếu có)
            if (!string.IsNullOrEmpty(viewModel.Edit_DiaChi_TinhTP))
            {
                var districts = await _context.QuanHuyens
                                        .Where(q => q.MaTinh == viewModel.Edit_DiaChi_TinhTP)
                                        .OrderBy(q => q.TenQuan)
                                        .ToListAsync();
                // Truyền MaQuan hiện tại của người dùng làm selected value
                ViewBag.DistrictOptions = new SelectList(districts, "MaQuan", "TenQuan", viewModel.Edit_DiaChi_QuanHuyen);
            }
            else
            {
                ViewBag.DistrictOptions = new SelectList(Enumerable.Empty<SelectListItem>(), "MaQuan", "TenQuan");
            }

            if (!string.IsNullOrEmpty(viewModel.Edit_DiaChi_QuanHuyen))
            {
                var wards = await _context.XaPhuongs
                                      .Where(w => w.MaQuan == viewModel.Edit_DiaChi_QuanHuyen)
                                      .OrderBy(w => w.TenXa)
                                      .ToListAsync();
                // Truyền MaXa hiện tại của người dùng làm selected value
                ViewBag.WardOptions = new SelectList(wards, "MaXa", "TenXa", viewModel.Edit_DiaChi_XaPhuong);
            }
            else
            {
                ViewBag.WardOptions = new SelectList(Enumerable.Empty<SelectListItem>(), "MaXa", "TenXa");
            }


            return View("Edit", viewModel); // Truyền ViewModel tới View Edit.cshtml

        }

        [HttpGet]
        public async Task<IActionResult> GetDistricts(string provinceId) // Tên tham số "provinceId" phải khớp với data gửi từ AJAX
        {
            if (string.IsNullOrEmpty(provinceId))
            {
                return Json(new List<object>()); // Trả về danh sách rỗng nếu không có provinceId
            }

            var districts = await _context.QuanHuyens // Giả sử _context là ApplicationDbContext của bạn
                                          .Where(q => q.MaTinh == provinceId)
                                          .OrderBy(q => q.TenQuan)
                                          .Select(q => new { value = q.MaQuan, text = q.TenQuan }) // Quan trọng: trả về value và text
                                          .ToListAsync();
            return Json(districts);
        }

        [HttpGet]
        public async Task<IActionResult> GetWards(string districtId) // Tên tham số "districtId" phải khớp
        {
            if (string.IsNullOrEmpty(districtId))
            {
                return Json(new List<object>()); // Trả về danh sách rỗng
            }

            var wards = await _context.XaPhuongs // Giả sử _context là ApplicationDbContext của bạn
                                      .Where(w => w.MaQuan == districtId)
                                      .OrderBy(w => w.TenXa)
                                      .Select(w => new { value = w.MaXa, text = w.TenXa }) // Quan trọng: trả về value và text
                                      .ToListAsync();
            return Json(wards);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditNguoiMuaProfile model) // Giả sử ViewModel tên là EditNguoiMuaProfile
        {
            if (!ModelState.IsValid)
            {
                // Load lại AvatarUrl nếu validation fail (logic này có vẻ ổn)
                var userIdForUrl = _userManager.GetUserId(User);
                var profileForUrl = await _nguoiMuaRepo.GetByUserIdAsync(userIdForUrl);
                if (profileForUrl != null) { model.AvatarUrl = profileForUrl.AvatarUrl; }
                return View("Edit", model);
            }
            var userId = _userManager.GetUserId(User);
            var nguoiMuaProfile = await _nguoiMuaRepo.GetByUserIdAsync(userId); // Lấy entity từ Repo
            if (nguoiMuaProfile == null)
            {
                TempData["ErrorMessage"] = "Lỗi: Không tìm thấy hồ sơ để cập nhật.";
                return RedirectToAction(nameof(HoSoCaNhan)); // Hoặc trang lỗi
            }

            // --- Cập nhật entity NguoiMua ---
            bool hasChanges = false; // Biến kiểm tra xem có thay đổi thực sự không
            if (nguoiMuaProfile.Ten_KhachHang != model.Ten_NguoiMua) { nguoiMuaProfile.Ten_KhachHang = model.Ten_NguoiMua; hasChanges = true; }
            if (nguoiMuaProfile.Email_KhachHang != model.Email_NguoiMua) { nguoiMuaProfile.Email_KhachHang = model.Email_NguoiMua; hasChanges = true; }
            if (nguoiMuaProfile.SDT_KhachHang != model.SDT_NguoiMua) { nguoiMuaProfile.SDT_KhachHang = model.SDT_NguoiMua; hasChanges = true; }
            if (nguoiMuaProfile.MaTinh != model.Edit_DiaChi_TinhTP) { nguoiMuaProfile.MaTinh = model.Edit_DiaChi_TinhTP; hasChanges = true; }
            if (nguoiMuaProfile.MaQuan != model.Edit_DiaChi_QuanHuyen) { nguoiMuaProfile.MaQuan = model.Edit_DiaChi_QuanHuyen; hasChanges = true; }
            if (nguoiMuaProfile.MaXa != model.Edit_DiaChi_XaPhuong) { nguoiMuaProfile.MaXa = model.Edit_DiaChi_XaPhuong; hasChanges = true; }
            if (nguoiMuaProfile.DiaChi_DuongApThon != model.Edit_DiaChi_DuongApThon) { nguoiMuaProfile.DiaChi_DuongApThon = model.Edit_DiaChi_DuongApThon; hasChanges = true; }
            if (nguoiMuaProfile.Gender != model.Gender) { nguoiMuaProfile.Gender = model.Gender; hasChanges = true; }

            // --- Xử lý upload ảnh ---
            string? oldImagePath = nguoiMuaProfile.AvatarUrl; // Lưu đường dẫn cũ
            if (model.ProfileImageFile != null && model.ProfileImageFile.Length > 0)
            {
                // Thêm kiểm tra kiểu file và kích thước nếu cần
                if (model.ProfileImageFile.Length > 2 * 1024 * 1024) // Ví dụ giới hạn 2MB
                {
                    ModelState.AddModelError("ProfileImageFile", "Kích thước ảnh không được vượt quá 2MB.");
                    // Không gán lại AvatarUrl vì cần giữ ảnh cũ khi có lỗi
                    return View("Edit", model);
                }
                // Kiểm tra kiểu file MIME Type nếu cần
                try
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "avatars");
                    var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ProfileImageFile.FileName); // Thêm phần mở rộng fil
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    Directory.CreateDirectory(uploadsFolder); // Tạo thư mục nếu chưa có
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                   {
                        await model.ProfileImageFile.CopyToAsync(fileStream);
                    }
                    var newAvatarUrl = "/images/avatars/" + uniqueFileName; // Đường dẫn lưu trong DB
                    if (nguoiMuaProfile.AvatarUrl != newAvatarUrl)
                    {
                        nguoiMuaProfile.AvatarUrl = newAvatarUrl;
                        hasChanges = true;
                        // Xóa ảnh cũ sau khi đã chắc chắn lưu ảnh mới thành công (hoặc sau khi SaveChanges)
                        if (!string.IsNullOrEmpty(oldImagePath))
                        {
                            string oldPhysicalPath = Path.Combine(_webHostEnvironment.WebRootPath, oldImagePath.TrimStart('/'));
                            if (System.IO.File.Exists(oldPhysicalPath))
                            {
                                try { System.IO.File.Delete(oldPhysicalPath); } catch (Exception ex) { _logger.LogError(ex, $"Error deleting old avatar: {oldPhysicalPath}"); } // Ghi log nếu xóa lỗi
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading profile image."); // Ghi log
                    ModelState.AddModelError("ProfileImageFile", "Lỗi xảy ra khi tải ảnh lên.");
                    model.AvatarUrl = oldImagePath; // Giữ lại ảnh cũ khi có lỗi upload
                    return View("Edit", model);
                }
            }
            // Chỉ lưu nếu thực sự có thay đổi
            if (hasChanges)
            {
                try
                {
                    _nguoiMuaRepo.Update(nguoiMuaProfile); // Đánh dấu Update
                    await _nguoiMuaRepo.SaveChangesAsync(); // Lưu thay đổi vào DB
                    TempData["SuccessMessage"] = "Cập nhật hồ sơ thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    TempData["ErrorMessage"] = "Lỗi: Dữ liệu đã bị thay đổi bởi người khác. Vui lòng tải lại trang và thử lại.";
                    model.AvatarUrl = oldImagePath; // Giữ ảnh cũ
                    return View("Edit", model);
                }
                catch (Exception ex) // Bắt lỗi chung hoặc DbUpdateException
                {
                    TempData["ErrorMessage"] = "Có lỗi xảy ra khi cập nhật hồ sơ.";
                    _logger.LogError(ex, "Error updating NguoiMua profile."); // Ghi log lỗi
                    model.AvatarUrl = oldImagePath; // Giữ ảnh cũ
                    return View("Edit", model);
                }
            }
            else
            {
                TempData["InfoMessage"] = "Không có thông tin nào được thay đổi.";
            }
            return RedirectToAction(nameof(HoSoCaNhan)); // Chuyển về trang hiển thị
        }



        public async Task<IActionResult> LichSuDonHang(string statusFilter, string timeFilter, int page = 1)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null)
            {
                return Challenge(); // Chưa đăng nhập
            }

            var query = _context.ChiTietDatHangs
                                .Where(dh => dh.KhachHang.UserId == userId); // Lọc theo User ID

            // 1. Lọc theo trạng thái (giữ nguyên logic của bạn)
            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "Tất cả trạng thái")
            {
                string dbStatus = statusFilter switch
                {
                    "pending" => "Đang xử lý",
                    "shipping" => "Đang giao hàng",
                    "completed" => "Đã giao",
                    "cancelled" => "Đã hủy",
                    _ => null
                };
                if (dbStatus != null)
                {
                    query = query.Where(dh => dh.TrangThaiDonHang == dbStatus);
                }
            }

            // 2. Lọc theo thời gian (giữ nguyên logic của bạn)
            DateTime? startDate = null;
            switch (timeFilter)
            {
                case "3m": startDate = DateTime.Now.AddMonths(-3); break;
                case "6m": startDate = DateTime.Now.AddMonths(-6); break;
                case "1y": startDate = DateTime.Now.AddYears(-1); break;
            }
            if (startDate.HasValue)
            {
                query = query.Where(dh => dh.NgayTao >= startDate.Value);
            }

            // 3. Sắp xếp (Mới nhất lên đầu)
            query = query.OrderByDescending(dh => dh.NgayTao);

            var ordersData = await query
                .Select(dh => new OrderSummaryViewModel
                {
                    OrderId = dh.M_DonHang,      // Mã chi tiết đặt hàng (hoặc mã đơn hàng chính nếu có)
                    OrderDate = dh.NgayTao,        // << THÊM HOẶC SỬA: Lấy ngày tạo/ngày đặt
                    TotalAmount = (Decimal)dh.TongTien,     // Tổng tiền của chi tiết này
                    Status = dh.TrangThaiDonHang,
                    KhoiLuong = (int)(float)dh.Khoiluong
                })
                .ToListAsync();

            // 5. Tạo ViewModel cho View
            var viewModel = new OrderHistoryViewModel
            {
                Orders = ordersData,
                CurrentStatusFilter = statusFilter,
                CurrentTimeFilter = timeFilter
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Details(string id)

        {

            if (id == null) return NotFound();

            // Use the repository to get data including related entities

            var sanPham = await _sanPhamRepo.GetByIdAsync(id); // Assumes GetByIdAsync includes LoaiSP, DonViTinh, KhoLuuTru

            // If GetByIdAsync doesn't include them, need to adjust the repo method or load them here if needed for display

            if (sanPham == null) return NotFound();

            return View(sanPham);

        }



        [HttpGet]

        public IActionResult ThuGom() // Đổi tên Controller nếu cần (ví dụ: thành ThuGomController)

        {

            // ... có thể load dropdown từ DB nếu cần ...

            return View(new ThuGomViewModel());

        }



        [HttpPost]

        [ValidateAntiForgeryToken]

        public async Task<IActionResult> ThuGom(ThuGomViewModel model) // Đổi tên Controller nếu cần

        {

            if (!ModelState.IsValid)

            {

                _logger.LogWarning("Dữ liệu gửi lên không hợp lệ.");

                return View(model);

            }



            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))

            {

                ModelState.AddModelError("", "Không thể xác định người dùng.");

                return View(model);

            }



            var khachHang = await _context.KhachHangs.FirstOrDefaultAsync(kh => kh.UserId == userId);

            if (khachHang == null)

            {

                _logger.LogError($"Không tìm thấy KhachHang cho UserId: {userId}");

                TempData["ErrorMessage"] = "Không tìm thấy thông tin khách hàng liên kết.";

                return RedirectToAction("Index");

            }



            // Transaction vẫn quản lý ở đây (Controller hoặc Service layer)

            using var transaction = await _context.Database.BeginTransactionAsync();

            try

            {

                // Tạo YeuCauThuGom và ChiTietThuGom (logic như trước)

                var yeuCau = new YeuCauThuGom

                {

                    M_YeuCau = GenerateRequestCode(), // Hàm tạo mã (có thể chuyển vào service)

                    M_KhachHang = khachHang.M_KhachHang,

                    NgayYeuCau = DateTime.UtcNow,

                    ThoiGianSanSang = model.PickupReadyTime.Value,

                    GhiChu = model.SupplierNotes,

                    TrangThai = "Chờ xử lý"

                };



                // Xử lý ảnh (logic như trước)

                List<string> savedImagePaths = await ProcessUploadedImages(model.ByproductImages, yeuCau.M_YeuCau);

                if (savedImagePaths == null) // Nếu có lỗi upload ảnh trong hàm helper

                {

                    await transaction.RollbackAsync();

                    ModelState.AddModelError("ByproductImages", "Đã xảy ra lỗi khi tải lên hình ảnh.");

                    return View(model);

                }





                var chiTiet = new ChiTietThuGom

                {

                    M_LoaiSP = model.M_LoaiSP,

                    M_DonViTinh = model.ByproductUnit,

                    SoLuong = (int)model.ByproductQuantity.Value,

                    MoTa = model.ByproductDescription,

                    GiaTriMongMuon = model.ByproductValue,

                    DacTinh_CongKenh = model.CharBulky,

                    DacTinh_AmUot = model.CharWet,

                    DacTinh_Kho = model.CharDry,

                    DacTinh_DoAmCao = model.CharMoisture,

                    DacTinh_TapChat = model.CharImpure,

                    DacTinh_DaXuLy = model.CharProcessed,

                    DanhSachHinhAnh = savedImagePaths.Any() ? string.Join(";", savedImagePaths) : null,



                };



                yeuCau.ChiTietThuGoms.Add(chiTiet);



                // --- SỬ DỤNG REPOSITORY ĐỂ ADD ---

                await _thuGomRepository.AddAsync(yeuCau);

                //----------------------------------



                // --- LƯU THAY ĐỔI QUA DBCONTEXT ---

                // Repository chỉ chuẩn bị, controller/service quyết định lưu cuối cùng

                await _context.SaveChangesAsync();

                //------------------------------------



                await transaction.CommitAsync();



                _logger.LogInformation($"Đã tạo yêu cầu thu gom {yeuCau.M_YeuCau} bởi khách hàng {khachHang.M_KhachHang} thông qua Repository.");

                TempData["SuccessMessage"] = $"Yêu cầu thu gom ({yeuCau.M_YeuCau}) của bạn đã được gửi thành công!";

                return RedirectToAction("Index");



            }

            catch (Exception ex) // Bắt lỗi chung hoặc DbUpdateException cụ thể hơn

            {

                await transaction.RollbackAsync();

                _logger.LogError(ex, "Lỗi khi xử lý POST ThuGom.");

                ModelState.AddModelError("", "Đã xảy ra lỗi khi tạo yêu cầu. Vui lòng thử lại.");

                return View(model);

            }

        }



        // --- Action LichSuYeuCauThuGom (Ví dụ cập nhật) ---

        public async Task<IActionResult> LichSuYeuCauThuGom()

        {

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var khachHang = await _context.KhachHangs.FirstOrDefaultAsync(kh => kh.UserId == userId);

            if (khachHang == null) return Challenge();



            // --- SỬ DỤNG REPOSITORY ĐỂ LẤY DỮ LIỆU ---

            var allRequests = await _thuGomRepository.GetAllForKhachHangAsync(khachHang.M_KhachHang);

            //------------------------------------------



            return View("LichSuYeuCauThuGom", allRequests);

        }





        // Hàm helper xử lý ảnh (tách ra cho gọn)

        private async Task<List<string>?> ProcessUploadedImages(List<IFormFile>? images, string requestCode)

        {

            if (images == null || images.Count == 0)

            {

                return new List<string>(); // Trả về list rỗng nếu không có ảnh

            }



            List<string> savedImagePaths = new List<string>();

            string requestUploadFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "thugom", requestCode);

            Directory.CreateDirectory(requestUploadFolder);



            foreach (var file in images)

            {

                if (file.Length > 0 && file.ContentType.StartsWith("image/")) // Chỉ xử lý file ảnh

                {

                    string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName); // Dùng extension gốc

                    string filePath = Path.Combine(requestUploadFolder, uniqueFileName);

                    try

                    {

                        using (var fileStream = new FileStream(filePath, FileMode.Create))

                        {

                            await file.CopyToAsync(fileStream);

                        }

                        savedImagePaths.Add(Path.Combine("/uploads/thugom", requestCode, uniqueFileName).Replace("\\", "/"));

                    }

                    catch (Exception ex)

                    {

                        _logger.LogError(ex, $"Lỗi khi lưu file ảnh: {file.FileName} cho yêu cầu {requestCode}");

                        // Nếu muốn dừng ngay khi có lỗi 1 file: return null;

                        // Nếu muốn bỏ qua file lỗi và tiếp tục: // continue; (và không add vào savedImagePaths)

                        return null; // Trả về null để báo lỗi cho action gọi nó

                    }

                }

            }

            return savedImagePaths;

        }



        // GET: /KhachHang/KhachHang/YeuCauThuGomDetails/{id}

        public async Task<IActionResult> YeuCauThuGomDetails(string id) // id là M_YeuCau

        {

            if (string.IsNullOrEmpty(id))

            {

                return NotFound("Mã yêu cầu không được để trống.");

            }



            var userId = _userManager.GetUserId(User);

            if (userId == null) return Challenge(); // Chưa đăng nhập



            var khachHang = await _context.KhachHangs.AsNoTracking()

                   .FirstOrDefaultAsync(kh => kh.UserId == userId);

            if (khachHang == null)

            {

                // Có thể không cần thiết nếu mọi user đăng nhập đều có KhachHang

                TempData["ErrorMessage"] = "Không tìm thấy hồ sơ khách hàng.";

                return RedirectToAction("Index", "Home", new { area = "" });

            }



            // Lấy yêu cầu bằng Repository (cần đảm bảo repo method include đủ dữ liệu)

            // Giả sử GetByIdAsync include ChiTietThuGoms -> LoaiSanPham và DonViTinh

            var yeuCau = await _thuGomRepository.GetByIdAsync(id, includeDetails: true);



            // Kiểm tra yêu cầu tồn tại và thuộc về đúng khách hàng này

            if (yeuCau == null || yeuCau.M_KhachHang != khachHang.M_KhachHang)

            {

                _logger.LogWarning($"User {userId} attempted to access unauthorized request {id}");

                return NotFound("Không tìm thấy yêu cầu hoặc bạn không có quyền xem yêu cầu này.");

            }



            // Trả về View chi tiết (cần tạo View này)

            return View("YeuCauThuGomDetails", yeuCau); // Truyền đối tượng YeuCauThuGom vào View

        }



        public async Task<IActionResult> YeuCauThuGomCancel(string id)

        {

            if (string.IsNullOrEmpty(id)) return NotFound();

            var userId = _userManager.GetUserId(User);

            if (userId == null) return Challenge();

            var khachHang = await _context.KhachHangs.AsNoTracking().FirstOrDefaultAsync(kh => kh.UserId == userId);

            if (khachHang == null) return NotFound("Không tìm thấy khách hàng.");



            var yeuCau = await _thuGomRepository.GetByIdAsync(id, includeDetails: false);



            if (yeuCau == null || yeuCau.M_KhachHang != khachHang.M_KhachHang)

            {

                return NotFound("Không tìm thấy yêu cầu hoặc bạn không có quyền hủy yêu cầu này.");

            }



            // --- KIỂM TRA TRẠNG THÁI CHO PHÉP HỦY ---

            if (yeuCau.TrangThai?.Equals("Chờ xử lý", StringComparison.OrdinalIgnoreCase) != true)

            {

                TempData["ErrorMessage"] = $"Không thể hủy yêu cầu ở trạng thái '{yeuCau.TrangThai}'.";

                return RedirectToAction(nameof(YeuCauThuGomDetails), new { id = id });

            }



            return View("YeuCauThuGomCancel", yeuCau); // Truyền vào View xác nhận

        }



        // --- Action Hủy Yêu Cầu - POST (Xác nhận hủy - Đã có ví dụ ở câu trả lời trước) ---

        [HttpPost, ActionName("YeuCauThuGomCancel")] // Đặt tên rõ ràng cho route post

        [ValidateAntiForgeryToken]

        public async Task<IActionResult> YeuCauThuGomCancelConfirmed(string id) // Tham số id lấy từ route

        {

            if (string.IsNullOrEmpty(id)) return NotFound();



            var userId = _userManager.GetUserId(User);

            if (userId == null) return Challenge();

            var khachHang = await _context.KhachHangs.FirstOrDefaultAsync(kh => kh.UserId == userId);

            if (khachHang == null) return NotFound("Không tìm thấy khách hàng.");



            // Lấy entity từ DbContext để update (dùng context vì repo có thể không có hàm Update)

            var yeuCau = await _context.YeuCauThuGoms.FirstOrDefaultAsync(yc => yc.M_YeuCau == id);



            if (yeuCau == null || yeuCau.M_KhachHang != khachHang.M_KhachHang)

            {

                return NotFound("Không tìm thấy yêu cầu hoặc bạn không có quyền hủy yêu cầu này.");

            }



            // --- KIỂM TRA LẠI TRẠNG THÁI TRƯỚC KHI HỦY ---

            if (yeuCau.TrangThai?.Equals("Chờ xử lý", StringComparison.OrdinalIgnoreCase) != true)

            {

                TempData["ErrorMessage"] = $"Không thể hủy yêu cầu vì trạng thái đã thay đổi thành '{yeuCau.TrangThai}'.";

                return RedirectToAction(nameof(YeuCauThuGomDetails), new { id = id });

            }



            try

            {

                yeuCau.TrangThai = "Đã hủy"; // Cập nhật trạng thái

                _context.YeuCauThuGoms.Update(yeuCau); // Đánh dấu là cần update

                await _context.SaveChangesAsync(); // Lưu thay đổi

                _logger.LogInformation($"User {userId} cancelled request {id}.");

                TempData["SuccessMessage"] = $"Đã hủy thành công yêu cầu #{id}.";

                return RedirectToAction(nameof(LichSuYeuCauThuGom)); // Về trang lịch sử

            }

            catch (Exception ex) // Bắt lỗi chung hoặc DbUpdateConcurrencyException

            {

                _logger.LogError(ex, $"Error cancelling request {id} by user {userId}.");

                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi hủy yêu cầu.";

                return RedirectToAction(nameof(YeuCauThuGomDetails), new { id = id });

            }

        }
        [HttpGet]
        public async Task<IActionResult> Create() // Đổi tên từ ThuGom thành Create

        {
            await LoadDropdownDataAsync(); // Gọi hàm helper load dropdown
            return View("Create", new ThuGomViewModel()); // Trả về View Create.cshtml

        }



        // --- Action Tạo Yêu Cầu - POST (Di chuyển từ HomeController) ---

        [HttpPost]

        [ValidateAntiForgeryToken]

        public async Task<IActionResult> Create(ThuGomViewModel model) // Đổi tên từ ThuGom thành Create

        {

            // --- KIỂM TRA MODELSTATE ---

            if (!ModelState.IsValid)

            {

                _logger.LogWarning("Dữ liệu tạo yêu cầu không hợp lệ.");

                await LoadDropdownDataAsync(model); // Load lại dropdown và giữ giá trị đã chọn

                return View("Create", model); // Trả về View Create.cshtml với lỗi

            }



            // --- Lấy Khách Hàng ---

            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId)) return Challenge();

            var khachHang = await _context.KhachHangs.FirstOrDefaultAsync(kh => kh.UserId == userId);

            if (khachHang == null)

            {

                TempData["ErrorMessage"] = "Không tìm thấy thông tin khách hàng liên kết.";

                return RedirectToAction("Index", "Home", new { area = "" }); // Hoặc trang profile

            }



            // --- Xử lý Tạo Yêu Cầu và Chi Tiết ---

            using var transaction = await _context.Database.BeginTransactionAsync();

            try

            {

                var yeuCau = new YeuCauThuGom

                {

                    M_YeuCau = GenerateRequestCode(),

                    M_KhachHang = khachHang.M_KhachHang,

                    NgayYeuCau = DateTime.UtcNow,

                    ThoiGianSanSang = model.PickupReadyTime.Value,

                    GhiChu = model.SupplierNotes,

                    TrangThai = "Chờ xử lý",

                    // M_QuanLy = null, // Gán ở đây nếu M_QuanLy thuộc YeuCauThuGom

                };



                // Xử lý ảnh

                List<string> savedImagePaths = await ProcessUploadedImages(model.ByproductImages, yeuCau.M_YeuCau);

                if (savedImagePaths == null) // Có lỗi upload

                {

                    await transaction.RollbackAsync();

                    ModelState.AddModelError("ByproductImages", "Đã xảy ra lỗi khi tải lên hình ảnh.");

                    await LoadDropdownDataAsync(model);

                    return View("Create", model);

                }



                // Tạo Chi Tiết (Đã sửa để dùng M_LoaiSP theo yêu cầu cuối)

                var chiTiet = new ChiTietThuGom

                {

                    // M_ChiTiet tự tăng

                    M_YeuCau = yeuCau.M_YeuCau, // Gán FK

                    M_LoaiSP = model.M_LoaiSP, // <<< Lưu Mã Loại Sản Phẩm

                    M_DonViTinh = model.ByproductUnit,

                    SoLuong = (int)model.ByproductQuantity.Value,

                    MoTa = model.ByproductDescription, // <<< Đảm bảo tên này đúng trong Model

                    GiaTriMongMuon = model.ByproductValue,

                    DacTinh_CongKenh = model.CharBulky,

                    DacTinh_AmUot = model.CharWet,

                    DacTinh_Kho = model.CharDry,

                    DacTinh_DoAmCao = model.CharMoisture,

                    DacTinh_TapChat = model.CharImpure,

                    DacTinh_DaXuLy = model.CharProcessed,

                    DanhSachHinhAnh = savedImagePaths.Any() ? string.Join(";", savedImagePaths) : null

                    // Không có M_SanPham, M_QuanLy

                };

                yeuCau.ChiTietThuGoms.Add(chiTiet);





                // Lưu bằng Repository hoặc DbContext

                await _thuGomRepository.AddAsync(yeuCau); // Dùng repo Add

                await _context.SaveChangesAsync();      // Lưu qua context



                await transaction.CommitAsync();



                _logger.LogInformation($"Đã tạo yêu cầu thu gom {yeuCau.M_YeuCau} bởi KH {khachHang.M_KhachHang}.");

                TempData["SuccessMessage"] = $"Yêu cầu thu gom ({yeuCau.M_YeuCau}) đã được gửi thành công!";

                return RedirectToAction(nameof(LichSuYeuCauThuGom)); // Về trang lịch sử



            }

            catch (Exception ex) // Bắt lỗi chung hoặc DbUpdateException

            {

                await transaction.RollbackAsync();

                _logger.LogError(ex, "Lỗi khi xử lý POST Create ThuGom.");

                ModelState.AddModelError("", "Đã xảy ra lỗi khi tạo yêu cầu. Vui lòng thử lại.");

                await LoadDropdownDataAsync(model); // Load lại dropdown khi có lỗi

                return View("Create", model);

            }

        }



        // --- Action Sửa Yêu Cầu - GET ---

        [HttpGet]

        public async Task<IActionResult> YeuCauThuGomEdit(string id)

        {

            if (string.IsNullOrEmpty(id)) return NotFound();



            var userId = _userManager.GetUserId(User);

            if (userId == null) return Challenge();

            var khachHang = await _context.KhachHangs.AsNoTracking().FirstOrDefaultAsync(kh => kh.UserId == userId);

            if (khachHang == null) return NotFound("Không tìm thấy khách hàng.");



            // Lấy dữ liệu hiện tại, include đủ thông tin cần thiết

            var yeuCau = await _thuGomRepository.GetByIdAsync(id, includeDetails: true);



            if (yeuCau == null || yeuCau.M_KhachHang != khachHang.M_KhachHang)

            {

                return NotFound("Không tìm thấy yêu cầu hoặc bạn không có quyền sửa yêu cầu này.");

            }



            // --- KIỂM TRA TRẠNG THÁI CHO PHÉP SỬA ---

            if (yeuCau.TrangThai?.Equals("Chờ xử lý", StringComparison.OrdinalIgnoreCase) != true)

            {

                TempData["ErrorMessage"] = $"Không thể sửa yêu cầu ở trạng thái '{yeuCau.TrangThai}'.";

                return RedirectToAction(nameof(YeuCauThuGomDetails), new { id = id });

            }

            // ---------------------------------------



            var firstDetail = yeuCau.ChiTietThuGoms?.FirstOrDefault();

            if (firstDetail == null)

            {

                // Xử lý trường hợp không có chi tiết (lỗi dữ liệu?)

                TempData["ErrorMessage"] = "Lỗi: Không tìm thấy chi tiết của yêu cầu.";

                return RedirectToAction(nameof(LichSuYeuCauThuGom));

            }



            // Map dữ liệu từ Entity sang ViewModel để hiển thị trên form Edit

            // Có thể dùng lại ThuGomViewModel hoặc tạo EditThuGomViewModel riêng

            var model = new ThuGomViewModel

            {

                M_YeuCau = yeuCau.M_YeuCau, // << Cần thêm M_YeuCau vào ViewModel nếu dùng lại ThuGomViewModel

                SupplierName = khachHang.Ten_KhachHang, // Lấy thông tin mới nhất của KH

                SupplierPhone = khachHang.SDT_KhachHang,

                SupplierProvince = khachHang.MaTinh,

                SupplierDistrict = khachHang.MaQuan,

                SupplierWard = khachHang.MaXa,

                SupplierStreet = khachHang.DiaChi_DuongApThon,

                PickupReadyTime = yeuCau.ThoiGianSanSang,

                SupplierNotes = yeuCau.GhiChu,



                M_LoaiSP = firstDetail.M_LoaiSP, // Map từ M_LoaiSP

                ByproductDescription = firstDetail.MoTa,

                ByproductQuantity = firstDetail.SoLuong,

                ByproductUnit = firstDetail.M_DonViTinh,

                ByproductValue = firstDetail.GiaTriMongMuon,

                CharBulky = firstDetail.DacTinh_CongKenh,

                CharWet = firstDetail.DacTinh_AmUot,

                CharDry = firstDetail.DacTinh_Kho,

                CharMoisture = firstDetail.DacTinh_DoAmCao,

                CharImpure = firstDetail.DacTinh_TapChat,

                CharProcessed = firstDetail.DacTinh_DaXuLy,

                // Không load lại ByproductImages, người dùng cần chọn lại nếu muốn thay đổi


            };



            await LoadThuGomDropdownsAsync(model); // Load dropdown và giữ giá trị đang được chọn

            return View("YeuCauThuGomEdit", model); // Trả về View Edit

        }

        // --- Action Sửa Yêu Cầu - POST ---

        [HttpPost]

        [ValidateAntiForgeryToken]

        public async Task<IActionResult> YeuCauThuGomEdit(ThuGomViewModel model) // Model nhận về từ form Edit

        {

            // --- KIỂM TRA MODELSTATE ---

            // Cần đảm bảo ViewModel có M_YeuCau để biết sửa yêu cầu nào

            if (string.IsNullOrEmpty(model.M_YeuCau))

            {

                ModelState.AddModelError("", "Thiếu mã yêu cầu để cập nhật.");

            }

            if (!ModelState.IsValid)

            {

                _logger.LogWarning("Dữ liệu cập nhật yêu cầu không hợp lệ.");

                await LoadDropdownDataAsync(model); // Load lại dropdown

                return View("YeuCauThuGomEdit", model);

            }



            // --- Lấy User/KhachHang ---

            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId)) return Challenge();

            var khachHang = await _context.KhachHangs.AsNoTracking().FirstOrDefaultAsync(kh => kh.UserId == userId);

            if (khachHang == null) return NotFound("Không tìm thấy khách hàng.");



            // --- Lấy Yêu cầu gốc từ DB để cập nhật (Include ChiTiet) ---

            var yeuCauGoc = await _context.YeuCauThuGoms

                  .Include(yc => yc.ChiTietThuGoms)

                  .FirstOrDefaultAsync(yc => yc.M_YeuCau == model.M_YeuCau);



            // --- Kiểm tra quyền và trạng thái ---

            if (yeuCauGoc == null || yeuCauGoc.M_KhachHang != khachHang.M_KhachHang)

            {

                return NotFound("Không tìm thấy yêu cầu hoặc bạn không có quyền sửa yêu cầu này.");

            }

            if (yeuCauGoc.TrangThai?.Equals("Chờ xử lý", StringComparison.OrdinalIgnoreCase) != true)

            {

                TempData["ErrorMessage"] = $"Không thể sửa yêu cầu vì trạng thái đã thay đổi thành '{yeuCauGoc.TrangThai}'.";

                await LoadDropdownDataAsync(model);

                return View("YeuCauThuGomEdit", model);

            }



            var chiTietGoc = yeuCauGoc.ChiTietThuGoms.FirstOrDefault(); // Lấy chi tiết gốc

            if (chiTietGoc == null)

            {

                TempData["ErrorMessage"] = "Lỗi: Không tìm thấy chi tiết gốc của yêu cầu.";

                await LoadDropdownDataAsync(model);

                return View("YeuCauThuGomEdit", model);

            }



            // --- Cập nhật dữ liệu ---

            using var transaction = await _context.Database.BeginTransactionAsync();

            try

            {

                // Cập nhật YeuCauThuGom

                yeuCauGoc.ThoiGianSanSang = model.PickupReadyTime.Value;

                yeuCauGoc.GhiChu = model.SupplierNotes;

                chiTietGoc.M_LoaiSP = model.M_LoaiSP; // Cập nhật loại SP
                chiTietGoc.M_DonViTinh = model.ByproductUnit;

                chiTietGoc.SoLuong = (int)model.ByproductQuantity.Value;

                chiTietGoc.MoTa = model.ByproductDescription;

                chiTietGoc.GiaTriMongMuon = model.ByproductValue;

                chiTietGoc.DacTinh_CongKenh = model.CharBulky;

                chiTietGoc.DacTinh_AmUot = model.CharWet;

                chiTietGoc.DacTinh_Kho = model.CharDry;

                chiTietGoc.DacTinh_DoAmCao = model.CharMoisture;

                chiTietGoc.DacTinh_TapChat = model.CharImpure;

                chiTietGoc.DacTinh_DaXuLy = model.CharProcessed;

                // Cập nhật DanhSachHinhAnh nếu có xử lý ảnh



                // Đánh dấu là đã sửa (EF Core tự động làm nếu lấy entity từ context)

                _context.YeuCauThuGoms.Update(yeuCauGoc); // Hoặc chỉ cần SaveChanges nếu đã lấy từ context

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();



                TempData["SuccessMessage"] = $"Đã cập nhật thành công yêu cầu #{yeuCauGoc.M_YeuCau}.";

                return RedirectToAction(nameof(YeuCauThuGomDetails), new { id = yeuCauGoc.M_YeuCau });

            }

            catch (Exception ex)

            {

                await transaction.RollbackAsync();

                _logger.LogError(ex, $"Lỗi khi cập nhật yêu cầu {model.M_YeuCau}.");

                ModelState.AddModelError("", "Đã xảy ra lỗi khi cập nhật yêu cầu.");

                await LoadDropdownDataAsync(model);

                return View("YeuCauThuGomEdit", model);

            }

        }

        public async Task<IActionResult> ChiTietDonHang(string id) // id ở đây là M_DonHang (Mã Đơn Hàng chính)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound("Mã đơn hàng không hợp lệ.");
            }

            var userId = _userManager.GetUserId(User);
            if (userId == null)
            {
                return Challenge(); // Chưa đăng nhập
            }

            var donHang = await _context.DonHangs // Truy vấn từ bảng DonHangs (Đơn hàng chính)
                .Include(dh => dh.KhachHang) // Thông tin khách hàng của đơn hàng
                .Include(dh => dh.PhuongThucThanhToan) // Phương thức thanh toán của đơn hàng
                .Include(dh => dh.VanChuyen) // Thông tin vận chuyển của đơn hàng
                .Include(dh => dh.ChiTietDatHangs) // Lấy danh sách tất cả các ChiTietDatHang thuộc đơn hàng này
                    .ThenInclude(ctdh => ctdh.SanPham) // Với mỗi ChiTietDatHang, lấy thông tin SanPham của nó
                        .ThenInclude(sp => sp.LoaiSanPham) // Nếu cần, lấy thêm LoaiSP của SanPham
                .FirstOrDefaultAsync(dh => dh.M_DonHang == id && dh.KhachHang.UserId == userId); // Lọc theo M_DonHang và UserId của khách hàng

            if (donHang == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng hoặc bạn không có quyền xem đơn hàng này.";
                return RedirectToAction(nameof(LichSuDonHang));
            }

            ViewData["Title"] = $"Chi tiết Đơn hàng #{donHang.M_DonHang}";
            return View(donHang); // Truyền đối tượng DonHang (đơn hàng chính) cho View
        }
        // Thêm hàm này vào KhachHangController.cs
        private async Task LoadThuGomDropdownsAsync(ThuGomViewModel model)
        {
            if (model == null || _context == null) return;
            try
            {
                // Load Loại Sản Phẩm
                var loaiSpList = await _context.LoaiSanPhams.OrderBy(lsp => lsp.TenLoai).ToListAsync();
                model.LoaiSanPhamOptions = new SelectList(loaiSpList, "M_LoaiSP", "TenLoai", model.M_LoaiSP);

                // Load Đơn vị tính
                var donViTinhList = await _context.DonViTinhs.OrderBy(dvt => dvt.TenLoaiTinh).ToListAsync();
                model.DonViTinhOptions = new SelectList(donViTinhList, "M_DonViTinh", "TenLoaiTinh", model.ByproductUnit);

                // Load Tỉnh/Thành phố
                var provinces = await _context.TinhThanhPhos.OrderBy(p => p.TenTinh).ToListAsync();
                model.ProvinceOptions = new SelectList(provinces, "MaTinh", "TenTinh", model.SupplierProvince);

                // Load Quận/Huyện nếu có Tỉnh được chọn sẵn
                var districts = Enumerable.Empty<QuanHuyen>();
                if (!string.IsNullOrEmpty(model.SupplierProvince))
                {
                    districts = await _context.QuanHuyens.Where(q => q.MaTinh == model.SupplierProvince).OrderBy(q => q.TenQuan).ToListAsync();
                }
                model.DistrictOptions = new SelectList(districts, "MaQuan", "TenQuan", model.SupplierDistrict);

                // Load Xã/Phường nếu có Quận/Huyện được chọn sẵn
                var wards = Enumerable.Empty<XaPhuong>();
                if (!string.IsNullOrEmpty(model.SupplierDistrict))
                {
                    wards = await _context.XaPhuongs.Where(w => w.MaQuan == model.SupplierDistrict).OrderBy(w => w.TenXa).ToListAsync();
                }
                model.WardOptions = new SelectList(wards, "MaXa", "TenXa", model.SupplierWard);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dropdown data for ThuGom Edit/Create");
                // Gán rỗng khi lỗi để tránh lỗi View
                model.LoaiSanPhamOptions ??= Enumerable.Empty<SelectListItem>();
                model.DonViTinhOptions ??= Enumerable.Empty<SelectListItem>();
                model.ProvinceOptions ??= Enumerable.Empty<SelectListItem>();
                model.DistrictOptions ??= Enumerable.Empty<SelectListItem>();
                model.WardOptions ??= Enumerable.Empty<SelectListItem>();
            }
        }
        public async Task<IActionResult> SanPhamChoDanhGia()
        {
            var userId = _userManager.GetUserId(User);
            var khachHang = await _context.KhachHangs.FirstOrDefaultAsync(k => k.UserId == userId);

            if (khachHang == null) return RedirectToAction("Login", "Account", new { area = "Identity" });

            // Lấy danh sách ID sản phẩm đã mua (Từ các đơn hàng đã hoàn thành/giao hàng)
            // LƯU Ý: Thay "Hoàn thành" bằng trạng thái đơn hàng thành công thực tế của bạn
            var purchasedProductIds = await _context.ChiTietDatHangs
                .Include(ct => ct.DonHang)
                .Where(ct => ct.DonHang.M_KhachHang == khachHang.M_KhachHang
                             && ct.DonHang.TrangThai == "Hoàn thành")
                .Select(ct => ct.M_SanPham)
                .Distinct()
                .ToListAsync();

            // Lấy danh sách ID sản phẩm đã đánh giá
            var reviewedProductIds = await _context.ChiTietDanhGias
                .Where(dg => dg.M_KhachHang == khachHang.M_KhachHang)
                .Select(dg => dg.M_SanPham)
                .ToListAsync();

            // Lọc ra sản phẩm chưa đánh giá = (Đã mua) - (Đã đánh giá)
            var productsToReviewIds = purchasedProductIds.Except(reviewedProductIds).ToList();

            var productsToReview = await _context.SanPhams
                .Where(sp => productsToReviewIds.Contains(sp.M_SanPham))
                .ToListAsync();

            return View(productsToReview);
        }

        // 2. Xử lý Gửi đánh giá (Chuyển từ DS_SPController sang đây và sửa đổi logic)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuiDanhGia(string m_SanPham, int mucDoHaiLong, string moTa_DanhGia)
        {
            var userId = _userManager.GetUserId(User);
            var khachHang = await _context.KhachHangs.FirstOrDefaultAsync(k => k.UserId == userId);

            if (khachHang == null) return RedirectToAction("Index");

            // BẢO MẬT: Kiểm tra kỹ lại lần nữa xem khách đã mua sản phẩm này chưa
            bool hasPurchased = await _context.ChiTietDatHangs
                .Include(ct => ct.DonHang)
                .AnyAsync(ct => ct.M_SanPham == m_SanPham
                                && ct.DonHang.M_KhachHang == khachHang.M_KhachHang
                                && ct.DonHang.TrangThai == "Hoàn thành");

            if (!hasPurchased)
            {
                TempData["Message"] = "Lỗi: Bạn chưa mua sản phẩm này hoặc đơn hàng chưa hoàn tất.";
                return RedirectToAction("SanPhamChoDanhGia");
            }

            // Kiểm tra đã đánh giá chưa
            bool alreadyReviewed = await _context.ChiTietDanhGias
                .AnyAsync(dg => dg.M_SanPham == m_SanPham && dg.M_KhachHang == khachHang.M_KhachHang);

            if (alreadyReviewed)
            {
                TempData["Message"] = "Bạn đã đánh giá sản phẩm này rồi.";
                return RedirectToAction("SanPhamChoDanhGia");
            }

            // Lưu đánh giá
            var review = new ChiTietDanhGia
            {
                M_KhachHang = khachHang.M_KhachHang,
                M_SanPham = m_SanPham,
                MucDoHaiLong = mucDoHaiLong.ToString(), // Nếu bạn đã sửa model thành int thì bỏ .ToString()
                MoTa_DanhGia = moTa_DanhGia,
                NgayDanhGia = DateTime.UtcNow
            };

            _context.ChiTietDanhGias.Add(review);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Đánh giá thành công! Cảm ơn bạn.";
            return RedirectToAction("SanPhamChoDanhGia");
        }
        [HttpGet]
        public async Task<IActionResult> LichSuDanhGia()
        {
            var userId = _userManager.GetUserId(User);
            var khachHang = await _context.KhachHangs.FirstOrDefaultAsync(k => k.UserId == userId);

            if (khachHang == null) return RedirectToAction("Login", "Account", new { area = "Identity" });

            // Lấy danh sách đánh giá của khách hàng này, kèm thông tin sản phẩm
            var history = await _context.ChiTietDanhGias
                .Include(dg => dg.SanPham) // Join bảng Sản phẩm để lấy tên và ảnh
                .Where(dg => dg.M_KhachHang == khachHang.M_KhachHang)
                .OrderByDescending(dg => dg.NgayDanhGia) // Mới nhất lên đầu
                .ToListAsync();

            return View(history);
        }



        // Hàm helper tạo mã (giữ lại hoặc chuyển vào service)

        private string GenerateRequestCode()

        {

            string prefix = "YC";

            string timestamp = DateTime.UtcNow.ToString("yyMMddHHmm");

            string randomPart = new Random().Next(1000, 9999).ToString();

            return $"{prefix}{timestamp}{randomPart}".Substring(0, 10);

        }



        // Action Error

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }



        private async Task LoadDropdownDataAsync(ThuGomViewModel? model = null)

        {
            var sanPhamList = await _context.LoaiSanPhams.OrderBy(sp => sp.TenLoai).ToListAsync(); // Dùng LoaiSanPham theo yêu cầu cuối

            ViewData["LoaiSanPhamOptions"] = new SelectList(sanPhamList, "M_LoaiSP", "TenLoai", model?.M_LoaiSP); // Giữ giá trị chọn nếu có



            var donViTinhList = await _context.DonViTinhs.OrderBy(dvt => dvt.TenLoaiTinh).ToListAsync(); // Dùng TenDonViTinh

            ViewData["DonViTinhOptions"] = new SelectList(donViTinhList, "M_DonViTinh", "TenDonViTinh", model?.ByproductUnit);

        }

        [HttpGet] // Action này chỉ đáp ứng yêu cầu GET
        public async Task<JsonResult> GetDistrictsForHome(string provinceId) // Nhận MaTinh từ AJAX
        {
            // Kiểm tra xem provinceId có được gửi lên không
            if (string.IsNullOrEmpty(provinceId))
            {
                // Trả về một danh sách rỗng nếu không có provinceId
                return Json(new List<object>());
            }

            try
            {
                // Truy vấn database để lấy các Quận/Huyện thuộc Tỉnh/TP đã chọn
                var districts = await _context.QuanHuyens // Sử dụng DbContext của bạn
                                        .Where(q => q.MaTinh == provinceId) // Lọc theo MaTinh
                                        .OrderBy(q => q.TenQuan) // Sắp xếp theo tên
                                                                 // Chọn chỉ Mã và Tên để gửi về trình duyệt (nhẹ hơn)
                                                                 // Đặt tên thuộc tính là 'id' và 'name' để JavaScript dễ dùng
                                        .Select(q => new { id = q.MaQuan, name = q.TenQuan })
                                        .ToListAsync();

                // Trả về danh sách dưới dạng JSON
                return Json(districts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi lấy Quận/Huyện cho Tỉnh ID: {provinceId}");
                // Cân nhắc trả về một đối tượng lỗi để JS biết và xử lý
                // return Json(new { error = "Lỗi tải dữ liệu Quận/Huyện." });
                return Json(new List<object>()); // Hoặc trả về rỗng khi lỗi
            }
        }

        [HttpGet] // Action này chỉ đáp ứng yêu cầu GET
        public async Task<JsonResult> GetWardsForHome(string districtId) // Nhận MaQuan từ AJAX
        {
            // Kiểm tra xem districtId có được gửi lên không
            if (string.IsNullOrEmpty(districtId))
            {
                // Trả về một danh sách rỗng nếu không có districtId
                return Json(new List<object>());
            }

            try
            {
                // Truy vấn database để lấy các Xã/Phường thuộc Quận/Huyện đã chọn
                var wards = await _context.XaPhuongs // Sử dụng DbContext của bạn
                                       .Where(w => w.MaQuan == districtId) // Lọc theo MaQuan
                                       .OrderBy(w => w.TenXa) // Sắp xếp theo tên
                                                              // Chọn chỉ Mã và Tên
                                                              // Đặt tên thuộc tính là 'id' và 'name'
                                       .Select(w => new { id = w.MaXa, name = w.TenXa })
                                       .ToListAsync();

                // Trả về danh sách dưới dạng JSON
                return Json(wards);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi lấy Xã/Phường cho Quận ID: {districtId}");
                // return Json(new { error = "Lỗi tải dữ liệu Xã/Phường." });
                return Json(new List<object>()); // Trả về rỗng khi lỗi
            }
        }

    }

}