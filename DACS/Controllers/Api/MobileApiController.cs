using System.Security.Claims;
using DACS.Controllers.Api;
using DACS.Models; // Hãy đảm bảo namespace này đúng với project của bạn
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MobileApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly FirebaseSyncService _firebaseSync;
        public MobileApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, FirebaseSyncService firebaseSync)
        {
            _context = context;
            _userManager = userManager;
            _firebaseSync = firebaseSync;
        }

        // --- 1. DANH SÁCH SẢN PHẨM ---
        [HttpGet("products")]
        public async Task<IActionResult> GetProducts()
        {
            var products = await _context.SanPhams
                .Include(sp => sp.LoaiSanPham)
                .Include(sp => sp.DonViTinh)
                .Select(sp => new
                {
                    m_SanPham = sp.M_SanPham,
                    tenSanPham = sp.TenSanPham,
                    gia = sp.Gia,
                    // Đảm bảo đường dẫn ảnh chuẩn để Flutter không bị lỗi 404
                    anhSanPham = sp.AnhSanPham,
                    tenLoai = sp.LoaiSanPham != null ? sp.LoaiSanPham.TenLoai : "",
                    tenDVT = sp.DonViTinh != null ? sp.DonViTinh.TenLoaiTinh : "kg",
                    moTa = sp.MoTa
                }).ToListAsync();
            return Ok(products);
        }

        // --- 2. CHI TIẾT SẢN PHẨM & TỔNG TỒN KHO ---
        [HttpGet("products/{id}")]
        public async Task<IActionResult> GetProductDetail(string id)
        {
            var sp = await _context.SanPhams
                .Include(s => s.LoaiSanPham)
                .Include(s => s.DonViTinh)
                // --- THÊM MỚI: Kèm theo đánh giá và thông tin khách hàng ---
                .Include(s => s.ChiTietDanhGias)
                    .ThenInclude(d => d.KhachHang)
                .FirstOrDefaultAsync(s => s.M_SanPham == id);

            if (sp == null) return NotFound();

            // Tính tổng tồn kho
            var totalStock = await _context.LoTonKhos
                .Where(l => l.M_SanPham == id)
                .SumAsync(l => l.KhoiLuongConLai);

            // Trả về JSON khớp với Product.fromJson trong Flutter
            return Ok(new
            {
                m_SanPham = sp.M_SanPham,
                tenSanPham = sp.TenSanPham,
                gia = sp.Gia,
                anhSanPham = sp.AnhSanPham,
                tenLoai = sp.LoaiSanPham?.TenLoai,
                tenDVT = sp.DonViTinh?.TenLoaiTinh,
                moTa = sp.MoTa,
                totalStock = totalStock,

                // --- THÊM MỚI: Mapping danh sách đánh giá ---
                chiTietDanhGias = sp.ChiTietDanhGias.Select(d => new
                {
                    // Lấy tên khách hàng, nếu null thì để Ẩn danh
                    tenKhachHang = d.KhachHang != null ? d.KhachHang.Ten_KhachHang : "Khách hàng",
                    mucDoHaiLong = d.MucDoHaiLong,
                    moTa_DanhGia = d.MoTa_DanhGia,
                    ngayDanhGia = d.NgayDanhGia
                }).ToList()
            });
        }
        [HttpPost("sync-products")]
        public async Task<IActionResult> SyncAllProductsToFirebase()
        {
            try
            {
                // 1. Lấy dữ liệu từ SQL (Logic giống hệt hàm GetProducts của bạn)
                var products = await _context.SanPhams
                    .Include(sp => sp.LoaiSanPham)
                    .Include(sp => sp.DonViTinh)
                    // Nếu bạn có bảng ảnh riêng (AnhSanPhams) thì Include thêm
                    // .Include(sp => sp.AnhSanPhams) 
                    .ToListAsync();

                // 2. Chuẩn bị dữ liệu gửi lên Firebase
                var listToSync = new List<Dictionary<string, object>>();

                foreach (var sp in products)
                {
                    // Xử lý ảnh: Nếu link ảnh là tương đối (/images/...), thêm domain vào
                    // Giả sử sp.AnhSanPham là chuỗi link ảnh chính
                    string imgUrl = sp.AnhSanPham;
                    if (!string.IsNullOrEmpty(imgUrl) && !imgUrl.StartsWith("http"))
                    {
                        imgUrl = $"{Request.Scheme}://{Request.Host}{imgUrl}";
                    }

                    // Map đúng tên trường mà Flutter App đang dùng (m_SanPham, tenSanPham...)
                    var productData = new Dictionary<string, object>
            {
                { "m_SanPham", sp.M_SanPham },
                { "tenSanPham", sp.TenSanPham },
                { "gia", sp.Gia },
                { "anhSanPham", imgUrl ?? "" }, // Link ảnh đầy đủ
                { "tenLoai", sp.LoaiSanPham != null ? sp.LoaiSanPham.TenLoai : "" },
                { "tenDVT", sp.DonViTinh != null ? sp.DonViTinh.TenLoaiTinh : "kg" },
                { "moTa", sp.MoTa ?? "" },
                
                // Thêm trường hỗ trợ tìm kiếm/lọc trên Firebase nếu cần
                { "searchName", sp.TenSanPham.ToLower() }, // Để search không phân biệt hoa thường
                { "lastUpdated", Timestamp.GetCurrentTimestamp() }
            };

                    listToSync.Add(productData);
                }

                // 3. Gọi Service đẩy lên Firebase
                await _firebaseSync.SyncProductsToFirestoreAsync(listToSync);

                return Ok(new { message = $"Đã đồng bộ thành công {listToSync.Count} sản phẩm lên Firebase." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi đồng bộ: " + ex.Message });
            }
        }
        //mobi đang ký
        [HttpPost("sync-user")]
        public async Task<IActionResult> SyncUserFromMobile([FromBody] RegisterRequestDto req)
        {
            // 1. Kiểm tra xem KhachHang đã có chưa (theo FirebaseID)
            var existingKhach = await _context.KhachHangs
                .FirstOrDefaultAsync(k => k.FirebaseID == req.FirebaseUid);

            if (existingKhach != null)
            {
                return Ok(new { message = "User đã tồn tại, không cần tạo mới." });
            }

            // ==================================================================
            // BƯỚC QUAN TRỌNG: TẠO IDENTITY USER ĐỂ LẤY UserId
            // ==================================================================

            string userIdForNewCustomer = "";

            // Kiểm tra xem Email này đã có trong bảng Identity (AspNetUsers) chưa
            var appUser = await _userManager.FindByEmailAsync(req.Email);

            if (appUser == null)
            {
                // A. Chưa có -> Tạo mới tài khoản Identity (Ngầm định)
                appUser = new ApplicationUser
                {
                    UserName = req.Email, // Lấy email làm username
                    Email = req.Email,
                    FullName = req.FullName,
                    EmailConfirmed = true // Set luôn là đã xác thực vì Firebase đã lo rồi
                };

                // Tạo user với một mật khẩu ngẫu nhiên (Vì user này dùng Firebase login, không dùng pass này)
                // Mật khẩu phải đủ mạnh: Có Hoa, thường, số, ký tự đặc biệt
                var result = await _userManager.CreateAsync(appUser, req.Password);

                if (!result.Succeeded)
                {
                    // Trả về lỗi chi tiết để Mobile biết (Ví dụ: Mật khẩu thiếu ký tự đặc biệt)
                    return BadRequest(new { message = "Lỗi tạo tài khoản Web: " + string.Join(", ", result.Errors.Select(e => e.Description)) });
                }
            }

            // Lấy ID của tài khoản vừa tìm thấy hoặc vừa tạo
            userIdForNewCustomer = appUser.Id;


            // ==================================================================
            // BƯỚC 3: TẠO KHÁCH HÀNG (Bây giờ đã có UserId xịn)
            // ==================================================================
            var newKhach = new KhachHang
            {
                M_KhachHang = Guid.NewGuid().ToString("N").Substring(0, 10),
                FirebaseID = req.FirebaseUid, // ID từ Mobile

                // Link với tài khoản Identity vừa tạo ở trên
                UserId = userIdForNewCustomer, // <--- KHÔNG CÒN NULL NỮA!

                Ten_KhachHang = req.FullName ?? "Mobile User",
                Email_KhachHang = req.Email,
                SDT_KhachHang = req.Phone ?? "None",

                // Các giá trị mặc định để tránh lỗi Foreign Key địa chỉ
                // Đảm bảo trong DB bạn đã có các mã này, hoặc set null nếu DB cho phép null
                MaTinh = "T00",
                MaQuan = "Q0100",
                MaXa = "X010100",
                DiaChi_DuongApThon = "Chưa cập nhật",

            };
            if (!string.IsNullOrEmpty(req.Role))
            {
                // Kiểm tra xem Role có tồn tại chưa để tránh lỗi
                // (Đảm bảo bạn đã chạy Seed Data tạo role "KhachHang" rồi)
                await _userManager.AddToRoleAsync(appUser, req.Role);
            }
            else
            {
                // Nếu Mobile quên gửi, mặc định gán KhachHang
                await _userManager.AddToRoleAsync(appUser, "KhachHang");
            }
            try
            {
                _context.KhachHangs.Add(newKhach);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Đồng bộ thành công! Đã tạo UserId tương ứng." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Lỗi lưu Database: " + ex.Message });
            }
        }

        // ==========================================
        // 2. API GỬI DỮ LIỆU CHO MOBILE (PULL)
        // Mobile gọi cái này để lấy thông tin từ SQL về
        // ==========================================
        [HttpGet("get-profile/{firebaseUid}")]
        public async Task<IActionResult> GetProfile(string firebaseUid)
        {
            var user = await _context.KhachHangs
                .FirstOrDefaultAsync(k => k.FirebaseID == firebaseUid);

            if (user == null) return NotFound(new { message = "Không tìm thấy user này trong hệ thống." });

            return Ok(new
            {
                mKhachHang = user.M_KhachHang,
                fullName = user.Ten_KhachHang,
                email = user.Email_KhachHang,
                phone = user.SDT_KhachHang,
                address = user.DiaChi_DuongApThon,
                // Trả về thêm các mã Tỉnh/Huyện để App hiển thị
                maTinh = user.MaTinh,
                maQuan = user.MaQuan,
                maXa = user.MaXa
            });
        }
        [HttpPost("tao-yeu-cau-thu-gom")]
        public async Task<IActionResult> CreateThuGom([FromBody] ThuGomRequestDto req)
        {
            if (req == null) return BadRequest("Dữ liệu gửi lên bị rỗng");
            var khachHang = await _context.KhachHangs
        .FirstOrDefaultAsync(k => k.FirebaseID == req.UserId); // Tìm theo FirebaseID

            if (khachHang == null)
            {
                return BadRequest("Không tìm thấy thông tin khách hàng trong hệ thống SQL.");
            }

            string mKhachHangCanDung = khachHang.M_KhachHang;
            // Bắt đầu Transaction để đảm bảo lưu cả Yêu Cầu và Chi Tiết cùng lúc
            using var transaction = _context.Database.BeginTransaction();
            try
            {
                // Bước 1: Tìm User trong DB để lấy ID nội bộ (Nếu cần)
                // Giả sử req.UserId là ID lấy từ bảng AspNetUsers hoặc KhachHang

                // Bước 2: Tạo Yêu Cầu Thu Gom (Master)
                var yeuCau = new YeuCauThuGom
                {
                    // 1. M_YeuCau: Tự sinh (GenerateRequestCode)
                    M_YeuCau = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper(),

                    // 2. M_KhachHang: Lấy từ kết quả tìm kiếm ở trên
                    M_KhachHang = khachHang.M_KhachHang,

                    // 3. NgayYeuCau: Lấy giờ hiện tại server
                    NgayYeuCau = DateTime.Now,

                    // 4. ThoiGianSanSang: Lấy từ Mobile gửi lên (Nếu null thì mặc định là Now)
                    ThoiGianSanSang = req.ThoiGianSanSang ?? DateTime.Now,

                    // 5. GhiChu: Map vào bảng Master này (Thay vì bảng ChiTiet)
                    GhiChu = req.GhiChu,

                    // 6. TrangThai: Set cứng ban đầu
                    TrangThai = "Chờ xử lý",

                    // 7. Địa chỉ: Map từ DTO
                    MaTinh = req.MaTinh,
                    MaQuan = req.MaQuan,
                    MaXa = req.MaXa,
                    DiaChi_DuongApThon = req.DiaChiCuThe // Map vào trường này như bạn yêu cầu
                };
                _context.YeuCauThuGoms.Add(yeuCau);
                await _context.SaveChangesAsync();

                // Bước 3: Tạo Chi Tiết Thu Gom (Detail)
                // Lưu ý: Bạn cần có logic tìm M_SanPham và M_LoaiSP dựa trên tên gửi lên
                // Ở đây mình ví dụ gán cứng hoặc tìm đơn giản

                var spDb = await _context.SanPhams.FirstOrDefaultAsync(p => p.TenSanPham == req.TenSanPham);
                var lspDb = await _context.LoaiSanPhams.FirstOrDefaultAsync(l => l.TenLoai == req.LoaiSanPham);

                // Nếu không tìm thấy, lấy sản phẩm mặc định (Bạn phải chắc chắn DB có mã này, ví dụ 'SP000')
                // Hoặc trả về lỗi nếu bắt buộc phải có
                string maSanPhamChuan = spDb != null ? spDb.M_SanPham : "SP001"; // <--- SỬA LẠI MÃ MẶC ĐỊNH CHO ĐÚNG DB CỦA BẠN
                string maLoaiChuan = lspDb != null ? lspDb.M_LoaiSP : "LSP001"; // <--- SỬA LẠI MÃ MẶC ĐỊNH CHO ĐÚNG DB CỦA BẠN

                var chiTiet = new ChiTietThuGom
                {
                    M_ChiTiet = Guid.NewGuid().ToString("N").Substring(0, 10),
                    M_YeuCau = yeuCau.M_YeuCau,

                    // --- 1. SỬA LẠI ID SẢN PHẨM/LOẠI (Quan trọng nhất) ---
                    M_SanPham = maSanPhamChuan,
                    M_LoaiSP = maLoaiChuan,
                    M_DonViTinh = req.DonViTinh, // Mặc định là KG từ mobile gửi lên

                    // --- 2. SỐ LƯỢNG & GIÁ ---
                    SoLuong = (int)req.KhoiLuong,
                    GiaTriMongMuon = req.GiaMongMuon,
                    MoTa = req.GhiChu,

                    // --- 3. CÁC HỆ SỐ & GIÁ (Bổ sung cho đủ giống hàm dưới) ---
                    DoAmThucTe = req.DoAm,
                    HeSoMuaVu = 1.0,      // Mặc định 1.0
                    HeSoDoAm = 1.0,       // Mặc định 1.0
                    PhiVanChuyen = 0,     // Mặc định 0
                    DonGiaThuMua = 0,     // Chưa chốt giá nên để 0

                    // --- 4. TRẠNG THÁI & HÌNH ẢNH ---
                    TrangThaiXuLy = "MoiYeuCau",
                    MaLoTonKho = null, // Chưa nhập kho
                    DanhSachHinhAnh = req.HinhAnh != null && req.HinhAnh.Any() ? string.Join(";", req.HinhAnh) : null,

                    // --- 5. ĐẶC TÍNH (Logic tự động suy luận từ Độ ẩm Mobile gửi lên) ---
                    DacTinh_CongKenh = req.IsCongKenh, // Mobile mặc định gửi false
                    DacTinh_TapChat = req.IsTapChat,   // Mobile mặc định gửi false

                    // Logic tự tính toán giống hàm dưới nhưng dựa trên số liệu Mobile
                    DacTinh_AmUot = (req.DoAm > 20),        // Ví dụ: Ẩm > 20% là ướt
                    DacTinh_Kho = (req.DoAm <= 15),         // Ẩm <= 15% là khô
                    DacTinh_DoAmCao = (req.DoAm > 15 && req.DoAm <= 20), // Hơi ẩm
                    DacTinh_DaXuLy = false                  // Mặc định
                };

                _context.ChiTietThuGoms.Add(chiTiet);
                await _context.SaveChangesAsync();

                // Commit transaction
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "Tạo yêu cầu thành công!",
                    maYeuCau = yeuCau.M_YeuCau
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi Server: " + ex.Message });
            }
        }
        [HttpPost("tao-don-hang")]
        public async Task<IActionResult> CreateOrder([FromBody] DonHangRequestDto req)
        {
            // 1. KIỂM TRA DỮ LIỆU CƠ BẢN
            if (req == null || req.ChiTietDonHangs == null || !req.ChiTietDonHangs.Any())
            {
                return BadRequest("Dữ liệu đơn hàng rỗng hoặc không hợp lệ.");
            }

            // 2. TÌM KHÁCH HÀNG (Qua FirebaseID)
            var khachHang = await _context.KhachHangs
                .FirstOrDefaultAsync(k => k.FirebaseID == req.UserId);

            if (khachHang == null)
            {
                return BadRequest($"Không tìm thấy khách hàng. Vui lòng đăng xuất và đăng nhập lại.");
            }

            // 3. KIỂM TRA TỒN KHO (Giống Web)
            var errorMessages = new List<string>();
            foreach (var item in req.ChiTietDonHangs)
            {
                var tongTonKho = await _context.LoTonKhos
                    .Where(t => t.M_SanPham == item.M_SanPham)
                    .SumAsync(t => t.KhoiLuongConLai);

                if ((float)item.SoLuong > (float)tongTonKho)
                {
                    errorMessages.Add($"Sản phẩm '{item.TenSanPham}' chỉ còn {tongTonKho:N0}kg, khách đặt {item.SoLuong:N0}kg.");
                }
            }

            if (errorMessages.Any())
            {
                return BadRequest(new { message = "Hết hàng", errors = errorMessages });
            }

            // 4. BẮT ĐẦU TRANSACTION (An toàn dữ liệu)
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // ======= A. XỬ LÝ PHƯƠNG THỨC THANH TOÁN (FIX LỖI KHÓA NGOẠI) ========
                string maPhuongThuc = string.IsNullOrEmpty(req.M_PhuongThuc) ? "PT001" : req.M_PhuongThuc;

                // Kiểm tra xem mã này có thật trong DB không
                var ptExist = await _context.PhuongThucThanhToans.FindAsync(maPhuongThuc);
                if (ptExist == null)
                {
                    // NẾU CHƯA CÓ -> TẠO MỚI LUÔN ĐỂ KHÔNG BỊ LỖI
                    ptExist = new PhuongThucThanhToan
                    {
                        M_PhuongThuc = "PT001",
                        TenPhuongThuc = "Thanh toán khi nhận hàng (COD)"
                    };
                    _context.PhuongThucThanhToans.Add(ptExist);
                    await _context.SaveChangesAsync(); // Lưu ngay
                    maPhuongThuc = "PT001"; // Gán lại mã chuẩn
                }

                // ======= B. XỬ LÝ VẬN ĐƠN (Giống Web) ========
                string vanDonId = "VD" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                var vanChuyenExist = await _context.VanChuyens.FirstOrDefaultAsync(vc => vc.M_VanDon == vanDonId);

                if (vanChuyenExist == null)
                {
                    vanChuyenExist = new VanChuyen
                    {
                        M_VanDon = vanDonId,
                        DonViVanChuyen = "Giao hàng tiêu chuẩn"
                    };
                    _context.VanChuyens.Add(vanChuyenExist);
                    await _context.SaveChangesAsync(); // Lưu ngay để có mã dùng cho Đơn Hàng
                }

                // ======= C. TẠO MÃ ĐƠN HÀNG (Logic tăng dần giống Web) ========
                var lastOrder = await _context.DonHangs.OrderByDescending(o => o.M_DonHang).FirstOrDefaultAsync();
                int nextNumber = 1;
                if (lastOrder != null && !string.IsNullOrEmpty(lastOrder.M_DonHang) && lastOrder.M_DonHang.StartsWith("DH"))
                {
                    var numberPart = lastOrder.M_DonHang.Substring(2);
                    if (int.TryParse(numberPart, out int parsedNumber))
                    {
                        nextNumber = parsedNumber + 1;
                    }
                }
                string maDonHangMoi = "DH" + nextNumber.ToString("D6");

                // ======= D. TẠO ĐƠN HÀNG MASTER ========
                // Validate dữ liệu bắt buộc (Tránh lỗi Required)
                string tenNguoiNhan = string.IsNullOrEmpty(req.Tendathang) ? khachHang.Ten_KhachHang : req.Tendathang;
                string sdtNguoiNhan = string.IsNullOrEmpty(req.SoDienThoaidathang) ? khachHang.SDT_KhachHang : req.SoDienThoaidathang;

                // Tính tổng tiền server-side
                decimal tongTienServer = req.ChiTietDonHangs.Sum(x => (decimal)x.SoLuong * (decimal)x.DonGia);

                var donHang = new DonHang
                {
                    M_DonHang = maDonHangMoi,

                    // Khóa ngoại
                    M_VanDon = vanChuyenExist.M_VanDon,
                    M_KhachHang = khachHang.M_KhachHang,
                    M_PhuongThuc = maPhuongThuc,

                    // Thông tin bắt buộc [Required]
                    Tendathang = tenNguoiNhan,
                    SoDienThoaidathang = sdtNguoiNhan,
                    ShippingAddress = req.ShippingAddress ?? "Tại cửa hàng",
                    Notes = req.Notes,

                    NgayDat = DateTime.Now,
                    TotalPrice = (float)tongTienServer,
                    TrangThai = "Chờ xác nhận",
                    TrangThaiThanhToan = "Chưa thanh toán",
                    DaTruTonKho = false
                };

                _context.DonHangs.Add(donHang);
                await _context.SaveChangesAsync();

                // ======= E. TẠO CHI TIẾT ĐƠN HÀNG ========
                foreach (var item in req.ChiTietDonHangs)
                {
                    // 1. Kiểm tra M_SanPham từ App gửi lên
                    string maSanPham = item.M_SanPham;
                    if (string.IsNullOrEmpty(maSanPham))
                    {
                        // Nếu App gửi thiếu, thử lấy từ TenSanPham hoặc bỏ qua (tùy logic)
                        // Ở đây ta báo lỗi hoặc log lại
                        throw new Exception($"Sản phẩm '{item.TenSanPham}' bị thiếu Mã (M_SanPham).");
                    }

                    // 2. Tính thành tiền
                    long thanhTienItem = (long)((decimal)item.SoLuong * item.DonGia);

                    var chiTiet = new ChiTietDatHang
                    {
                        M_CTDatHang = "CT" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),

                        // --- KHÓA NGOẠI ---
                        M_DonHang = donHang.M_DonHang,
                        M_KhachHang = khachHang.M_KhachHang,
                        M_SanPham = maSanPham,

                        // --- SỬA LỖI: GÁN CỘT ProductId BẮT BUỘC ---
                        ProductId = maSanPham, // <--- DÒNG QUAN TRỌNG NÀY PHẢI CÓ

                        // --- DỮ LIỆU KHÁC ---
                        Khoiluong = (float)item.SoLuong, // Ép kiểu double
                        Quantity = (int)item.SoLuong,     // Ép kiểu int
                        GiaDatHang = item.DonGia,         // decimal
                        TongTien = thanhTienItem,         // long

                        NgayTao = DateTime.Now,
                        TrangThaiDonHang = "Chờ xác nhận"
                    };

                    _context.ChiTietDatHangs.Add(chiTiet);
                }
                await _context.SaveChangesAsync();

                // ======= F. HOÀN TẤT ========
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "Đặt hàng thành công!",
                    maDonHang = donHang.M_DonHang,
                    tongTien = donHang.TotalPrice
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                // TRẢ VỀ LỖI CHI TIẾT (QUAN TRỌNG ĐỂ DEBUG)
                // Nếu lỗi do Khóa ngoại, InnerException sẽ nói rõ là bảng nào
                var errorMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { message = "Lỗi Server: " + errorMsg });
            }
        }
    }
}
    
