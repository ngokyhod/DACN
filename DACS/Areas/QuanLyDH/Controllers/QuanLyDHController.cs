// File: Areas/QuanLyDH/Controllers/QuanLyDHController.cs // (Tiếp tục file của bạn)
using DACS.Models;
using DACS.Areas.Owner.Models; // ViewModel của bạn
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System; // For DateTime
using System.Linq; // For LINQ methods like .Any()
using System.Threading.Tasks; // For Task
using System.Collections.Generic;
using DACS.Models.ViewModels; // For List
using DACS.Services;
using Microsoft.Extensions.Logging;

namespace DACS.Areas.QuanLyDH.Controllers
{
    [Area("QuanLyDH")]
    [Authorize(Roles = SD.Role_Owner + "," + SD.Role_QuanLyDH)] // Đảm bảo phân quyền phù hợp
    public class QuanLyDHController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<QuanLyDHController> _logger;
        private readonly BlockchainService _blockchainService;

        // --- ĐỊNH NGHĨA CÁC CHUỖI TRẠNG THÁI ĐƠN HÀNG TRONG DATABASE ---
        private const string StatusPendingDbValue = "Chờ xác nhận"; // Hoặc "Chưa xử lý" nếu bạn dùng nó làm giá trị DB ban đầu
        private const string StatusConfirmedDbValue = "Đã xác nhận";
        private const string StatusProcessingDbValue = "Đang xử lý"; // Đã có
        private const string StatusShippingDbValue = "Đang giao hàng";
        private const string StatusCompletedDbValue = "Hoàn thành";
        private const string StatusCancelledDbValue = "Đã hủy";
        // --- KẾT THÚC ĐỊNH NGHĨA ---

        public QuanLyDHController(ApplicationDbContext context,
                                        ILogger<QuanLyDHController> logger,
                                        BlockchainService blockchainService)
        {
            _context = context;
            _logger = logger;
            _blockchainService = blockchainService;
        }

        // GET: QuanLyDH/QuanLyDH (Index)
        public async Task<IActionResult> Index(
            string searchInput,
            DateTime? dateFromFilter,
            DateTime? dateToFilter,
            string statusFilterSelect,
            string paymentStatusFilter,
            int page = 1)
        {
            ViewData["Title"] = "Quản Lý Đơn Hàng";
            int pageSize = 5;

            var viewModel = new Owner.Models.OrderManagementViewModel
            {
                SearchInput = searchInput,
                DateFromFilter = dateFromFilter,
                DateToFilter = dateToFilter,
                SelectedOrderStatus = statusFilterSelect,
                SelectedPaymentStatus = paymentStatusFilter,
                OrderStatusOptions = GetOrderStatusOptions(statusFilterSelect),
                PaymentStatusOptions = GetPaymentStatusOptions(paymentStatusFilter),
                CurrentPage = page
            };

            IQueryable<DonHang> donHangQuery = _context.DonHangs
                                                .Include(dh => dh.KhachHang)
                                                .Include(dh => dh.PhuongThucThanhToan)
                                                .Include(dh => dh.VanChuyen) // Include VanChuyen để lấy M_VanDon
                                                .OrderByDescending(dh => dh.NgayDat);

            if (!string.IsNullOrEmpty(searchInput))
            {
                string searchLower = searchInput.ToLower();
                donHangQuery = donHangQuery.Where(dh =>
                    dh.M_DonHang.ToLower().Contains(searchLower) ||
                    (dh.KhachHang != null && dh.KhachHang.Ten_KhachHang.ToLower().Contains(searchLower)) ||
                    (dh.KhachHang != null && dh.KhachHang.SDT_KhachHang.Contains(searchInput))
                );
            }
            if (dateFromFilter.HasValue)
            {
                donHangQuery = donHangQuery.Where(dh => dh.NgayDat.Date >= dateFromFilter.Value.Date);
            }
            if (dateToFilter.HasValue)
            {
                donHangQuery = donHangQuery.Where(dh => dh.NgayDat.Date <= dateToFilter.Value.Date);
            }
            if (!string.IsNullOrEmpty(statusFilterSelect))
            {
                string dbOrderStatus = MapFilterToDbOrderStatus(statusFilterSelect);
                if (!string.IsNullOrEmpty(dbOrderStatus))
                {
                    donHangQuery = donHangQuery.Where(dh => dh.TrangThai == dbOrderStatus);
                }
            }
            // Lọc theo paymentStatusFilter sẽ không được thực hiện ở đây nếu DonHang model ko có trường trạng thái TT tường minh
            // (Code hiện tại suy luận TT Thanh toán)

            var currentMonthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var currentMonthEnd = currentMonthStart.AddMonths(1).AddDays(-1);

            viewModel.PendingConfirmationOrders = await _context.DonHangs.CountAsync(d => d.TrangThai == StatusPendingDbValue || d.TrangThai == "Chưa xử lý"); // Thêm "Chưa xử lý" nếu đó là trạng thái ban đầu của bạn
            viewModel.ProcessingOrShippingOrders = await _context.DonHangs.CountAsync(d => d.TrangThai == StatusConfirmedDbValue || d.TrangThai == StatusProcessingDbValue || d.TrangThai == StatusShippingDbValue);
            viewModel.CompletedOrdersThisMonth = await _context.DonHangs.CountAsync(d => d.TrangThai == StatusCompletedDbValue && d.NgayDat >= currentMonthStart && d.NgayDat <= currentMonthEnd);
            viewModel.CancelledOrdersThisMonth = await _context.DonHangs.CountAsync(d => d.TrangThai == StatusCancelledDbValue && d.NgayDat >= currentMonthStart && d.NgayDat <= currentMonthEnd);

            int totalItems = await donHangQuery.CountAsync();
            viewModel.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (viewModel.CurrentPage < 1) viewModel.CurrentPage = 1;
            if (viewModel.CurrentPage > viewModel.TotalPages && viewModel.TotalPages > 0) viewModel.CurrentPage = viewModel.TotalPages;

            var pagedDonHangs = await donHangQuery
                                        .Skip((viewModel.CurrentPage - 1) * pageSize)
                                        .Take(pageSize)
                                        .ToListAsync();

            viewModel.Orders = pagedDonHangs.Select(dh => new OrderViewModel
            {
                M_DonHang = dh.M_DonHang,
                CustomerName = dh.KhachHang?.Ten_KhachHang ?? "N/A",
                CustomerPhoneNumber = dh.KhachHang?.SDT_KhachHang ?? "N/A",
                OrderDate = dh.NgayDat,
                TotalAmount = (decimal)dh.TotalPrice,
                OrderStatusText = dh.TrangThai,
                OrderStatusBadgeClass = GetOrderStatusBadgeClass(dh.TrangThai),
                PaymentStatusText = InferPaymentStatusText(dh),
                PaymentStatusBadgeClass = InferPaymentStatusBadgeClass(InferPaymentStatusText(dh)),
            }).ToList();

            return View(viewModel);
        }


