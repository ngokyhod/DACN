using DACS.Models; 
using DACS.Models.ViewModels;
using DACS.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore; 


namespace DACS.Controllers
{
    public class TruyXuatController : Controller
    {
        private readonly BlockchainService _blockchainService;
        private readonly ApplicationDbContext _db; // <<< 1. KHAI BÁO DBCONTEXT

        // <<< 2. TIÊM DBCONTEXT VÀO CONSTRUCTOR >>>
        public TruyXuatController(BlockchainService blockchainService, ApplicationDbContext dbContext)
        {
            _blockchainService = blockchainService;
            _db = dbContext; // Gán DbContext
        }

        // GET: /TruyXuat/NhatKy/L001
        // (Bạn có thể gọi đường dẫn này trực tiếp trên trình duyệt để test)
        [HttpGet("TruyXuat/NhatKy/{maLo}")]
        public async Task<IActionResult> NhatKy(string maLo)
        {
            if (string.IsNullOrEmpty(maLo))
            {
                return NotFound("Vui lòng cung cấp Mã Lô.");
            }

            // === BƯỚC 1: LẤY DỮ LIỆU TỪ SQL ===
            // (Thay 'LoHangs' bằng tên DbSet của bạn)
            // (Bao gồm cả thông tin 'SanPham' nếu bạn muốn)
            var lotInfo = await _db.LoTonKhos
                                    .Include(l => l.SanPham) // <-- Join Bảng Sản Phẩm (tùy chọn)
                                    .FirstOrDefaultAsync(l => l.MaLoTonKho == maLo);

            if (lotInfo == null)
            {
                return NotFound($"Không tìm thấy lô hàng với mã '{maLo}' trong cơ sở dữ liệu.");
            }

            // === BƯỚC 2: LẤY DỮ LIỆU TỪ BLOCKCHAIN ===
            var history = await _blockchainService.GetHistoryAsync(maLo);

            // === BƯỚC 3: TẠO VIEWMODEL ===
            var viewModel = new NhatKyViewModel
            {
                LotInfo = lotInfo,     // Dữ liệu từ SQL
                //History = history      // Dữ liệu từ Blockchain
            };

            // === BƯỚC 4: GỬI VIEWMODEL RA VIEW ===
            return View(viewModel);
        }

        // ========= THÊM HÀM TEST NÀY VÀO =========
        [HttpGet("TruyXuat/Test")]
        public async Task<IActionResult> TestGhiVaDoc()
        {
            string testLotId = "TEST_TU_CSHARP_12345"; // Dùng một mã test hoàn toàn mới
            var resultLog = new List<string>();

            try
            {
                // ---- BƯỚC 1: GHI (WRITE) ----
                resultLog.Add($"Đang GHI vào Lô: {testLotId}...");
                dynamic result2 = await _blockchainService.GhiNhatKyAsync(
     testLotId,
     "TEST_GHI",
     "TEST_LOCATION",
     "Ghi thành công từ C#"
);

                string txHash2 = result2.TxHash;
                resultLog.Add($"GHI THÀNH CÔNG. TxHash: {txHash2}");

                // Chờ 3 giây để Ganache đào (mine) khối
                resultLog.Add("Đang chờ 3 giây cho khối được đào...");
                await Task.Delay(8000);

                // ---- BƯỚC 2: ĐỌC (READ) ----
                resultLog.Add($"Đang ĐỌC từ Lô: {testLotId}...");
                var history = await _blockchainService.GetHistoryAsync(testLotId);

                // ---- BƯỚC 3: TRẢ KẾT QUẢ ----
                resultLog.Add($"ĐỌC THÀNH CÔNG. Số bản ghi tìm thấy: {history.Count}");

                return Json(new
                {
                    TestSuccess = true,
                    Log = resultLog,
                    Data = history
                });
            }
            catch (Exception ex)
            {
                resultLog.Add($"TEST THẤT BẠI: {ex.Message}");
                return Json(new { TestSuccess = false, Log = resultLog });
            }
        }
    }
}