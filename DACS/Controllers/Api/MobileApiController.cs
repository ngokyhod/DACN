using System.Security.Claims;
using DACS.Controllers.Api;
using DACS.Models; // Hãy đảm bảo namespace này đúng với project của bạn
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
        public MobileApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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
            // 1. KIỂM TRA DỮ LIỆU ĐẦU VÀO
            if (req == null || req.ChiTietDonHangs == null || !req.ChiTietDonHangs.Any())
            {
                return BadRequest("Dữ liệu đơn hàng rỗng.");
            }

            // 2. TÌM KHÁCH HÀNG (Thay vì User.Id của Identity thì dùng FirebaseID)
            // Code MVC: var nguoiMuaProfile = ... Where(UserId == user.Id)
            var khachHang = await _context.KhachHangs
                .FirstOrDefaultAsync(k => k.FirebaseID == req.UserId);

            if (khachHang == null)
            {
                return BadRequest($"Không tìm thấy khách hàng có FirebaseID: {req.UserId}");
            }

            // 3. KIỂM TRA TỒN KHO (Giống hệt MVC)
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

            // 4. BẮT ĐẦU TRANSACTION
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // ======= A. XỬ LÝ VẬN ĐƠN (Giống MVC) ========
                string vanDonId = "VD" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

                // Kiểm tra trùng (Logic MVC)
                var vanChuyenExist = await _context.VanChuyens.FirstOrDefaultAsync(vc => vc.M_VanDon == vanDonId);
                if (vanChuyenExist == null)
                {
                    vanChuyenExist = new VanChuyen
                    {
                        M_VanDon = vanDonId,
                        DonViVanChuyen = "DHL", 
                    };
                    _context.VanChuyens.Add(vanChuyenExist);
                    await _context.SaveChangesAsync(); // <--- LƯU NGAY: Để mã VD tồn tại trong DB
                }

                // ======= B. TẠO MÃ ĐƠN HÀNG (Giống MVC) ========
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

                // ======= C. XỬ LÝ PHƯƠNG THỨC THANH TOÁN (Bổ sung để tránh lỗi) ========
                // Code MVC lấy từ Dropdown, API lấy từ JSON string. Cần check kỹ.
                string maPhuongThuc = req.M_PhuongThuc;
                if (string.IsNullOrEmpty(maPhuongThuc)) maPhuongThuc = "PT001"; // Mặc định nếu rỗng

                // Kiểm tra tồn tại trong DB chưa
                bool ptExists = await _context.PhuongThucThanhToans.AnyAsync(p => p.M_PhuongThuc == maPhuongThuc);
                if (!ptExists) maPhuongThuc = "PT001"; // Fallback về COD nếu mã gửi lên sai

                // ======= D. TẠO ĐƠN HÀNG MASTER (Map theo MVC) ========
                var donHang = new DonHang
                {
                    M_DonHang = maDonHangMoi,

                    // --- CÁC KHÓA NGOẠI ---
                    M_VanDon = vanChuyenExist.M_VanDon,  // Lấy từ biến vừa tạo ở bước A
                    M_KhachHang = khachHang.M_KhachHang, // Lấy từ biến tìm được ở bước 2
                    M_PhuongThuc = maPhuongThuc,

                    // --- THÔNG TIN NGƯỜI NHẬN ---
                    Tendathang = req.Tendathang,
                    SoDienThoaidathang = req.SoDienThoaidathang,
                    ShippingAddress = req.ShippingAddress,
                    Notes = req.Notes,

                    // --- THÔNG TIN KHÁC ---
                    NgayDat = DateTime.Now,
                    // Tính lại tổng tiền giống MVC logic
                    TotalPrice = (float)req.ChiTietDonHangs.Sum(i => (decimal)i.SoLuong * (decimal)i.DonGia),

                    TrangThai = "Chờ xác nhận", // Giống MVC
                    TrangThaiThanhToan = "Chưa thanh toán",
                    DaTruTonKho = false
                };

                _context.DonHangs.Add(donHang);
                await _context.SaveChangesAsync(); // LƯU ĐƠN HÀNG

                // ======= E. TẠO CHI TIẾT ĐƠN HÀNG (Giống MVC) ========
                foreach (var item in req.ChiTietDonHangs)
                {
                    var chiTiet = new ChiTietDatHang
                    {
                        M_CTDatHang = "CT" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),

                        // Khóa ngoại
                        M_DonHang = donHang.M_DonHang,
                        M_KhachHang = khachHang.M_KhachHang, // <--- QUAN TRỌNG: MVC có dòng này, API cũng phải có
                        M_SanPham = item.M_SanPham,

                        // Dữ liệu
                        ProductId = item.M_SanPham, // MVC của bạn lưu cả 2 cột này
                        Khoiluong = (float)item.SoLuong, // Ép kiểu về double như MVC
                        Quantity = (int)item.SoLuong,     // Lưu thêm cột int nếu cần

                        GiaDatHang = (long)item.DonGia,

                        // Logic tính tiền ép kiểu giống MVC
                        TongTien = (long)((decimal)item.SoLuong * (decimal)item.DonGia),

                        NgayTao = DateTime.Now,
                        TrangThaiDonHang = "Chờ xác nhận"
                    };
                    _context.ChiTietDatHangs.Add(chiTiet);
                }
                await _context.SaveChangesAsync(); // LƯU CHI TIẾT

                // ======= F. COMMIT ========
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

                // --- LOGIC BẮT LỖI SÂU HƠN ---
                // Lấy lỗi gốc (InnerException) để biết cột nào bị NULL/Lỗi
                var innerMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;

                // Trả về lỗi chi tiết cho Android xem
                return StatusCode(500, new
                {
                    message = "Lỗi lưu SQL: " + innerMessage,
                    details = ex.ToString()
                });
            }
        }
    }
}
    
