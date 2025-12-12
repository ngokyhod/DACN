using DACS.Areas.QuanLyDH.Controllers;
using DACS.Models;
using DACS.Models.Blockchain;
using DACS.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization; // Cần thêm using này cho List
using DACS.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
public class HistoryViewModel
    {
    public string LotId { get; set; }
    public string Status { get; set; }
    public string Location { get; set; }
    public string Metadata { get; set; }
    public List<TraceEventDTO> Events { get; set; } = new List<TraceEventDTO>();
    public string ErrorMessage { get; set; }
    public string SuccessMessage { get; set; }
}
namespace DACS.Areas.QuanLyDH.Controllers
{
    [Area("QuanLyDH")]
    [Authorize(Roles = SD.Role_Owner + "," + SD.Role_QuanLyDH)]
    // ViewModel cho ViewHistory



    public class QuanLyDataController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<QuanLyDHController> _logger;
        private readonly BlockchainService _blockchainService;

        public QuanLyDataController(ApplicationDbContext context,
                                         ILogger<QuanLyDHController> logger,
                                         BlockchainService blockchainService)
        {
            _context = context;
            _logger = logger;
            _blockchainService = blockchainService;
        }

        public IActionResult Index()
        {
            // Có thể dùng để hiển thị form tra cứu ban đầu
            return View(new HistoryViewModel());
        }
        public IActionResult PublicTrace()
        {
            // Có thể dùng để hiển thị form tra cứu ban đầu
            return View(); 
        }
        [HttpGet("api/blockchain/tx-data-sql")]
        public async Task<IActionResult> GetTxDataFromSql([FromQuery] string txHash)
        {
            if (string.IsNullOrEmpty(txHash))
            {
                return BadRequest(new { success = false, message = "TxHash không được để trống." });
            }

            // Giả định _context là DbContext của bạn chứa DbSet<BlockchainTransaction>
            var data = await _context.BlockchainTransaction
                .FirstOrDefaultAsync(t => t.TxHash == txHash);

            if (data == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy bản ghi nào trong CSDL với TxHash này." });
            }

            // Trả về dữ liệu SQL
            return Ok(new
            {
                success = true,
                data = new
                {
                    data.MaDonHang,
                    data.TrangThai,
                    data.DiaChiGiaoHang,
                    data.Metadata,
                    data.CreatedAt
                }
            });
        }



    }
}