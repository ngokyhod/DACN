using DACS.Extention;
using DACS.Models;
using DACS.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YourNameSpace.Extensions;
using DACS.Services;
using DACS.PTTT;
using Google.Cloud.Firestore;

namespace DACS.Controllers
{
    [Authorize]
    public class ShoppingCartController : Controller
    {
        private readonly ISanPhamRepository _productRepository;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ShoppingCartController> _logger;
        private readonly IEmailService _emailService;
        private readonly ISmsService _smsService;
        private readonly FirebaseSyncService _firebaseSync;

        // <<< THÊM SERVICE TRUY XUẤT NGUỒN GỐC >>>
        private readonly TraceabilityService _traceabilityService;

        public ShoppingCartController(ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ISanPhamRepository productRepository,
            ILogger<ShoppingCartController> logger,
            IEmailService emailService,
            ISmsService smsService,
            FirebaseSyncService firebaseSync,
            TraceabilityService traceabilityService) // <<< INJECT VÀO CONSTRUCTOR >>>
        {
            _productRepository = productRepository;
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _emailService = emailService;
            _smsService = smsService;
            _firebaseSync = firebaseSync;
            _traceabilityService = traceabilityService; // <<< GÁN BIẾN >>>
        }

        public IActionResult Checkout()
        {
            return View(new DonHang());
        }
        public IActionResult VNpayRedirect()
        {
            return View(new DonHang());
        }

        [HttpPost]
        public async Task<IActionResult> Checkout(DonHang order)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            if (cart == null || !cart.Items.Any())
            {
                return RedirectToAction("Index");
            }

            var user = await _userManager.GetUserAsync(User);
            var nguoiMuaProfile = await _context.KhachHangs.FirstOrDefaultAsync(kh => kh.UserId == user.Id);
            if (nguoiMuaProfile == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy hồ sơ khách hàng của bạn.";
                return RedirectToAction("Index");
            }

            var errorMessages = new List<string>();

            // <<< ================= KIỂM TRA LỖI TỒN KHO ================= >>>
            foreach (var item in cart.Items)
            {
                var tongTonKho = await _context.LoTonKhos
                    .Where(t => t.M_SanPham == item.ProductId)
                    .SumAsync(t => t.KhoiLuongConLai);

                if ((float)item.Khoiluong > (float)tongTonKho)
                {
                    errorMessages.Add($"Sản phẩm '{item.Name}' chỉ còn {tongTonKho:N0}kg, bạn đặt {item.Khoiluong:N0}kg.");
                }
            }

