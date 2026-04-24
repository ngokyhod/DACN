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
using System.Collections.Generic; // For List

namespace DACS.Areas.Owner.Controllers
{
    [Area("Owner")]
    [Authorize(Roles = "Owner")]
    public class QuanLyDonHangController : Controller
    {
        private readonly ApplicationDbContext _context;

        // --- ĐỊNH NGHĨA CÁC CHUỖI TRẠNG THÁI ĐƠN HÀNG TRONG DATABASE ---
        private const string StatusPendingDbValue = "Chờ xác nhận"; // Hoặc "Chưa xử lý" nếu bạn dùng nó làm giá trị DB ban đầu
        private const string StatusConfirmedDbValue = "Đã xác nhận";
        private const string StatusProcessingDbValue = "Đang xử lý"; // Đã có
        private const string StatusShippingDbValue = "Đang giao hàng";
        private const string StatusCompletedDbValue = "Hoàn thành";
        private const string StatusCancelledDbValue = "Đã hủy";
        // --- KẾT THÚC ĐỊNH NGHĨA ---

        public QuanLyDonHangController(ApplicationDbContext context)
        {
            _context = context;
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

            var viewModel = new OrderManagementViewModel
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

            var donHang = await _context.DonHangs.FirstOrDefaultAsync(m => m.M_DonHang == id);
            if (donHang == null)
                return NotFound($"Không tìm thấy đơn hàng {id}.");

            string actualNewStatusInDb = "";
            string successMessagePart = "";
            bool canUpdate = false;

            switch (newStatusKey.ToLower())
            {
                case "confirm":
                    if (donHang.TrangThai == StatusPendingDbValue || donHang.TrangThai == "Chưa xử lý") // Cho phép từ "Chưa xử lý"
                    {
                        actualNewStatusInDb = StatusConfirmedDbValue;
                        successMessagePart = "xác nhận.";
                        canUpdate = true;
                    }
                    break;
                case "process": // THÊM CASE "PROCESS"
                    if (donHang.TrangThai == StatusConfirmedDbValue)
                    {
                        actualNewStatusInDb = StatusProcessingDbValue;
                        successMessagePart = "chuyển sang đang xử lý.";
                        canUpdate = true;
                        // TODO: Có thể có logic thêm ở đây (ví dụ: kiểm tra tồn kho,...)
                    }
                    break;
                case "ship":
                    if (donHang.TrangThai == StatusConfirmedDbValue || donHang.TrangThai == StatusProcessingDbValue)
                    {
                        // TODO: Kiểm tra xem đã có thông tin vận đơn (M_VanDon) chưa? Nếu chưa có thể không cho ship hoặc chuyển tới trang nhập TT vận đơn.
                        // For now, we assume it can be shipped.
                        actualNewStatusInDb = StatusShippingDbValue;
                        successMessagePart = "chuyển sang đang giao.";
                        canUpdate = true;
                    }
                    break;
                case "complete":
                    if (donHang.TrangThai == StatusShippingDbValue)
                    {
                        actualNewStatusInDb = StatusCompletedDbValue;
                        successMessagePart = "hoàn thành.";
                        canUpdate = true;
                    }
                    break;
                case "cancel":
                    // Cho phép hủy từ nhiều trạng thái hơn (tùy theo logic nghiệp vụ)
                    if (donHang.TrangThai == StatusPendingDbValue || donHang.TrangThai == "Chưa xử lý" || donHang.TrangThai == StatusConfirmedDbValue)
                    {
                        actualNewStatusInDb = StatusCancelledDbValue;
                        successMessagePart = "hủy.";
                        canUpdate = true;
                        // TODO: Logic khi hủy (VD: hoàn kho, hoàn tiền nếu đã thanh toán,...)
                        // Nếu đã thanh toán, cân nhắc việc chuyển trạng thái thanh toán sang "Đã hoàn tiền"
                        if (!string.IsNullOrEmpty(donHang.TrangThaiThanhToan) && donHang.TrangThaiThanhToan == "Đã thanh toán")
                        {
                            // donHang.TrangThaiThanhToan = "Đã hoàn tiền"; // Cần xem xét kỹ
                        }
                    }
                    break;
            }

            if (canUpdate && !string.IsNullOrEmpty(actualNewStatusInDb))
            {
                donHang.TrangThai = actualNewStatusInDb;
                _context.Update(donHang);
                await _context.SaveChangesAsync();

                var chiTietItems = await _context.ChiTietDatHangs
                              .Where(ct => ct.M_DonHang == donHang.M_DonHang)
                              .ToListAsync();
                foreach (var item in chiTietItems)
                {
                    item.TrangThaiDonHang = actualNewStatusInDb;
                    _context.Update(item);
                }
                await _context.SaveChangesAsync();

                // TRỪ TỒN KHO khi đơn hoàn thành (FIFO, chống trừ 2 lần)
                if (actualNewStatusInDb == StatusCompletedDbValue && !donHang.DaTruTonKho)
                {
                    try
                    {
                        foreach (var item in chiTietItems)
                        {
                            decimal soLuongCanTru = (decimal)item.Khoiluong;
                            var cacLoHang = await _context.LoTonKhos
                                .Where(l => l.M_SanPham == item.M_SanPham && l.KhoiLuongConLai > 0)
                                .OrderBy(l => l.NgayNhapKho)
                                .ToListAsync();

                            foreach (var lo in cacLoHang)
                            {
                                if (soLuongCanTru <= 0) break;
                                decimal layDuoc = Math.Min(soLuongCanTru, lo.KhoiLuongConLai);
                                lo.KhoiLuongConLai -= layDuoc;
                                soLuongCanTru -= layDuoc;
                                _context.LoTonKhos.Update(lo);
                            }
                        }
                        donHang.DaTruTonKho = true;
                        _context.Update(donHang);
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        // Log lỗi nhưng không rollback trạng thái đơn hàng
                        TempData["WarningMessage"] = $"Đơn hàng đã hoàn thành nhưng có lỗi khi trừ tồn kho: {ex.Message}";
                    }
                }

                TempData["SuccessMessage"] = $"Đơn hàng {donHang.M_DonHang} đã được {successMessagePart}";
            }
            else
            {
                TempData["ErrorMessage"] = $"Không thể cập nhật trạng thái cho đơn hàng {donHang.M_DonHang} từ '{donHang.TrangThai}' sang '{newStatusKey}'.";
            }
            return RedirectToAction(nameof(Index)); // Hoặc RedirectToAction(nameof(Details), new { id = id });
        }

