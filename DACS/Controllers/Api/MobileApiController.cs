using System.Security.Claims;
using DACS.Controllers.Api;
using DACS.Models; // Hãy đảm bảo namespace này đúng với project của bạn
using DACS.Services;
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
        private readonly BlockchainService _blockchainService;
        public MobileApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, FirebaseSyncService firebaseSync,
            BlockchainService blockchainService)
        {
            _context = context;
            _userManager = userManager;
            _firebaseSync = firebaseSync;
            _blockchainService = blockchainService;
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
                    moTa = sp.MoTa,
                    totalStock = _context.LoTonKhos
                                .Where(lo => lo.M_SanPham == sp.M_SanPham) // (Tùy chọn) Chỉ lấy lô còn hạn
                                .Sum(lo => lo.KhoiLuongConLai)
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
        [HttpPut("Update/{id}")]
        public async Task<IActionResult> UpdateProduct(string id, [FromBody] ProductUpdateDto model)
        {
            if (id != model.Id) return BadRequest("ID không khớp");

            // 1. TÌM VÀ CẬP NHẬT VÀO SQL SERVER
            var productInSql = await _context.SanPhams.FindAsync(id);
            if (productInSql == null) return NotFound("Không tìm thấy sản phẩm trong SQL");

            // Chỉ cập nhật 3 trường như bạn yêu cầu
            productInSql.TenSanPham = model.Title;
            productInSql.Gia = model.Price;
            productInSql.MoTa = model.Description;

            try
            {
                // Lưu SQL trước
                await _context.SaveChangesAsync();

                // 2. CẬP NHẬT SANG FIRESTORE
                // Gọi service đã tạo ở bước 1
                await _firebaseSync.UpdateProductInFirestore(
                    id,
                    model.Title,
                    model.Price,
                    model.Description
                );

                return Ok(new { message = "Cập nhật thành công cả SQL và Firestore!" });
            }
            catch (Exception ex)
            {
                // Nếu lỗi xảy ra (ví dụ mất mạng không up được Firebase)
                return StatusCode(500, new { message = "Lỗi cập nhật: " + ex.Message });
            }
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
                    

                    // Map đúng tên trường mà Flutter App đang dùng (m_SanPham, tenSanPham...)
                    var productData = new Dictionary<string, object>
            {
                { "id", sp.M_SanPham },
                { "title", sp.TenSanPham },
                { "price", sp.Gia },
                { "imageUrls", imgUrl ?? "" }, // Link ảnh đầy đủ
                { "category", sp.LoaiSanPham != null ? sp.LoaiSanPham.TenLoai : "" },
                { "unit", sp.DonViTinh != null ? sp.DonViTinh.TenLoaiTinh : "kg" },
                { "description", sp.MoTa ?? "" },
                
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

            // 1. Tìm Khách Hàng theo FirebaseID
            var khachHang = await _context.KhachHangs
                .FirstOrDefaultAsync(k => k.FirebaseID == req.UserId);

            if (khachHang == null)
            {
                return BadRequest("Không tìm thấy thông tin khách hàng trong hệ thống SQL.");
            }

            // Bắt đầu Transaction SQL
            using var transaction = _context.Database.BeginTransaction();
            try
            {
                // 2. Tạo Yêu Cầu Thu Gom (Master)
                var yeuCau = new YeuCauThuGom
                {
                    M_YeuCau = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper(), // Mã tự sinh
                    M_KhachHang = khachHang.M_KhachHang,
                    NgayYeuCau = DateTime.Now,
                    ThoiGianSanSang = req.ThoiGianSanSang ?? DateTime.Now,
                    GhiChu = req.GhiChu,
                    TrangThai = "Chờ xử lý",
                    MaTinh = req.MaTinh,
                    MaQuan = req.MaQuan,
                    MaXa = req.MaXa,
                    DiaChi_DuongApThon = req.DiaChiCuThe
                };
                _context.YeuCauThuGoms.Add(yeuCau);
                await _context.SaveChangesAsync();

                // 3. Tìm sản phẩm / loại sản phẩm (Map dữ liệu)
                var spDb = await _context.SanPhams.FirstOrDefaultAsync(p => p.TenSanPham == req.TenSanPham);
                var lspDb = await _context.LoaiSanPhams.FirstOrDefaultAsync(l => l.TenLoai == req.LoaiSanPham);

                string maSanPhamChuan = spDb != null ? spDb.M_SanPham : "SP001";
                string maLoaiChuan = lspDb != null ? lspDb.M_LoaiSP : "LSP001";

                // 4. Tạo Chi Tiết Thu Gom (Detail)
                var chiTiet = new ChiTietThuGom
                {
                    M_ChiTiet = Guid.NewGuid().ToString("N").Substring(0, 10),
                    M_YeuCau = yeuCau.M_YeuCau,
                    M_SanPham = maSanPhamChuan,
                    M_LoaiSP = maLoaiChuan,
                    M_DonViTinh = req.DonViTinh,
                    SoLuong = (int)req.KhoiLuong,
                    GiaTriMongMuon = req.GiaMongMuon,
                    MoTa = req.GhiChu,
                    DoAmThucTe = req.DoAm,
                    HeSoMuaVu = 1.0,
                    HeSoDoAm = 1.0,
                    PhiVanChuyen = 0,
                    DonGiaThuMua = 0,
                    TrangThaiXuLy = "MoiYeuCau",
                    DanhSachHinhAnh = req.HinhAnh != null && req.HinhAnh.Any() ? string.Join(";", req.HinhAnh) : null,
                    DacTinh_CongKenh = req.IsCongKenh,
                    DacTinh_TapChat = req.IsTapChat,
                    DacTinh_AmUot = (req.DoAm > 20),
                    DacTinh_Kho = (req.DoAm <= 15),
                    DacTinh_DoAmCao = (req.DoAm > 15 && req.DoAm <= 20),
                    DacTinh_DaXuLy = false
                };

                _context.ChiTietThuGoms.Add(chiTiet);
                await _context.SaveChangesAsync();

                // 5. Commit SQL thành công
                await transaction.CommitAsync();

                // ---------------------------------------------------------
                // 6. ĐỒNG BỘ SANG FIREBASE (ĐỂ ADMIN APP THẤY NGAY)
                // ---------------------------------------------------------
                try
                {
                    // Tạo Dictionary chứa dữ liệu cần hiển thị bên App Admin
                    var firestoreData = new Dictionary<string, object>
            {
                // Key quan trọng để đồng bộ ngược lại
                { "m_YeuCau", yeuCau.M_YeuCau },
                { "isSync", true }, // Đánh dấu là dữ liệu chuẩn từ SQL

                // Thông tin hiển thị
                { "uid", khachHang.FirebaseID },
                { "contactName", req.HoTen ?? khachHang.Ten_KhachHang },
                { "contactPhone", req.SoDienThoai ?? khachHang.SDT_KhachHang },
                { "fullAddress", $"{req.DiaChiCuThe}, {req.MaXa}, {req.MaQuan}, {req.MaTinh}" }, // Tạm gộp địa chỉ
                
                // Thông tin sản phẩm
                { "productName", req.TenSanPham ?? "Sản phẩm chưa xác định" },
                { "productId", maSanPhamChuan },
                { "category", req.LoaiSanPham ?? "Loại chưa xác định" },
                
                // Số liệu
                { "amount", req.KhoiLuong },
                { "giaTriMongMuon", req.GiaMongMuon },
                { "doAm", req.DoAm },
                { "note", req.GhiChu },
                
                // Trạng thái & Thời gian
                { "trangThaiXuLy", "MoiYeuCau" },
                { "createdAt", Timestamp.GetCurrentTimestamp() }
            };

                    // Gọi Service đẩy lên Firestore
                    // Lưu ý: _firebaseService cần được Inject vào Controller này
                    await _firebaseSync.AddThuGomToFirestoreAsync(firestoreData, yeuCau.M_YeuCau);
                }
                catch (Exception firestoreEx)
                {
                    // Chỉ log lỗi, không làm hỏng flow chính vì đơn hàng đã tạo thành công ở SQL
                    Console.WriteLine($"[Lỗi Firestore]: {firestoreEx.Message}");
                }

                // 7. Trả về kết quả cho Mobile App
                return Ok(new
                {
                    message = "Tạo yêu cầu thành công!",
                    maYeuCau = yeuCau.M_YeuCau // Trả mã này về để Mobile App có thể lưu cục bộ nếu cần
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
        [HttpPost("update-order-status")]
        public async Task<IActionResult> UpdateOrderStatus([FromBody] OrderStatusDto req)
        {
            if (req == null || string.IsNullOrEmpty(req.MaDonHang))
                return BadRequest("Dữ liệu không hợp lệ.");

            // 1. Tìm đơn hàng (Include Chi tiết để trừ kho)
            var orderSql = await _context.DonHangs
                .Include(d => d.ChiTietDatHangs)
                .FirstOrDefaultAsync(o => o.M_DonHang == req.MaDonHang);

            if (orderSql == null)
                return NotFound(new { message = "Không tìm thấy đơn hàng trong SQL." });

            if (orderSql.TrangThai == "Hoàn thành" && req.TrangThai == "Đã hủy")
                return BadRequest(new { message = "Đơn hàng đã hoàn thành, không thể hủy." });

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                // 2. Cập nhật thông tin cơ bản
                orderSql.TrangThai = req.TrangThai;

                if (!string.IsNullOrEmpty(req.DonViVanChuyen) && !string.IsNullOrEmpty(orderSql.M_VanDon))
                {
                    var vanChuyen = await _context.VanChuyens.FirstOrDefaultAsync(vc => vc.M_VanDon == orderSql.M_VanDon);
                    if (vanChuyen != null) vanChuyen.DonViVanChuyen = req.DonViVanChuyen;
                }

                // =========================================================================
                // 3. LOGIC TRỪ TỒN KHO & GHI BLOCKCHAIN (CHỈ CHẠY KHI HOÀN THÀNH)
                // =========================================================================
                if (req.TrangThai == "Hoàn thành")
                {
                    // A. TRỪ TỒN KHO (Chỉ trừ nếu chưa trừ)
                    if (orderSql.DaTruTonKho == false)
                    {
                        foreach (var item in orderSql.ChiTietDatHangs)
                        {
                            decimal soLuongCanTru = (decimal)item.Khoiluong;
                            var cacLoHang = await _context.LoTonKhos
                                .Where(l => l.M_SanPham == item.M_SanPham && l.KhoiLuongConLai > 0)
                                .OrderBy(l => l.HanSuDung)
                                .ToListAsync();

                            if (!cacLoHang.Any()) throw new Exception($"Sản phẩm {item.M_SanPham} hết hàng.");

                            foreach (var lo in cacLoHang)
                            {
                                if (soLuongCanTru <= 0) break;
                                if (lo.KhoiLuongConLai >= soLuongCanTru)
                                {
                                    lo.KhoiLuongConLai -= (int)soLuongCanTru;
                                    soLuongCanTru = 0;
                                }
                                else
                                {
                                    soLuongCanTru -= lo.KhoiLuongConLai;
                                    lo.KhoiLuongConLai = 0;
                                }
                            }
                            if (soLuongCanTru > 0) throw new Exception($"Kho thiếu hàng cho sản phẩm {item.M_SanPham}.");
                        }
                        orderSql.DaTruTonKho = true;
                    }

                    // B. GHI NHẬT KÝ VÀO GANACHE (BLOCKCHAIN)
                    try
                    {
                        // Chuẩn bị dữ liệu Metadata
                        string metadata = $"Đơn hàng {orderSql.M_DonHang} - {orderSql.TrangThai} - " +
                                          $"{orderSql.M_PhuongThuc} - Tổng tiền: {orderSql.TotalPrice:N0}";

                        // Lấy khối lượng thực tế từ App gửi lên, nếu null thì lấy 0
                        string weightData = req.KhoiLuongThucTe.HasValue
                                            ? req.KhoiLuongThucTe.Value.ToString()
                                            : "0";

                        // Gọi Service Blockchain
                        // Tham số 3: Thay ShippingAddress bằng WeightData như bạn yêu cầu
                        await _blockchainService.GhiNhatKyAsync(
                            orderSql.M_DonHang,      // ID
                            orderSql.TrangThai,      // Trạng thái
                            weightData,              // Thay cho Shipping Address
                            metadata                 // Metadata mô tả
                        );
                    }
                    catch (Exception bcEx)
                    {
                        // Chỉ log lỗi blockchain, không rollback giao dịch chính (vì đơn hàng đã xong rồi)
                        // Hoặc nếu bạn muốn Blockchain bắt buộc phải thành công thì bỏ try-catch này đi
                        Console.WriteLine($"[Blockchain Error]: {bcEx.Message}");
                    }
                }

                // 4. Lưu Database
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Cập nhật thành công! (Đã trừ kho & lưu Blockchain)" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi Server: " + ex.Message });
            }
        }
        [HttpGet("inventory")]
        public async Task<IActionResult> GetInventory()
        {
            try
            {
                // Join bảng Lô Tồn Kho với Sản Phẩm và Kho Hàng
                var inventory = await _context.LoTonKhos
                    .Include(lo => lo.SanPham)
                        .ThenInclude(sp => sp.DonViTinh) // Để lấy đơn vị (kg, lít...)
                    .Include(lo => lo.KhoHang)
                    .Where(lo => lo.KhoiLuongConLai > 0) // Chỉ lấy lô còn hàng
                    .Select(lo => new
                    {
                        // Thông tin hiển thị
                        ProductName = lo.SanPham.TenSanPham,
                        ProductImage = lo.SanPham.AnhSanPham,
                        Quantity = lo.KhoiLuongConLai,
                        Unit = lo.SanPham.DonViTinh != null ? lo.SanPham.DonViTinh.TenLoaiTinh : "kg",
                        WarehouseName = lo.KhoHang.TenKho,

                        // Thông tin phụ (nếu cần)
                        ExpiryDate = lo.HanSuDung
                    })
                    .OrderBy(x => x.WarehouseName) // Sắp xếp theo tên kho cho dễ nhìn
                    .ToListAsync();

                return Ok(inventory);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi Server: " + ex.Message });
            }
        }
        [HttpPost("update-scrap-status")]
        public async Task<IActionResult> UpdateScrapStatus([FromBody] ScrapStatusDto req)
        {
            if (req == null || string.IsNullOrEmpty(req.RequestId)) return BadRequest("Dữ liệu không hợp lệ");

            // A. Tìm Yêu Cầu Thu Gom (Kèm theo Chi Tiết để lấy ProductId)
            var request = await _context.YeuCauThuGoms
                .Include(y => y.ChiTietThuGoms) // <--- QUAN TRỌNG: Load chi tiết để lấy M_SanPham
                .FirstOrDefaultAsync(y => y.M_YeuCau == req.RequestId);

            if (request == null) return NotFound(new { message = "Không tìm thấy yêu cầu trong SQL" });

            // Validate: Nếu đã xong thì thôi
            if (request.TrangThai == "HoanThanh" && req.Status != "HoanThanh")
            {
                return BadRequest(new { message = "Đơn này đã hoàn thành, không thể sửa." });
            }

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                // B. Cập nhật trạng thái Master (YeuCauThuGom)
                request.TrangThai = req.Status;

                if (!string.IsNullOrEmpty(req.Date) && DateTime.TryParse(req.Date, out DateTime parsedDate))
                {
                     request.ThoiGianHoanThanh = parsedDate; // Nếu muốn lưu ngày hoàn thành
                }

                // C. LOGIC CỘNG TỒN KHO (Dựa vào Chi Tiết)
                if (req.Status == "HoanThanh")
                {
                    // Duyệt qua danh sách chi tiết (thường chỉ có 1 sản phẩm, nhưng code loop cho chắc)
                    foreach (var detail in request.ChiTietThuGoms)
                    {
                        // Xác định khối lượng cần cộng:
                        // Nếu User nhập khối lượng thực tế -> Dùng nó.
                        // Nếu không -> Dùng khối lượng dự kiến trong chi tiết.
                        int weightToAdd = req.ActualWeight ?? (int)detail.SoLuong;

                        // Cập nhật lại số lượng chốt trong ChiTietThuGom luôn
                        detail.SoLuong = (int)weightToAdd;

                        // 1. Tìm Lô Tồn Kho dựa trên M_SanPham của chi tiết này
                        var existingBatch = await _context.LoTonKhos
                            .FirstOrDefaultAsync(l => l.M_SanPham == detail.M_SanPham);

                        if (existingBatch != null)
                        {
                            // 2. Cộng vào kho
                            existingBatch.KhoiLuongConLai += weightToAdd;
                        }
                        else
                        {
                            // Nếu chưa có lô nào, bắt buộc phải có logic tạo mới hoặc báo lỗi
                            // Ở đây tôi chọn báo lỗi để Admin biết mà tạo kho trước
                            throw new Exception($"Sản phẩm {detail.M_SanPham} chưa có lô hàng nào trong kho.");
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Cập nhật & Nhập kho thành công!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi Server: " + ex.Message });
            }
        }
    }
}
    