            if (errorMessages.Any())
            {
                ViewBag.CartErrors = errorMessages;
                return View("Checkout", order);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // ======= TẠO MÃ VẬN ĐƠN ========
                string vanDonId = "VD" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                var vanChuyenExist = await _context.VanChuyens.FirstOrDefaultAsync(vc => vc.M_VanDon == vanDonId);
                if (vanChuyenExist == null)
                {
                    vanChuyenExist = new VanChuyen
                    {
                        M_VanDon = vanDonId,
                        DonViVanChuyen = "DHL"
                    };
                    _context.VanChuyens.Add(vanChuyenExist);
                    await _context.SaveChangesAsync();
                }

                // ======= TẠO MÃ ĐƠN HÀNG ========
                var lastOrder = await _context.DonHangs.OrderByDescending(o => o.M_DonHang).FirstOrDefaultAsync();
                int nextNumber = 1;
                if (lastOrder != null && lastOrder.M_DonHang.StartsWith("DH"))
                {
                    var numberPart = lastOrder.M_DonHang.Substring(2);
                    if (int.TryParse(numberPart, out int parsedNumber))
                    {
                        nextNumber = parsedNumber + 1;
                    }
                }

                order.M_DonHang = "DH" + nextNumber.ToString("D6");
                order.M_VanDon = vanChuyenExist.M_VanDon;
                order.TrangThai = order.TrangThai ?? "Chờ xác nhận";
                order.M_KhachHang = nguoiMuaProfile.M_KhachHang;
                order.NgayDat = DateTime.UtcNow;
                order.TotalPrice = cart.Items.Sum(i => i.Price * i.Khoiluong);
                order.TrangThaiThanhToan = "Chưa thanh toán";

                _context.DonHangs.Add(order);
                await _context.SaveChangesAsync();

                // ======= CHI TIẾT ĐƠN HÀNG ========
                order.ChiTietDatHangs = cart.Items.Select(i => new ChiTietDatHang
                {
                    M_KhachHang = nguoiMuaProfile.M_KhachHang,
                    M_DonHang = order.M_DonHang,
                    M_SanPham = i.ProductId,
                    ProductId = i.ProductId,
                    Khoiluong = i.Khoiluong,
                    GiaDatHang = i.Price,
                    M_CTDatHang = "CT" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                    TongTien = (long)(i.Price * i.Khoiluong),
                    NgayTao = DateTime.UtcNow,
                    Quantity = i.Quantity,
                    TrangThaiDonHang = "Chờ xác nhận"
                }).ToList();

                _context.ChiTietDatHangs.AddRange(order.ChiTietDatHangs);
                await _context.SaveChangesAsync();

                try
                {
                    // A. Chuẩn bị danh sách Items (Sản phẩm)
                    var itemsList = new List<Dictionary<string, object>>();

                    foreach (var item in cart.Items)
                    {
                        var imgUrl = await _context.SanPhams
                            .Where(a => a.M_SanPham == item.ProductId)
                            .Select(a => a.AnhSanPham)
                            .FirstOrDefaultAsync();

                        string fullImgUrl = string.IsNullOrEmpty(imgUrl)
                            ? ""
                            : (imgUrl.StartsWith("http") ? imgUrl : $"{Request.Scheme}://{Request.Host}{imgUrl}");

                        itemsList.Add(new Dictionary<string, object>
                        {
                            { "productId", item.ProductId },
                            { "productName", item.Name },
                            { "quantity", item.Khoiluong },
                            { "price", item.Price },
                            { "imageUrl", fullImgUrl }
                        });
                    }

                    // B. Đóng gói dữ liệu Đơn hàng (Firebase)
                    var firestoreData = new Dictionary<string, object>
                    {
                        { "uid", nguoiMuaProfile.FirebaseID ?? "" },
                        { "maDonHang", order.M_DonHang },
                        { "ngayDat", Timestamp.GetCurrentTimestamp() },
                        { "trangThai", "Chờ xác nhận" },
                        { "trangThaiThanhToan", "Chưa thanh toán" },
                        { "tenNguoiNhan", order.Tendathang },
                        { "sdtNguoiNhan", order.SoDienThoaidathang },
                        { "diaChiGiaoHang", order.ShippingAddress },
                        { "ghiChu", order.Notes ?? "" },
                        { "phuongThucTT", order.M_PhuongThuc },
                        { "tongTien", order.TotalPrice },
                        { "items", itemsList }
                    };

                    // C. Gọi Service đẩy lên Firebase
                    if (!string.IsNullOrEmpty(nguoiMuaProfile.FirebaseID))
                    {
                        await _firebaseSync.AddDonHangToFirestoreAsync(firestoreData);
                    }
                }
                catch (Exception fireEx)
                {
                    Console.WriteLine($"[Lỗi Đồng bộ Firebase]: {fireEx.Message}");
                }

                await transaction.CommitAsync(); // Hoàn thành

                // <<< ================= BẮT ĐẦU GHI BLOCKCHAIN (BƯỚC ĐẶT HÀNG) ================= >>>
                try
                {
                    // Lấy tên khách hàng từ Profile hoặc Account
                    string tenKhachHang = nguoiMuaProfile.Ten_KhachHang ?? user.FullName ?? user.UserName ?? "Khách hàng hệ thống";

                    await _traceabilityService.GhiNhatKyAsync(
                        order.M_DonHang,                           // Mã Yêu Cầu / Đơn hàng
                        "Khách hàng đặt đơn",                      // Hành động
                        tenKhachHang,                              // Người thực hiện
                        "Website mua sắm",                         // Vị trí
                        $"Đã đặt mua {cart.Items.Count} loại sản phẩm. Tổng tiền: {order.TotalPrice:N0} đ" // Chi tiết
                    );
                }
                catch (Exception bcEx)
                {
                    _logger.LogError(bcEx, "Lỗi ghi Blockchain khi đặt đơn hàng {DonHangId}", order.M_DonHang);
                }
                // <<< ================= KẾT THÚC GHI BLOCKCHAIN ================= >>>

                // <<< ============ GỬI EMAIL SAU KHI MUA HÀNG THÀNH CÔNG ============ >>>
                try
                {
                    var subject = $"Xác nhận đơn hàng #{order.M_DonHang}";
                    var body = $@"
                        <h1>Cảm ơn bạn đã mua hàng!</h1>
                        <p>Chào {user.FullName ?? user.UserName},</p>
                        <p>Đơn hàng <strong>#{order.M_DonHang}</strong> của bạn đã được tiếp nhận và đang chờ xử lý.</p>
                        <p><strong>Tổng giá trị đơn hàng:</strong> {order.TotalPrice:N0} VNĐ</p>
                        <p><strong>Địa chỉ giao hàng:</strong> {order.ShippingAddress},{order.SoDienThoaidathang}</p>
                        <p>Chúng tôi sẽ liên hệ với bạn sớm nhất.</p>
                        <p>Trân trọng,</p>
                        <p>Đội ngũ Nông Sản Sạch</p>";

                    await _emailService.SendEmailAsync(user.Email, subject, body);
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Lỗi khi gửi email xác nhận đơn hàng {DonHangId}", order.M_DonHang);
                }

                // <<< ============ GỬI SMS SAU KHI MUA HÀNG ============ >>>
                try
                {
                    if (!string.IsNullOrEmpty(order.SoDienThoaidathang))
                    {
                        var smsMessage = $"Cam on ban da mua hang. Don hang #{order.M_DonHang} da duoc tiep nhan.";
                        await _smsService.SendSmsAsync(order.SoDienThoaidathang, smsMessage);
                    }
                }
                catch (Exception smsEx)
                {
                    _logger.LogError(smsEx, "Lỗi khi gửi SMS cho đơn hàng {DonHangId}", order.M_DonHang);
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi nghiêm trọng khi Checkout ĐH {DonHangId}", order.M_DonHang);
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tạo đơn hàng. Vui lòng thử lại.";
                return View("Index", cart);
            }

            if (order.M_PhuongThuc == "PT005") // THANH TOÁN VNPAY
            {
                string vnp_Returnurl = "https://localhost:7240/ShoppingCart/VnpayReturn";
                string vnp_Url = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
                string vnp_TmnCode = "ODJ7F1TO";
                string vnp_HashSecret = "GEHJ4HI0BPW6D8C6DZG42TF9DZ2L5ZUI";

                VnPayLibrary vnpay = new VnPayLibrary();

                vnpay.AddRequestData("vnp_Version", "2.1.0");
                vnpay.AddRequestData("vnp_Command", "pay");
                vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);
                vnpay.AddRequestData("vnp_Amount", ((long)(order.TotalPrice * 100)).ToString());
                vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
                vnpay.AddRequestData("vnp_CurrCode", "VND");
                vnpay.AddRequestData("vnp_IpAddr", "127.0.0.1");
                vnpay.AddRequestData("vnp_Locale", "vn");
                vnpay.AddRequestData("vnp_OrderInfo", $"Thanh toan don hang {order.M_DonHang}");
                vnpay.AddRequestData("vnp_OrderType", "other");
                vnpay.AddRequestData("vnp_ReturnUrl", vnp_Returnurl);
                vnpay.AddRequestData("vnp_TxnRef", order.M_DonHang);

                string vnpayUrl = vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);
                return Redirect(vnpayUrl);
            }