        // --- CÁC ACTION METHOD MỚI ---

        // GET: QuanLyDH/QuanLyDH/EditOrder/DH00001
        [HttpGet]
        public async Task<IActionResult> EditOrder(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var donHang = await _context.DonHangs
                                .Include(d => d.KhachHang)
                                // .Include(d => d.ChiTietDatHangs).ThenInclude(ct => ct.SanPham) // Load chi tiết nếu cần sửa sản phẩm
                                .FirstOrDefaultAsync(m => m.M_DonHang == id);

            if (donHang == null) return NotFound();

            // Chỉ cho phép sửa nếu đơn hàng ở trạng thái phù hợp (ví dụ: "Chờ xác nhận")
            if (donHang.TrangThai != StatusPendingDbValue && donHang.TrangThai != "Chưa xử lý")
            {
                TempData["ErrorMessage"] = "Không thể sửa đơn hàng ở trạng thái này.";
                return RedirectToAction(nameof(Details), new { id = id });
            }

            // TODO: Tạo một EditOrderViewModel nếu cần, map DonHang sang ViewModel đó
            // var viewModel = new EditOrderViewModel { /* ... map properties ... */ };
            ViewData["Title"] = $"Sửa đơn hàng {id}";
            return View(donHang); // Hoặc View(viewModel);
        }

        // POST: QuanLyDH/QuanLyDH/EditOrder/DH00001
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditOrder(string id, [Bind("M_DonHang,ShippingAddress,Notes,KhachHang.Ten_KhachHang,SDTNguoiNhan")] DonHang editedDonHang) // Bind các thuộc tính cho phép sửa
        {
            if (id != editedDonHang.M_DonHang) return NotFound();

            var donHangToUpdate = await _context.DonHangs.FindAsync(id);
            if (donHangToUpdate == null) return NotFound();

            // Chỉ cho phép sửa nếu đơn hàng ở trạng thái phù hợp
            if (donHangToUpdate.TrangThai != StatusPendingDbValue && donHangToUpdate.TrangThai != "Chưa xử lý")
            {
                TempData["ErrorMessage"] = "Không thể sửa đơn hàng ở trạng thái này. Trạng thái hiện tại: " + donHangToUpdate.TrangThai;
                // TODO: Load lại view EditOrder với model và lỗi
                return View(editedDonHang); // Hoặc tạo ViewModel và trả về
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // TODO: Cập nhật các thuộc tính được phép của donHangToUpdate từ editedDonHang
                    // Ví dụ:
                    donHangToUpdate.ShippingAddress = editedDonHang.ShippingAddress;
                    donHangToUpdate.Notes = editedDonHang.Notes;
                    donHangToUpdate.KhachHang.Ten_KhachHang = editedDonHang.KhachHang.Ten_KhachHang;
                    donHangToUpdate.KhachHang.SDT_KhachHang = editedDonHang.KhachHang.SDT_KhachHang;
                    // ... các trường khác bạn cho phép sửa ...
                    // KHÔNG cho phép sửa các trường nhạy cảm như TotalPrice trực tiếp ở đây trừ khi có logic tính toán lại.

                    // donHangToUpdate.ThoiGianCapNhat = DateTime.Now;
                    _context.Update(donHangToUpdate);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Đơn hàng {id} đã được cập nhật.";
                    return RedirectToAction(nameof(Details), new { id = id });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DonHangExists(donHangToUpdate.M_DonHang)) return NotFound();
                    else throw;
                }
            }
            ViewData["Title"] = $"Sửa đơn hàng {id}";
            return View(editedDonHang); // Trả về view với model đã nhập nếu có lỗi validation
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

            // Nếu ModelState không hợp lệ, load lại view với thông tin đã nhập và lỗi
            // Cần truyền lại donHang (hoặc ViewModel) để hiển thị form với giá trị cũ
            donHang.M_VanDon = m_VanDon; // Để hiển thị lại giá trị đã nhập
            if (donHang.VanChuyen != null) donHang.VanChuyen.DonViVanChuyen = donViVanChuyen;
            else donHang.VanChuyen = new VanChuyen { M_VanDon = m_VanDon, DonViVanChuyen = donViVanChuyen };

            ViewData["Title"] = $"Cập nhật thông tin vận chuyển đơn hàng {id}";
            return View(donHang); // Trả về View với model và lỗi ModelState
        }


        private bool DonHangExists(string id)
        {
            return _context.DonHangs.Any(e => e.M_DonHang == id);
        }
    }
}