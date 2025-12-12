using DACS.Models.Blockchain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes; // <<< THÊM USING NÀY
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs; // <<< THÊM USING NÀY
using System.Linq;

namespace DACS.Services
{
    public class BlockchainService
    {
        private readonly Web3 _web3;
        private readonly Contract _contract;
        private readonly Nethereum.Web3.Accounts.Account _account;
        private readonly string _contractAddress;
        private readonly ILogger<BlockchainService> _logger;

        // (ABI của bạn giữ nguyên)
        private readonly string _abi = @"[
	
	
	{
		""anonymous"": false,
		""inputs"": [
			{
				""indexed"": true,
				""internalType"": ""bytes32"",
				""name"": ""lotId"",
				""type"": ""bytes32""
			},
			{
				""indexed"": false,
				""internalType"": ""uint256"",
				""name"": ""timestamp"",
				""type"": ""uint256""
			},
			{
				""indexed"": false,
				""internalType"": ""string"",
				""name"": ""status"",
				""type"": ""string""
			},
			{
				""indexed"": false,
				""internalType"": ""string"",
				""name"": ""location"",
				""type"": ""string""
			},
			{
				""indexed"": false,
				""internalType"": ""string"",
				""name"": ""metadata"",
				""type"": ""string""
			}
		],
		""name"": ""HistoryAdded"",
		""type"": ""event""
	},
	{
		""inputs"": [
			{
				""internalType"": ""string"",
				""name"": ""lotId"",
				""type"": ""string""
			},
			{
				""internalType"": ""string"",
				""name"": ""status"",
				""type"": ""string""
			},
			{
				""internalType"": ""string"",
				""name"": ""location"",
				""type"": ""string""
			},
			{
				""internalType"": ""string"",
				""name"": ""metadata"",
				""type"": ""string""
			}
		],
		""name"": ""addHistory"",
		""outputs"": [],
		""stateMutability"": ""nonpayable"",
		""type"": ""function""
	},
	{
		""inputs"": [
			{
				""internalType"": ""string"",
				""name"": ""lotId"",
				""type"": ""string""
			},
			{
				""internalType"": ""uint256"",
				""name"": ""index"",
				""type"": ""uint256""
			}
		],
		""name"": ""getHistoryByIndex"",
		""outputs"": [
			{
				""internalType"": ""uint256"",
				""name"": """",
				""type"": ""uint256""
			},
			{
				""internalType"": ""string"",
				""name"": """",
				""type"": ""string""
			},
			{
				""internalType"": ""string"",
				""name"": """",
				""type"": ""string""
			},
			{
				""internalType"": ""string"",
				""name"": """",
				""type"": ""string""
			}
		],
		""stateMutability"": ""view"",
		""type"": ""function""
	},
	{
		""inputs"": [
			{
				""internalType"": ""string"",
				""name"": ""lotId"",
				""type"": ""string""
			}
		],
		""name"": ""getHistoryCount"",
		""outputs"": [
			{
				""internalType"": ""uint256"",
				""name"": """",
				""type"": ""uint256""
			}
		],
		""stateMutability"": ""view"",
		""type"": ""function""
	}


]";

        public BlockchainService(IConfiguration configuration, ILogger<BlockchainService> logger)
        {
            _logger = logger;
            var privateKey = configuration["Blockchain:PrivateKey"];
            var rpcUrl = configuration["Blockchain:RpcUrl"];
            _contractAddress = configuration["Blockchain:ContractAddress"];

            _account = new Nethereum.Web3.Accounts.Account(privateKey);
            _web3 = new Web3(_account, rpcUrl);
            _contract = _web3.Eth.GetContract(_abi, _contractAddress);

            // <<< ================= THÊM 3 DÒNG NÀY ================= >>>
            _logger.LogWarning("--- CẤU HÌNH BLOCKCHAIN SERVICE ĐANG CHẠY ---");
            _logger.LogWarning("RPC URL (Ganache): {RpcUrl}", rpcUrl);
            _logger.LogWarning("TÀI KHOẢN (Người gửi): {AccountAddress}", _account.Address);
            _logger.LogWarning("HỢP ĐỒNG (Người nhận): {ContractAddress}", _contractAddress);
            // <<< ================= KẾT THÚC THÊM ================= >>>
        }

        public async Task<object> GhiNhatKyAsync(
     string lotId, string status, string location, string metadata)
        {
            try
            {
                var addFunction = _contract.GetFunction("addHistory");

                // Estimate gas
                var gas = await addFunction.EstimateGasAsync(
                    _account.Address, null, null, lotId, status, location, metadata);

                gas = new HexBigInteger(gas.Value + (gas.Value / 6));

                // Send TX và nhận hash ngay lập tức
                var txHash = await addFunction.SendTransactionAsync(
                    _account.Address, gas, null, lotId, status, location, metadata);
                var receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
                


                if (receipt != null && receipt.Status.Value == 1)
                {
                    Console.WriteLine("TX mined thành công!");
                }
                else
                {
                    Console.WriteLine("TX chưa mined hoặc thất bại");
                }

                return new
                {
                    success = true,
                    TxHash = txHash
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    success = false,
                    message = ex.Message
                };
            }
        }


        public async Task<object> GetHistoryByTxHashAsync(string txHash)
        {
            try
            {
                var receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
                if (receipt == null)
                    return new { success = false, message = "Không tìm thấy receipt" };

                var eventHistory = _contract.GetEvent("HistoryAdded");
                var decodedLogs = eventHistory.DecodeAllEventsForEvent<HistoryAddedEventDTO>(receipt.Logs.ToArray());

                if (!decodedLogs.Any())
                    return new { success = false, message = "Không tìm thấy event trong log" };

                var evt = decodedLogs.First().Event;

                return new
                {
                    success = true,
                    data = new
                    {
                        txHash = txHash,
                        block = receipt.BlockNumber.Value,
                        lotId = evt.LotId,
                        timestamp = evt.Timestamp,
                        status = evt.Status,
                        location = evt.Location,
                        metadata = evt.Metadata
                    }
                };
            }
            catch (Exception ex)
            {
                return new { success = false, message = ex.Message };
            }
        }




        // <<< ================= SỬA LẠI HÀM NÀY ================= >>>
        public async Task<List<HistoryAddedEventDTO>> GetHistoryAsync(string maLo)
        {
            var result = new List<HistoryAddedEventDTO>();
            try
            {
                var getCountFunction = _contract.GetFunction("getHistoryCount");
                var count = await getCountFunction.CallAsync<BigInteger>(maLo);
                _logger.LogInformation("📦 Lô {MaLo} có {Count} bản ghi.", maLo, count);

                var getByIndexFunction = _contract.GetFunction("getHistoryByIndex");

                for (int i = 0; i < (int)count; i++)
                {
                    // 1. Gọi hàm và hứng bằng DTO chuyên dụng cho Output
                    var eventRaw = await getByIndexFunction.CallDeserializingToObjectAsync<HistoryAddedEventDTO>(maLo, i);

                    // 2. Chuyển đổi thủ công sang DTO chính của bạn
                    if (eventRaw != null)
                    {
                        result.Add(new HistoryAddedEventDTO
                        {
                            Timestamp = eventRaw.Timestamp,
                            Status = eventRaw.Status,
                            Location = eventRaw.Location,
                            Metadata = eventRaw.Metadata
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đọc Blockchain.");
            }
            return result;
        }
        // <<< ================= KẾT THÚC SỬA ================= >>>

        public async Task<HistoryAddedEventDTO?> GetHistoryByTxHash(string txHash)
        {
            var receipt = await _web3.Eth.Transactions
                .GetTransactionReceipt
                .SendRequestAsync(txHash);

            var eventHandler = _web3.Eth.GetEvent<HistoryAddedEventDTO>(_contractAddress);

            var events = eventHandler.DecodeAllEventsForEvent(receipt.Logs);

            if (events == null || events.Count == 0)
                return null;   // ✅ Không có event nào

            var ev = events[0].Event;

            Console.WriteLine($"Lot: {ev.LotId}");
            Console.WriteLine($"Status: {ev.Status}");
            Console.WriteLine($"Location: {ev.Location}");
            Console.WriteLine($"Metadata: {ev.Metadata}");

            return ev;   // ✅ Trả về giá trị
        }


        public async Task<List<HistoryAddedEventDTO>> GetHistoryByLotId(string lotId)
        {
            var countFunction = _contract.GetFunction("getHistoryCount");
            var getFunction = _contract.GetFunction("getHistoryByIndex");

            var count = await countFunction.CallAsync<int>(lotId);

            var list = new List<HistoryAddedEventDTO>();

            for (int i = 0; i < count; i++)
            {
                var result = await getFunction.CallAsync<HistoryAddedEventDTO>(lotId, i);
                list.Add(result);
            }

            return list;
        }

        // (Hàm TestBlockchainAsync giữ nguyên)
        public async Task TestBlockchainAsync()
        {
            try
            {
                _logger.LogInformation("===== BẮT ĐẦU KIỂM TRA GHI & ĐỌC BLOCKCHAIN =====");
                string lotId = "TEST_AUTO_" + DateTime.Now.ToString("yyyyMMddHHmmss");

                var events = await GetHistoryAsync(lotId);
                if (events.Count > 0)
                {
                    _logger.LogInformation("✅ Đọc lại thành công, tổng {Count} bản ghi.", events.Count);
                    foreach (var e in events)
                        _logger.LogInformation($"🕓 {e.Timestamp} | {e.Status} @ {e.Location} | {e.Metadata}");
                }
                else
                {
                    _logger.LogWarning("⚠️ Không tìm thấy bản ghi nào trong blockchain cho {LotId}", lotId);
                }

                _logger.LogInformation("===== KIỂM TRA HOÀN TẤT =====");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong quá trình test tự động Blockchain.");
            }
        }

    }

    // <<< ================= THÊM CLASS NÀY VÀO ================= >>>
    // Class này chỉ dùng để "hứng" 4 giá trị trả về không tên của hàm getHistoryByIndex

    [Event("HistoryAdded")]
    public class HistoryAddedEventDTO : IEventDTO
    {
        [Parameter("bytes32", "lotId", 1, true)]
        public byte[] LotId { get; set; }

        [Parameter("uint256", "timestamp", 2, false)]
        public BigInteger Timestamp { get; set; }

        [Parameter("string", "status", 3, false)]
        public string Status { get; set; }

        [Parameter("string", "location", 4, false)]
        public string Location { get; set; }

        [Parameter("string", "metadata", 5, false)]
        public string Metadata { get; set; }
    }
    // <<< ================= KẾT THÚC THÊM ================= >>>
}