            // Nếu COD
            return View("OrderCompleted", order.M_DonHang);
        }

        public async Task<IActionResult> VnpayReturn()
        {
            string hashSecret = "GEHJ4HI0BPW6D8C6DZG42TF9DZ2L5ZUI";
            VnPayLibrary vnpay = new VnPayLibrary();

            var query = Request.Query;
            foreach (var key in query.Keys)
            {
                if (key.StartsWith("vnp_"))
                {
                    vnpay.AddResponseData(key, query[key]);
                }
            }

            string vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
            string vnp_TxnRef = vnpay.GetResponseData("vnp_TxnRef");
            string vnp_TransactionNo = vnpay.GetResponseData("vnp_TransactionNo");
            string vnp_SecureHash = vnpay.GetResponseData("vnp_SecureHash");

            bool isValidSignature = vnpay.ValidateSignature(vnp_SecureHash, hashSecret);

            var order = await _context.DonHangs.FirstOrDefaultAsync(x => x.M_DonHang == vnp_TxnRef);

            if (order == null)
            {
                return Content("Không tìm thấy đơn hàng!");
            }

            // Mã 00 = Thanh toán thành công
            if (vnp_ResponseCode == "00")
            {
                order.TrangThaiThanhToan = "Thanh toán thành công";

                // <<< GHI BLOCKCHAIN NẾU KHÁCH THANH TOÁN ONLINE THÀNH CÔNG >>>
                try
                {
                    await _traceabilityService.GhiNhatKyAsync(
                        order.M_DonHang,
                        "Thanh toán Online",
                        "VNPay Gateway",
                        "Hệ thống Ngân hàng",
                        $"Giao dịch VNPay thành công. Mã GD: {vnp_TransactionNo}"
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi ghi Blockchain VNPay");
                }
            }
            else
            {
                order.TrangThaiThanhToan = "Thanh toán thất bại";
            }

            await _context.SaveChangesAsync();
            return View("VnpayRedirect", order);
        }

        [HttpPost]
        public async Task<IActionResult> CheckoutFromAI(DonHang order, string productId, float khoiluong)
        {
            var user = await _userManager.GetUserAsync(User);
            var nguoiMuaProfile = await _context.KhachHangs.FirstOrDefaultAsync(kh => kh.UserId == user.Id);
            if (nguoiMuaProfile == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy hồ sơ khách hàng của bạn.";
                return RedirectToAction("Index");
            }

            // 1. Lấy thông tin sản phẩm AI vừa đặt
            var product = await _context.SanPhams.FirstOrDefaultAsync(p => p.M_SanPham == productId);
            if (product == null)
            {
                TempData["ErrorMessage"] = "Sản phẩm không tồn tại.";
                return RedirectToAction("Index");
            }

            // 2. KIỂM TRA LỖI TỒN KHO CHO 1 SẢN PHẨM NÀY
            var tongTonKho = await _context.LoTonKhos
                .Where(t => t.M_SanPham == productId)
                .SumAsync(t => t.KhoiLuongConLai);

            if (khoiluong > (float)tongTonKho)
            {
                TempData["ErrorMessage"] = $"Sản phẩm '{product.TenSanPham}' chỉ còn {tongTonKho:N0}kg, bạn đặt {khoiluong:N0}kg.";
                return RedirectToAction("Index");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // ======= TẠO MÃ VẬN ĐƠN ========
                string vanDonId = "VD" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                var vanChuyenExist = await _context.VanChuyens.FirstOrDefaultAsync(vc => vc.M_VanDon == vanDonId);
                if (vanChuyenExist == null)
                {
                    vanChuyenExist = new VanChuyen
                    {
                        M_VanDon = vanDonId,
                        DonViVanChuyen = "DHL"
                    };
                    _context.VanChuyens.Add(vanChuyenExist);
                    await _context.SaveChangesAsync();
                }

                // ======= TẠO MÃ ĐƠN HÀNG ========
                var lastOrder = await _context.DonHangs.OrderByDescending(o => o.M_DonHang).FirstOrDefaultAsync();
                int nextNumber = 1;
                if (lastOrder != null && lastOrder.M_DonHang.StartsWith("DH"))
                {
                    var numberPart = lastOrder.M_DonHang.Substring(2);
                    if (int.TryParse(numberPart, out int parsedNumber))
                    {
                        nextNumber = parsedNumber + 1;
                    }
                }

                order.M_DonHang = "DH" + nextNumber.ToString("D6");
                order.M_VanDon = vanChuyenExist.M_VanDon;
                order.TrangThai = order.TrangThai ?? "Chờ xác nhận";
                order.M_KhachHang = nguoiMuaProfile.M_KhachHang;
                order.NgayDat = DateTime.UtcNow;
                order.TotalPrice = product.Gia * (float)khoiluong; // Tính tiền 1 món
                order.TrangThaiThanhToan = "Chưa thanh toán";

                _context.DonHangs.Add(order);
                await _context.SaveChangesAsync();

                // ======= CHI TIẾT ĐƠN HÀNG (CHỈ CÓ 1 MÓN CỦA AI) ========
                var chiTiet = new ChiTietDatHang
                {
                    M_KhachHang = nguoiMuaProfile.M_KhachHang,
                    M_DonHang = order.M_DonHang,
                    M_SanPham = productId,
                    ProductId = productId,
                    Khoiluong = khoiluong,
                    GiaDatHang = product.Gia,
                    M_CTDatHang = "CT" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                    TongTien = (long)(product.Gia * (decimal)khoiluong),
                    NgayTao = DateTime.UtcNow,
                    Quantity = 1,
                    TrangThaiDonHang = "Chờ xác nhận"
                };
                _context.ChiTietDatHangs.Add(chiTiet);
                await _context.SaveChangesAsync();

                try
                {
                    // A. Chuẩn bị danh sách Items cho Firebase (Chỉ 1 item)
                    var itemsList = new List<Dictionary<string, object>>();
                    string fullImgUrl = string.IsNullOrEmpty(product.AnhSanPham) ? "" : (product.AnhSanPham.StartsWith("http") ? product.AnhSanPham : $"{Request.Scheme}://{Request.Host}{product.AnhSanPham}");

                    itemsList.Add(new Dictionary<string, object>
            {
                { "productId", productId },
                { "productName", product.TenSanPham },
                { "quantity", khoiluong },
                { "price", product.Gia },
                { "imageUrl", fullImgUrl }
            });

                    // B. Đóng gói dữ liệu Đơn hàng (Firebase)
                    var firestoreData = new Dictionary<string, object>
            {
                { "uid", nguoiMuaProfile.FirebaseID ?? "" },
                { "maDonHang", order.M_DonHang },
                { "ngayDat", Timestamp.GetCurrentTimestamp() },
                { "trangThai", "Chờ xác nhận" },
                { "trangThaiThanhToan", "Chưa thanh toán" },
                { "tenNguoiNhan", order.Tendathang },
                { "sdtNguoiNhan", order.SoDienThoaidathang },
                { "diaChiGiaoHang", order.ShippingAddress },
                { "ghiChu", order.Notes ?? "" },
                { "phuongThucTT", order.M_PhuongThuc },
                { "tongTien", order.TotalPrice },
                { "items", itemsList }
            };

                    if (!string.IsNullOrEmpty(nguoiMuaProfile.FirebaseID))
                    {
                        await _firebaseSync.AddDonHangToFirestoreAsync(firestoreData);
                    }
                }
                catch (Exception fireEx)
                {
                    Console.WriteLine($"[Lỗi Đồng bộ Firebase]: {fireEx.Message}");
                }

                await transaction.CommitAsync();

                // <<< GHI BLOCKCHAIN >>>
                try
                {
                    string tenKhachHang = nguoiMuaProfile.Ten_KhachHang ?? user.FullName ?? user.UserName ?? "Khách hàng hệ thống";
                    await _traceabilityService.GhiNhatKyAsync(
                        order.M_DonHang,
                        "Khách hàng đặt đơn qua AI",
                        tenKhachHang,
                        "Website mua sắm",
                        $"Đã đặt mua 1 loại sản phẩm qua trợ lý AI. Tổng tiền: {order.TotalPrice:N0} đ"
                    );
                }
                catch (Exception bcEx)
                {
                    _logger.LogError(bcEx, "Lỗi ghi Blockchain khi đặt đơn hàng {DonHangId}", order.M_DonHang);
                }

                // <<< GỬI EMAIL >>>
                try
                {
                    var subject = $"Xác nhận đơn hàng #{order.M_DonHang}";
                    var body = $@"
                <h1>Cảm ơn bạn đã mua hàng qua Trợ Lý AI!</h1>
                <p>Chào {user.FullName ?? user.UserName},</p>
                <p>Đơn hàng <strong>#{order.M_DonHang}</strong> của bạn đã được tiếp nhận.</p>
                <p><strong>Tổng giá trị đơn hàng:</strong> {order.TotalPrice:N0} VNĐ</p>
                <p><strong>Địa chỉ giao hàng:</strong> {order.ShippingAddress},{order.SoDienThoaidathang}</p>
                <p>Đội ngũ Nông Sản Sạch</p>";

                    await _emailService.SendEmailAsync(user.Email, subject, body);
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Lỗi khi gửi email xác nhận đơn hàng {DonHangId}", order.M_DonHang);
                }

                // <<< GỬI SMS >>>
                try
                {
                    if (!string.IsNullOrEmpty(order.SoDienThoaidathang))
                    {
                        var smsMessage = $"Cam on ban da mua hang qua AI. Don hang #{order.M_DonHang} da duoc tiep nhan.";
                        await _smsService.SendSmsAsync(order.SoDienThoaidathang, smsMessage);
                    }
                }
                catch (Exception smsEx)
                {
                    _logger.LogError(smsEx, "Lỗi khi gửi SMS cho đơn hàng {DonHangId}", order.M_DonHang);
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi nghiêm trọng khi Checkout AI ĐH {DonHangId}", order.M_DonHang);
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tạo đơn hàng. Vui lòng thử lại.";
                return RedirectToAction("Index");
            }

            if (order.M_PhuongThuc == "PT005") // THANH TOÁN VNPAY
            {
                string vnp_Returnurl = "https://localhost:7240/ShoppingCart/VnpayReturn";
                string vnp_Url = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
                string vnp_TmnCode = "ODJ7F1TO";
                string vnp_HashSecret = "GEHJ4HI0BPW6D8C6DZG42TF9DZ2L5ZUI";

                VnPayLibrary vnpay = new VnPayLibrary();
                vnpay.AddRequestData("vnp_Version", "2.1.0");
                vnpay.AddRequestData("vnp_Command", "pay");
                vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);
                vnpay.AddRequestData("vnp_Amount", ((long)(order.TotalPrice * 100)).ToString());
                vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
                vnpay.AddRequestData("vnp_CurrCode", "VND");
                vnpay.AddRequestData("vnp_IpAddr", "127.0.0.1");
                vnpay.AddRequestData("vnp_Locale", "vn");
                vnpay.AddRequestData("vnp_OrderInfo", $"Thanh toan don hang AI {order.M_DonHang}");
                vnpay.AddRequestData("vnp_OrderType", "other");
                vnpay.AddRequestData("vnp_ReturnUrl", vnp_Returnurl);
                vnpay.AddRequestData("vnp_TxnRef", order.M_DonHang);

                string vnpayUrl = vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);
                return Redirect(vnpayUrl);
            }

            return View("OrderCompleted", order.M_DonHang);
        }
        public IActionResult OrderCompleted(string id)
        {
            return View(id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateCartItem([FromBody] CartUpdateModel model)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            if (cart == null) return BadRequest();

            var item = cart.Items.FirstOrDefault(i => i.ProductId == model.ProductId);
            if (item != null)
            {
                item.Khoiluong = model.Khoiluong;
            }

            HttpContext.Session.SetObjectAsJson("Cart", cart);
            return Ok();
        }

        public class CartUpdateModel
        {
            public string ProductId { get; set; }
            public float Khoiluong { get; set; }
        }

        public async Task<IActionResult> AddToCart(string productId, float khoiluong)
        {
            var product = await GetProductFromDatabase(productId);
            if (product == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy sản phẩm.";
                return RedirectToAction("Index", "Home");
            }

            AddProductToSessionCart(product, khoiluong);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> BuyNow(string productId, float khoiluong)
        {
            var product = await GetProductFromDatabase(productId);
            if (product == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy sản phẩm.";
                return RedirectToAction("Index", "Home");
            }

            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            cart.RemoveItem(productId);
            var cartItem = new CartItem
            {
                ProductId = product.M_SanPham,
                Name = product.TenSanPham,
                Price = product.Gia,
                Quantity = 1,
                Khoiluong = khoiluong > 0 ? khoiluong : 1
            };
            cart.AddItem(cartItem);
            HttpContext.Session.SetObjectAsJson("Cart", cart);
            return RedirectToAction(nameof(Checkout));
        }

        public IActionResult Index()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            return View(cart);
        }

        private async Task<SanPham> GetProductFromDatabase(string productId)
        {
            var product = await _productRepository.GetByIdAsync(productId);
            return product;
        }

        private void AddProductToSessionCart(SanPham product, float khoiluong)
        {
            var cartItem = new CartItem
            {
                ProductId = product.M_SanPham,
                Name = product.TenSanPham,
                Price = product.Gia,
                Quantity = 1,
                Khoiluong = khoiluong > 0 ? khoiluong : 1
            };

            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            cart.AddItem(cartItem);
            HttpContext.Session.SetObjectAsJson("Cart", cart);
        }

        public IActionResult RemoveFromCart(string productId)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            if (cart is not null)
            {
                cart.RemoveItem(productId);
                HttpContext.Session.SetObjectAsJson("Cart", cart);
            }
            return RedirectToAction("Index");
        }
    }
}