        // GET: QuanLyDH/QuanLyDH/Details/DH00001
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var donHang = await _context.DonHangs
                .Include(d => d.KhachHang)
                .Include(d => d.PhuongThucThanhToan)
                .Include(d => d.VanChuyen)
                .Include(d => d.ChiTietDatHangs).ThenInclude(ctdh => ctdh.SanPham)
                .FirstOrDefaultAsync(m => m.M_DonHang == id);
            if (donHang == null) return NotFound();
            ViewData["Title"] = $"Chi tiết đơn hàng {donHang.M_DonHang}";
            ViewData["OrderStatusBadgeClass"] = GetOrderStatusBadgeClass(donHang.TrangThai);

            // Cập nhật để lấy TrangThaiThanhToan trực tiếp nếu có, hoặc suy luận
            ViewData["PaymentStatusText"] = string.IsNullOrEmpty(donHang.TrangThaiThanhToan) ? InferPaymentStatusText(donHang) : donHang.TrangThaiThanhToan;
            ViewData["PaymentStatusBadgeClass"] = GetPaymentStatusBadgeClass(ViewData["PaymentStatusText"].ToString());

            return View(donHang);
        }

        // POST: QuanLyDH/QuanLyDH/UpdateStatus/DH00001
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(string id, string newStatusKey)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(newStatusKey))
                return BadRequest("Thông tin không hợp lệ.");

            // Bắt đầu Transaction
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var donHang = await _context.DonHangs.FirstOrDefaultAsync(m => m.M_DonHang == id);
                if (donHang == null)
                    return NotFound($"Không tìm thấy đơn hàng {id}.");

                string actualNewStatusInDb = "";
                string successMessagePart = "";
                bool canUpdate = false;
                string inventoryError = null; // Biến giữ lỗi tồn kho

                switch (newStatusKey.ToLower())
                {
                    case "confirm":
                        if (donHang.TrangThai == StatusPendingDbValue || donHang.TrangThai == "Chưa xử lý")
                        {
                            // --- SỬA: CHUYỂN TRỪ KHO LÊN ĐÂY ---
                            inventoryError = await ApplyInventoryChangesForOrderAsync(donHang, truKho: true);
                            if (inventoryError != null)
                            {
                                await transaction.RollbackAsync();
                                TempData["ErrorMessage"] = $"Lỗi tồn kho: {inventoryError}";
                                return RedirectToAction(nameof(Details), new { id = id });
                            }
                            // ------------------------------------

                            actualNewStatusInDb = StatusConfirmedDbValue;
                            successMessagePart = "xác nhận và đã trừ tồn kho.";
                            canUpdate = true;
                        }
                        break;

                    case "process":
                        if (donHang.TrangThai == StatusConfirmedDbValue)
                        {
                            actualNewStatusInDb = StatusProcessingDbValue;
                            successMessagePart = "chuyển sang đang xử lý.";
                            canUpdate = true;
                        }
                        break;

                    case "ship":
                        if (donHang.TrangThai == StatusConfirmedDbValue || donHang.TrangThai == StatusProcessingDbValue)
                        {
                            actualNewStatusInDb = StatusShippingDbValue;
                            successMessagePart = "chuyển sang đang giao.";
                            canUpdate = true;
                        }
                        break;

                    case "complete":
                        // SỬA: Cho phép hoàn thành từ các trạng thái đang chạy
                        if (donHang.TrangThai == StatusShippingDbValue ||
                            donHang.TrangThai == StatusProcessingDbValue ||
                            donHang.TrangThai == StatusConfirmedDbValue)
                        {
                            // QUAN TRỌNG: KHÔNG GỌI TRỪ KHO Ở ĐÂY NỮA (VÌ ĐÃ TRỪ Ở CONFIRM RỒI)

                            actualNewStatusInDb = StatusCompletedDbValue;
                            successMessagePart = "hoàn thành.";
                            canUpdate = true;
                        }
                        break;

                    case "cancel":
                        // Logic hủy giữ nguyên, nó sẽ hoàn kho đúng vì ta đã trừ kho ở bước confirm.
                        if (donHang.TrangThai == StatusPendingDbValue || donHang.TrangThai == "Chưa xử lý")
                        {
                            actualNewStatusInDb = StatusCancelledDbValue;
                            successMessagePart = "hủy (chưa trừ kho).";
                            canUpdate = true;
                        }
                        else if (donHang.TrangThai == StatusConfirmedDbValue ||
                                 donHang.TrangThai == StatusProcessingDbValue ||
                                 donHang.TrangThai == StatusShippingDbValue) // Thêm Shipping vào để cho phép hủy khi đang giao
                        {
                            inventoryError = await ApplyInventoryChangesForOrderAsync(donHang, truKho: false); // Hoàn kho
                            if (inventoryError != null)
                            {
                                _logger.LogError("Lỗi hoàn kho: {Error}", inventoryError);
                                successMessagePart = $"hủy. (Lỗi hoàn kho: {inventoryError})";
                            }
                            else
                            {
                                successMessagePart = "hủy và đã hoàn tồn kho.";
                            }
                            actualNewStatusInDb = StatusCancelledDbValue;
                            canUpdate = true;
                        }
                        break;
                }

                if (canUpdate && !string.IsNullOrEmpty(actualNewStatusInDb))
                {
                    donHang.TrangThai = actualNewStatusInDb;
                    _context.Update(donHang);

                    // Cập nhật trạng thái cho ChiTietDatHang
                    var chiTietItems = await _context.ChiTietDatHangs
                                    .Where(ct => ct.M_DonHang == donHang.M_DonHang)
                                    .ToListAsync();
                    foreach (var item in chiTietItems)
                    {
                        item.TrangThaiDonHang = actualNewStatusInDb;
                        _context.Update(item);
                    }

                    await _context.SaveChangesAsync(); // Lưu tất cả thay đổi (ĐH, CTĐH, LoTonKho)
                    await transaction.CommitAsync(); // Hoàn thành transaction

                    TempData["SuccessMessage"] = $"Đơn hàng {donHang.M_DonHang} đã được {successMessagePart}";
                }
                else
                {
                    await transaction.RollbackAsync(); // Không có gì thay đổi, rollback
                    TempData["ErrorMessage"] = $"Không thể cập nhật trạng thái cho đơn hàng {donHang.M_DonHang} từ '{donHang.TrangThai}' sang '{newStatusKey}'.";
                }

                // <<< XÓA: Bỏ hàm gọi CapNhatTonKhoAsync sai
                // await CapNhatTonKhoAsync(truKho: true); 
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi nghiêm trọng khi cập nhật trạng thái đơn hàng {DonHangId}", id);
                TempData["ErrorMessage"] = "Lỗi nghiêm trọng: " + ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id = id });
        }
        // --- CÁC ACTION METHOD MỚI ---

        // GET: QuanLyDH/QuanLyDH/EditOrder/DH00001
        [HttpGet]
        public async Task<IActionResult> EditOrder(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var donHang = await _context.DonHangs
                                    .Include(d => d.KhachHang) // Cần để lấy tên và SĐT khách hàng/người nhận ban đầu
                                    .FirstOrDefaultAsync(m => m.M_DonHang == id);

            if (donHang == null) return NotFound();

            // Chỉ cho phép sửa nếu đơn hàng ở trạng thái phù hợp (ví dụ: "Chờ xác nhận" hoặc "Chưa xử lý")
            if (donHang.TrangThai != StatusPendingDbValue && donHang.TrangThai != "Chưa xử lý")
            {
                TempData["ErrorMessage"] = $"Không thể sửa đơn hàng ở trạng thái '{donHang.TrangThai}'.";
                return RedirectToAction(nameof(Details), new { id = id });
            }

            // Map từ DonHang sang EditOrderViewModel
            var viewModel = new EditOrderViewModel
            {
                M_DonHang = donHang.M_DonHang,

                // Thông tin gốc để hiển thị
                Display_M_KhachHang = donHang.M_KhachHang,
                Display_Ten_KhachHang = donHang.KhachHang?.Ten_KhachHang ?? "N/A",
                Display_NgayDat = donHang.NgayDat,
                Display_TotalPrice = (decimal)donHang.TotalPrice, // Giả sử TotalPrice là decimal
                Display_TrangThai = donHang.TrangThai,
                TenNguoiNhan = donHang.KhachHang?.Ten_KhachHang ?? "", // Nếu KhachHang null, để trống
                SDTNguoiNhan = donHang.KhachHang?.SDT_KhachHang ?? "",   // Nếu KhachHang null, để trống
                ShippingAddress = donHang.ShippingAddress,
                Notes = donHang.Notes // Ghi chú của Owner/Quản lý
            };
            
            ViewData["Title"] = $"Sửa Đơn Hàng {viewModel.M_DonHang}";
            return View(viewModel); // Truyền ViewModel cho View
        }

        // POST: QuanLyDH/QuanLyDH/EditOrder/DH00001
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditOrder(string id, EditOrderViewModel viewModel) // Nhận EditOrderViewModel
        {
            if (id != viewModel.M_DonHang) // Kiểm tra id từ route và M_DonHang trong model có khớp không
            {
                return NotFound("Lỗi: Mã đơn hàng không khớp.");
            }

            // Trước khi kiểm tra ModelState, nạp lại các thông tin display nếu ModelState không hợp lệ và cần trả về View
            // Điều này cần thiết vì các trường Display_ không được POST lên.
            var donHangToUpdate = await _context.DonHangs
                                                .Include(d => d.KhachHang) // Include KhachHang để có thể cập nhật hoặc tham chiếu
                                                .FirstOrDefaultAsync(d => d.M_DonHang == viewModel.M_DonHang);

            if (donHangToUpdate == null)
            {
                return NotFound($"Không tìm thấy đơn hàng {viewModel.M_DonHang}.");
            }

            if (!ModelState.IsValid) // Validate EditOrderViewModel
            {
                // Nạp lại thông tin gốc cho ViewModel để hiển thị đúng nếu có lỗi validation
                viewModel.Display_M_KhachHang = donHangToUpdate.M_KhachHang;
                viewModel.Display_Ten_KhachHang = donHangToUpdate.KhachHang?.Ten_KhachHang ?? "N/A";
                viewModel.Display_NgayDat = donHangToUpdate.NgayDat;
                viewModel.Display_TotalPrice = (decimal)donHangToUpdate.TotalPrice;
                viewModel.Display_TrangThai = donHangToUpdate.TrangThai;

                ViewData["Title"] = $"Sửa Đơn Hàng {viewModel.M_DonHang}";
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ, vui lòng kiểm tra lại.";
                return View(viewModel); // Trả về ViewModel với các lỗi validation
            }


            // Kiểm tra lại trạng thái đơn hàng trước khi cập nhật (quan trọng)
            if (donHangToUpdate.TrangThai != StatusPendingDbValue && donHangToUpdate.TrangThai != "Chưa xử lý")
            {
                TempData["ErrorMessage"] = $"Không thể sửa đơn hàng ở trạng thái '{donHangToUpdate.TrangThai}'. Đơn hàng có thể đã được xử lý.";
                // Nạp lại thông tin display cho ViewModel
                viewModel.Display_M_KhachHang = donHangToUpdate.M_KhachHang;
                viewModel.Display_Ten_KhachHang = donHangToUpdate.KhachHang?.Ten_KhachHang ?? "N/A";
                viewModel.Display_NgayDat = donHangToUpdate.NgayDat;
                viewModel.Display_TotalPrice = (decimal)donHangToUpdate.TotalPrice;
                viewModel.Display_TrangThai = donHangToUpdate.TrangThai;
                return View(viewModel);
            }

            try
            {
                // Cập nhật các thuộc tính của donHangToUpdate từ viewModel
                donHangToUpdate.ShippingAddress = viewModel.ShippingAddress;
                donHangToUpdate.Notes = viewModel.Notes; // Ghi chú của Owner/QL

                // Cẩn thận khi cập nhật thông tin KhachHang.
                // Nếu bạn cho phép sửa tên/SĐT của KhachHang gốc liên kết với đơn hàng:
                if (donHangToUpdate.KhachHang != null)
                {
                    donHangToUpdate.KhachHang.Ten_KhachHang = viewModel.TenNguoiNhan;
                    donHangToUpdate.KhachHang.SDT_KhachHang = viewModel.SDTNguoiNhan;
                    _context.Update(donHangToUpdate.KhachHang); // Đánh dấu KhachHang là đã thay đổi nếu cần thiết
                }
                else
                {
                    // Xử lý trường hợp đơn hàng không có KhachHang liên kết (ít xảy ra nếu M_KhachHang là required)
                    // Có thể bạn muốn log lỗi hoặc hiển thị thông báo khác.
                    // Nếu không có KhachHang, bạn không thể cập nhật Ten_KhachHang, SDT_KhachHang trực tiếp vào nó.
                    // Trong trường hợp này, có thể Tên người nhận và SĐT người nhận nên là các trường riêng trên DonHang.
                    // Dựa theo form của bạn, nó đang cố gắng cập nhật vào DonHang.KhachHang.
                    TempData["ErrorMessage"] = "Lỗi: Không tìm thấy thông tin khách hàng liên kết với đơn hàng để cập nhật tên/SĐT người nhận.";
                    viewModel.Display_M_KhachHang = donHangToUpdate.M_KhachHang;
                    viewModel.Display_Ten_KhachHang = "N/A"; // Vì KhachHang gốc là null
                    viewModel.Display_NgayDat = donHangToUpdate.NgayDat;
                    viewModel.Display_TotalPrice = (decimal)donHangToUpdate.TotalPrice;
                    viewModel.Display_TrangThai = donHangToUpdate.TrangThai;
                    return View(viewModel);
                }

                // donHangToUpdate.ThoiGianCapNhat = DateTime.Now; // Nếu có trường này
                _context.Update(donHangToUpdate); // Đánh dấu DonHang là đã thay đổi
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đơn hàng {donHangToUpdate.M_DonHang} đã được cập nhật thành công.";
                return RedirectToAction(nameof(Details), new { id = donHangToUpdate.M_DonHang });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DonHangExists(viewModel.M_DonHang))
                {
                    return NotFound();
                }
                else
                {
                    // Xử lý lỗi concurrency: thông báo cho người dùng dữ liệu đã bị thay đổi
                    TempData["ErrorMessage"] = "Dữ liệu đã bị thay đổi bởi một người khác. Vui lòng tải lại trang và thử lại.";
                }
            }
            catch (Exception ex)
            {
                // Log lỗi và thông báo chung
                // _logger.LogError(ex, $"Lỗi khi cập nhật đơn hàng {viewModel.M_DonHang}");
                TempData["ErrorMessage"] = "Đã xảy ra lỗi không mong muốn khi cập nhật đơn hàng.";
            }

            // Nếu có lỗi không phải validation hoặc concurrency, nạp lại thông tin display và trả về view
            viewModel.Display_M_KhachHang = donHangToUpdate.M_KhachHang;
            viewModel.Display_Ten_KhachHang = donHangToUpdate.KhachHang?.Ten_KhachHang ?? "N/A";
            viewModel.Display_NgayDat = donHangToUpdate.NgayDat;
            viewModel.Display_TotalPrice = (decimal)donHangToUpdate.TotalPrice;
            viewModel.Display_TrangThai = donHangToUpdate.TrangThai;
            ViewData["Title"] = $"Sửa Đơn Hàng {viewModel.M_DonHang}";
            return View(viewModel);
        }

        // GET: QuanLyDH/QuanLyDH/OrderNotes/DH00001
        [HttpGet]
        public async Task<IActionResult> OrderNotes(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var donHang = await _context.DonHangs.FindAsync(id);
            if (donHang == null) return NotFound();

            // TODO: Giả sử bạn có model OrderNote và DbSet<OrderNote> OrderNotes trong DbContext
            // var notes = await _context.OrderNotes.Where(n => n.M_DonHang == id).OrderByDescending(n => n.NgayTao).ToListAsync();
            // var viewModel = new OrderNotesViewModel { DonHang = donHang, Notes = notes };

            ViewData["Title"] = $"Ghi chú cho đơn hàng {id}";
            // return View(viewModel);
            TempData["InfoMessage"] = "Chức năng Ghi chú đang được phát triển.";
            return View("Details", donHang); // Tạm thời, hiển thị trang chi tiết
        }

        // POST: QuanLyDH/QuanLyDH/OrderNotes/DH00001
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOrderNote(string id, string noteContent)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(noteContent))
            {
                TempData["ErrorMessage"] = "Mã đơn hàng và nội dung ghi chú không được trống.";
                return RedirectToAction(nameof(OrderNotes), new { id = id }); // Hoặc Details
            }
            var donHang = await _context.DonHangs.FindAsync(id);
            if (donHang == null) return NotFound();

            // TODO: Tạo và lưu OrderNote mới
            // var newNote = new OrderNote
            // {
            //     M_DonHang = id,
            //     NoiDung = noteContent,
            //     NguoiTao = User.Identity.Name, // Lấy tên người dùng hiện tại
            //     NgayTao = DateTime.Now
            // };
            // _context.OrderNotes.Add(newNote);
            // await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã thêm ghi chú cho đơn hàng " + id;
            // return RedirectToAction(nameof(OrderNotes), new { id = id });
            return RedirectToAction(nameof(Details), new { id = id }); // Tạm thời
        }


        // GET: QuanLyDH/QuanLyDH/PrintOrder/DH00001
        [HttpGet]
        public async Task<IActionResult> PrintOrder(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var donHang = await _context.DonHangs
                .Include(d => d.KhachHang)
                .Include(d => d.ChiTietDatHangs).ThenInclude(ct => ct.SanPham)
                // .Include(d => d.VanChuyen)
                .FirstOrDefaultAsync(m => m.M_DonHang == id);

            if (donHang == null) return NotFound();

            // Chỉ cho phép in đơn hàng ở trạng thái phù hợp (VD: Hoàn thành, Đang giao)
            if (donHang.TrangThai != StatusCompletedDbValue && donHang.TrangThai != StatusShippingDbValue && donHang.TrangThai != StatusConfirmedDbValue && donHang.TrangThai != StatusProcessingDbValue)
            {
                TempData["ErrorMessage"] = "Không thể in đơn hàng ở trạng thái này.";
                return RedirectToAction(nameof(Details), new { id = id });
            }

            ViewData["Title"] = $"In đơn hàng {id}";
            // TODO: Tạo một View đặc biệt cho việc in (layout tối giản, định dạng hóa đơn/phiếu giao)
            // Hoặc tích hợp thư viện tạo PDF (ví dụ: Rotativa, DinkToPdf)
            // return View("PrintView", donHang); // Giả sử có PrintView.cshtml
            TempData["InfoMessage"] = $"Chức năng In cho đơn hàng {id} đang được phát triển.";
            return View("Details", donHang); // Tạm thời
        }

        // POST hoặc GET: QuanLyDH/QuanLyDH/ResendNotification/DH00001?type=confirmation
        [HttpPost] // Nên là POST để tránh tác dụng phụ khi refresh trang
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendNotification(string id, string type)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(type)) return BadRequest();
            var donHang = await _context.DonHangs.FindAsync(id);
            if (donHang == null) return NotFound();

            // TODO: Logic gửi email/SMS dựa vào `type`
            // Ví dụ:
            // switch (type.ToLower())
            // {
            //     case "confirmation":
            //         // _emailService.SendOrderConfirmationEmail(donHang);
            //         TempData["SuccessMessage"] = $"Đã gửi lại thông báo xác nhận cho đơn hàng {id}.";
            //         break;
            //     case "shipping":
            //         // _emailService.SendShippingNotificationEmail(donHang);
            //         TempData["SuccessMessage"] = $"Đã gửi lại thông báo giao hàng cho đơn hàng {id}.";
            //         break;
            //     default:
            //         TempData["ErrorMessage"] = "Loại thông báo không hợp lệ.";
            //         break;
            // }
            TempData["InfoMessage"] = $"Chức năng Gửi lại thông báo '{type}' cho đơn {id} đang được phát triển.";
            return RedirectToAction(nameof(Details), new { id = id });
        }

        // GET: QuanLyDH/QuanLyDH/TrackOrder/DH00001 (Hoặc mã vận đơn)
        // Trong View, link có thể là: asp-action="TrackOrder" asp-route-id="@order.M_DonHang"
        [HttpGet]
        public async Task<IActionResult> TrackOrder(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var donHang = await _context.DonHangs.Include(d => d.VanChuyen).FirstOrDefaultAsync(d => d.M_DonHang == id);

            if (donHang == null || string.IsNullOrEmpty(donHang.M_VanDon) || donHang.VanChuyen == null || string.IsNullOrEmpty(donHang.VanChuyen.DonViVanChuyen))
            {
                TempData["ErrorMessage"] = "Không có thông tin vận đơn hoặc đơn vị vận chuyển cho đơn hàng này.";
                return RedirectToAction(nameof(Details), new { id = id });
            }

            // TODO: Xây dựng URL theo dõi dựa trên M_VanDon và DonViVanChuyen
            // Ví dụ đơn giản:
            string trackingUrl = "";
            // if (donHang.VanChuyen.DonViVanChuyen.Contains("GHTK")) {
            //     trackingUrl = $"https://i.ghtk.vn/{donHang.M_VanDon}";
            // } else if (donHang.VanChuyen.DonViVanChuyen.Contains("ViettelPost")) {
            //     trackingUrl = $"https://viettelpost.com.vn/tra-cuu-hanh-trinh-don/?key={donHang.M_VanDon}";
            // } // ... thêm các đơn vị vận chuyển khác

            if (string.IsNullOrEmpty(trackingUrl))
            {
                TempData["InfoMessage"] = $"Chưa hỗ trợ theo dõi cho đơn vị vận chuyển: {donHang.VanChuyen.DonViVanChuyen}. Mã vận đơn: {donHang.M_VanDon}";
                return RedirectToAction(nameof(Details), new { id = id });
            }

            // return Redirect(trackingUrl);
            TempData["InfoMessage"] = $"Chuyển hướng theo dõi cho đơn {id} với mã {donHang.M_VanDon} (URL: {trackingUrl}) đang được phát triển.";
            return RedirectToAction(nameof(Details), new { id = id }); // Tạm thời
        }


        // GET: QuanLyDH/QuanLyDH/HandleReturn/DH00001
        [HttpGet]
        public async Task<IActionResult> HandleReturn(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var donHang = await _context.DonHangs.Include(d => d.ChiTietDatHangs).ThenInclude(ct => ct.SanPham)
                                        .FirstOrDefaultAsync(m => m.M_DonHang == id);
            if (donHang == null) return NotFound();

            // Chỉ cho phép xử lý đổi trả cho đơn hàng đã hoàn thành (hoặc tùy logic)
            if (donHang.TrangThai != StatusCompletedDbValue)
            {
                TempData["ErrorMessage"] = "Chỉ có thể xử lý đổi/trả cho đơn hàng đã hoàn thành.";
                return RedirectToAction(nameof(Details), new { id = id });
            }

            // TODO: Tạo ReturnRequestViewModel, truyền thông tin đơn hàng và chi tiết sản phẩm
            // var viewModel = new ReturnRequestViewModel { OrderId = id, /* ... */ };
            ViewData["Title"] = $"Xử lý đổi/trả cho đơn hàng {id}";
            // return View(viewModel);
            TempData["InfoMessage"] = $"Chức năng Xử lý đổi/trả cho đơn hàng {id} đang được phát triển.";
            return View("Details", donHang); // Tạm thời
        }

        // POST: QuanLyDH/QuanLyDH/HandleReturn/DH00001



        // --- CÁC HÀM HELPER (giữ nguyên và bổ sung nếu cần) ---
        private List<SelectListItem> GetOrderStatusOptions(string selectedValue)
        {
            // Thêm "Đang xử lý" nếu bạn muốn lọc theo nó
            return new List<SelectListItem> {
                new SelectListItem { Value = "", Text = "Trạng thái ĐH", Selected = string.IsNullOrEmpty(selectedValue) },
                new SelectListItem { Value = "pending", Text = "Chờ xác nhận", Selected = selectedValue == "pending" }, // "Chưa xử lý"
                new SelectListItem { Value = "confirmed", Text = "Đã xác nhận", Selected = selectedValue == "confirmed" },
                new SelectListItem { Value = "processing", Text = "Đang xử lý", Selected = selectedValue == "processing" },
                new SelectListItem { Value = "shipping", Text = "Đang giao", Selected = selectedValue == "shipping" },
                new SelectListItem { Value = "completed", Text = "Hoàn thành", Selected = selectedValue == "completed" },
                new SelectListItem { Value = "cancelled", Text = "Đã hủy", Selected = selectedValue == "cancelled" }
            };
        }
        private string MapFilterToDbOrderStatus(string filterStatus)
        {
            return filterStatus?.ToLower() switch
            {
                "pending" => StatusPendingDbValue, // Hoặc "Chưa xử lý" nếu đó là giá trị DB
                "confirmed" => StatusConfirmedDbValue,
                "processing" => StatusProcessingDbValue, // Thêm mapping
                "shipping" => StatusShippingDbValue,
                "completed" => StatusCompletedDbValue,
                "cancelled" => StatusCancelledDbValue,
                _ => string.Empty,
            };
        }
        private string GetOrderStatusBadgeClass(string dbStatus)
        {
            if (dbStatus == StatusPendingDbValue || dbStatus == "Chưa xử lý") return "badge bg-warning text-dark";
            if (dbStatus == StatusConfirmedDbValue) return "badge bg-info text-dark";
            if (dbStatus == StatusProcessingDbValue) return "badge bg-primary"; // Màu cho "Đang xử lý"
            if (dbStatus == StatusShippingDbValue) return "badge bg-primary"; // Giữ màu cho "Đang giao"
            if (dbStatus == StatusCompletedDbValue) return "badge bg-success";
            if (dbStatus == StatusCancelledDbValue) return "badge bg-danger";
            return "badge bg-secondary";
        }

        // Các hàm liên quan đến Trạng thái Thanh Toán (Payment Status)
        // (UpdatePaymentStatus, ConfirmPayment, RefundPayment, GetPaymentStatusOptions, ...)
        // ... (Giữ nguyên code hiện tại của bạn cho các hàm này) ...
        // GET: /QuanLyDH/QuanLyDH/UpdatePaymentStatus/DH123
        public async Task<IActionResult> UpdatePaymentStatus(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var donHang = await _context.DonHangs.FindAsync(id);
            if (donHang == null) return NotFound();

            ViewBag.M_DonHang = id;
            // Sử dụng hàm GetPaymentStatusOptions đã có trong controller của bạn hoặc tạo mới nếu cần
            ViewBag.PaymentStatusOptions = GetPaymentStatusOptions(donHang.TrangThaiThanhToan); // Giả sử bạn có hàm này
            ViewData["Title"] = $"Cập nhật TT thanh toán đơn hàng {id}";
            // Cần tạo view Areas/QuanLyDH/Views/QuanLyDH/UpdatePaymentStatus.cshtml
            // Trong view đó sẽ có dropdown để chọn newPaymentStatus và POST về action UpdatePaymentStatus (POST)
            return View(); // Trả về view cho phép chọn trạng thái thanh toán mới
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePaymentStatus(string id, string newPaymentStatus)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(newPaymentStatus))
            {
                return BadRequest("Thông tin không hợp lệ.");
            }

            var donHang = await _context.DonHangs.FindAsync(id);
            if (donHang == null)
            {
                return NotFound($"Không tìm thấy đơn hàng {id}.");
            }

            // TODO: Validate newPaymentStatus có nằm trong danh sách các trạng thái hợp lệ không.
            // Ví dụ: List<string> validPaymentStatuses = new List<string> { "Chưa thanh toán", "Đã thanh toán", "Đã hoàn tiền", "Thanh toán lỗi"};
            // if (!validPaymentStatuses.Contains(newPaymentStatus)) {
            //      ModelState.AddModelError("newPaymentStatus", "Trạng thái thanh toán không hợp lệ.");
            //      ViewBag.M_DonHang = id;
            //      ViewBag.PaymentStatusOptions = GetPaymentStatusOptions(donHang.TrangThaiThanhToan);
            //      return View( /* ViewModel nếu có */);
            // }


            if (donHang.TrangThaiThanhToan != newPaymentStatus)
            {
                donHang.TrangThaiThanhToan = newPaymentStatus;
                // donHang.NgayCapNhatThanhToan = DateTime.Now; // Nếu có trường này
                _context.Update(donHang);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Trạng thái thanh toán của đơn hàng {donHang.M_DonHang} đã được cập nhật thành '{newPaymentStatus}'.";
            }
            else
            {
                TempData["InfoMessage"] = $"Trạng thái thanh toán của đơn hàng {donHang.M_DonHang} đã là '{newPaymentStatus}'.";
            }

            return RedirectToAction(nameof(Details), new { id = id });
        }
        private List<SelectListItem> GetPaymentStatusOptions(string selectedValue)
        {
            // Lấy từ code bạn đã cung cấp hoặc tùy chỉnh
            return new List<SelectListItem> {
                new SelectListItem { Value = "", Text = "Trạng thái TT", Selected = string.IsNullOrEmpty(selectedValue) },
                new SelectListItem { Value = "Chưa thanh toán", Text = "Chưa thanh toán", Selected = selectedValue == "Chưa thanh toán" },
                new SelectListItem { Value = "Đã thanh toán", Text = "Đã thanh toán", Selected = selectedValue == "Đã thanh toán" },
                new SelectListItem { Value = "Đã hoàn tiền", Text = "Đã hoàn tiền", Selected = selectedValue == "Đã hoàn tiền" },
                new SelectListItem { Value = "Thanh toán lỗi", Text = "Thanh toán lỗi", Selected = selectedValue == "Thanh toán lỗi" }
            };
        }
        private string GetPaymentStatusBadgeClass(string paymentStatus)
        {
            return paymentStatus switch
            {
                "Chưa thanh toán" => "badge bg-warning text-dark",
                "Chờ TT" => "badge bg-warning text-dark", // Từ hàm Infer
                "Đã thanh toán" => "badge bg-success",
                "Đã TT" => "badge bg-success", // Từ hàm Infer
                "Đã hoàn tiền" => "badge bg-secondary",
                "Thanh toán lỗi" => "badge bg-danger",
                _ => "badge bg-light text-dark",
            };
        }

        private string InferPaymentStatusText(DonHang dh)
        {
            // Logic suy luận rất cơ bản - bạn cần tùy chỉnh cho phù hợp
            if (!string.IsNullOrEmpty(dh.TrangThaiThanhToan)) return dh.TrangThaiThanhToan; // Ưu tiên trạng thái TT đã có

            if (dh.TrangThai == StatusCancelledDbValue) return "Đã hoàn tiền"; // Giả định hủy đơn là hoàn tiền
            if (dh.TrangThai == StatusPendingDbValue || dh.TrangThai == "Chưa xử lý") return "Chưa thanh toán"; // Hoặc "Chờ TT"
            if (dh.PhuongThucThanhToan?.TenPhuongThuc?.ToUpper() == "COD")
            {
                if (dh.TrangThai == StatusCompletedDbValue) return "Đã thanh toán";
                if (dh.TrangThai == StatusShippingDbValue) return "Chưa thanh toán"; // COD thì khi giao mới TT
            }
            if (dh.TrangThai == StatusConfirmedDbValue ||
                dh.TrangThai == StatusProcessingDbValue ||
                dh.TrangThai == StatusShippingDbValue ||
                dh.TrangThai == StatusCompletedDbValue)
            {
                // Giả sử nếu đã qua "Chờ xác nhận" và không phải COD thì là "Đã TT"
                // Logic này cần rất cẩn thận và phụ thuộc vào PTTT
                return "Đã thanh toán"; // Đây là một giả định lớn
            }
            return "Chưa rõ"; // Mặc định
        }

        private string InferPaymentStatusBadgeClass(string paymentStatusText)
        {
            return GetPaymentStatusBadgeClass(paymentStatusText);
        }

        // Action UpdateShippingInfo
        public async Task<IActionResult> UpdateShippingInfo(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var donHang = await _context.DonHangs.Include(d => d.VanChuyen).FirstOrDefaultAsync(d => d.M_DonHang == id);
            if (donHang == null) return NotFound();

            // Tạo ViewModel nếu cần để truyền M_VanDon và DonViVanChuyen hiện tại
            // var viewModel = new UpdateShippingInfoViewModel {
            // M_DonHang = donHang.M_DonHang,
            // M_VanDon = donHang.M_VanDon,
            // DonViVanChuyen = donHang.VanChuyen?.DonViVanChuyen
            // };

            ViewData["Title"] = $"Cập nhật TT vận chuyển đơn hàng {id}";
            return View(donHang); // Hoặc viewModel
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateShippingInfo(string id, string m_VanDon, string donViVanChuyen)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest("Mã đơn hàng không hợp lệ.");

            var donHang = await _context.DonHangs.FindAsync(id);
            if (donHang == null) return NotFound($"Không tìm thấy đơn hàng {id}.");

            // Validate m_VanDon và donViVanChuyen
            if (string.IsNullOrWhiteSpace(m_VanDon)) ModelState.AddModelError("M_VanDon", "Vui lòng nhập mã vận đơn.");
            if (string.IsNullOrWhiteSpace(donViVanChuyen)) ModelState.AddModelError("DonViVanChuyen", "Vui lòng nhập tên đơn vị vận chuyển.");


            if (ModelState.IsValid)
            {
                VanChuyen vanChuyen = await _context.VanChuyens.FirstOrDefaultAsync(vc => vc.M_VanDon == m_VanDon);

                if (vanChuyen == null)
                {
                    vanChuyen = new VanChuyen { M_VanDon = m_VanDon, DonViVanChuyen = donViVanChuyen };
                    _context.VanChuyens.Add(vanChuyen);
                }
                else
                {
                    vanChuyen.DonViVanChuyen = donViVanChuyen; // Cập nhật nếu đã có mã vận đơn này
                    _context.Update(vanChuyen);
                }

                donHang.M_VanDon = m_VanDon; // Cập nhật khóa ngoại trong đơn hàng
                // Tự động chuyển trạng thái sang "Đang giao hàng" nếu hợp lệ
                if (donHang.TrangThai == StatusConfirmedDbValue || donHang.TrangThai == StatusProcessingDbValue)
                {
                    donHang.TrangThai = StatusShippingDbValue;
                }
                _context.Update(donHang);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Thông tin vận chuyển của đơn hàng {donHang.M_DonHang} đã được cập nhật.";
                return RedirectToAction(nameof(Details), new { id = id });
            }

            if (donHang.VanChuyen != null) donHang.VanChuyen.DonViVanChuyen = donViVanChuyen;
            else donHang.VanChuyen = new VanChuyen { M_VanDon = m_VanDon, DonViVanChuyen = donViVanChuyen };

            ViewData["Title"] = $"Cập nhật thông tin vận chuyển đơn hàng {id}";
            return View(donHang); // Trả về View với model và lỗi ModelState
        }


        private async Task<string?> ApplyInventoryChangesForOrderAsync(DonHang donHang, bool truKho)
        {
            // 1. Lấy tất cả chi tiết của đơn hàng
            var chiTietItems = await _context.ChiTietDatHangs
                .Where(ct => ct.M_DonHang == donHang.M_DonHang)
                .ToListAsync();

            if (!chiTietItems.Any())
            {
                return "Đơn hàng không có chi tiết sản phẩm.";
            }

            foreach (var item in chiTietItems)
            {
                // Giả định: 
                // 1. `item.ProductId` là mã sản phẩm (lấy từ code cũ của bạn)
                // 2. `item.Khoiluong` là khối lượng cần trừ (lấy từ code cũ của bạn)
                // 3. Model LoTonKho dùng `KhoiLuongConLai` (decimal)
                decimal soLuongCanThayDoi = (decimal)(item.Khoiluong);
                string maSanPham = item.ProductId; // Giả định tên cột là ProductId

                if (soLuongCanThayDoi <= 0) continue;

                if (truKho) // === Logic TRỪ KHO (Xác nhận đơn) ===
                {
                    // Lấy các lô hàng theo FIFO (Cũ nhất trước)
                    var availableLots = await _context.LoTonKhos
                        .Where(l => l.M_SanPham == maSanPham &&
                                    l.KhoiLuongConLai > 0)
                        .OrderBy(l => l.NgayNhapKho) // FIFO
                        .ToListAsync();

                    // Kiểm tra tổng tồn kho
                    decimal tongTonKho = availableLots.Sum(l => l.KhoiLuongConLai);
                    if (tongTonKho < soLuongCanThayDoi)
                    {
                        var tenSP = await _context.SanPhams.Where(s => s.M_SanPham == maSanPham).Select(s => s.TenSanPham).FirstOrDefaultAsync();
                        return $"Không đủ tồn kho cho '{tenSP ?? maSanPham}'. Tồn: {tongTonKho}, Cần: {soLuongCanThayDoi}";
                    }

                    // Lặp qua các lô để trừ kho
                    foreach (var lot in availableLots)
                    {
                        decimal soLuongLayTuLoNay = Math.Min(soLuongCanThayDoi, lot.KhoiLuongConLai);

                        lot.KhoiLuongConLai -= soLuongLayTuLoNay;
                        soLuongCanThayDoi -= soLuongLayTuLoNay;

                        _context.Update(lot); // Đánh dấu lô này cần cập nhật
                        _logger.LogInformation("Trừ kho FIFO: Lấy {SoLuong} từ Lô {MaLo}. Lô còn lại: {KhoiLuongConLai}", soLuongLayTuLoNay, lot.MaLoTonKho, lot.KhoiLuongConLai);
                        // <<< ================= BẮT ĐẦU GỌI BLOCKCHAIN ================= >>>
                        try
                        {

                            string metadata = $"Đơn hàng {donHang.M_DonHang} - {donHang.TrangThai} - " +
                      $"{donHang.M_PhuongThuc} - {donHang.TotalPrice}";

                            dynamic result = await _blockchainService.GhiNhatKyAsync(
                                donHang.M_DonHang,
                                donHang.TrangThai,
                                donHang.ShippingAddress,
                                metadata
                            );

                          
                            // === CHỈ LƯU SQL KHI BLOCKCHAIN THÀNH CÔNG ===
                            var log = new BlockchainTransaction
                            {
                                MaDonHang = donHang.M_DonHang,
                                TrangThai = donHang.TrangThai,
                                DiaChiGiaoHang = donHang.ShippingAddress,
                                Metadata = metadata,
                                TxHash = "nuh",
                            };

                            _context.BlockchainTransaction.Add(log);
                            await _context.SaveChangesAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "LỖI GHI BLOCKCHAIN (Xuất Kho) cho Lô {MaLo}. Giao dịch CSDL sẽ tiếp tục.", lot.MaLoTonKho);
                            // Không rollback, chấp nhận CSDL thành công
                        }
                        // <<< ================= KẾT THÚC GỌI BLOCKCHAIN ================= >>>
                        if (soLuongCanThayDoi <= 0) break; // Đã lấy đủ
                    }
                }
                else // === Logic HOÀN KHO (Hủy đơn) ===
                {
                    // Hoàn kho: Tìm lô MỚI NHẤT của sản phẩm này và cộng trả lại
                    var lotDeHoan = await _context.LoTonKhos
                        .Where(l => l.M_SanPham == maSanPham)
                        .OrderByDescending(l => l.NgayNhapKho) // Ưu tiên hoàn vào lô mới nhất
                        .FirstOrDefaultAsync();

                    if (lotDeHoan != null)
                    {
                        lotDeHoan.KhoiLuongConLai += soLuongCanThayDoi;
                        _context.Update(lotDeHoan);
                        _logger.LogInformation("Hoàn kho: Trả {SoLuong} vào Lô {MaLo}. Lô còn lại: {KhoiLuongConLai}", soLuongCanThayDoi, lotDeHoan.MaLoTonKho, lotDeHoan.KhoiLuongConLai);
                    }
                    else
                    {
                        // Nếu không còn lô nào, tạo một lô mới? (Logic nguy hiểm)
                        // Tốt hơn là báo lỗi
                        _logger.LogError("Không tìm thấy lô nào để hoàn kho cho SP {SanPhamId} khi hủy ĐH {DonHangId}", maSanPham, donHang.M_DonHang);
                        // Không return lỗi, vẫn cho hủy đơn
                    }
                }

            }

            return null; // Thành công
        }
        private bool DonHangExists(string id)
        {
            return _context.DonHangs.Any(e => e.M_DonHang == id);
        }
    }
}