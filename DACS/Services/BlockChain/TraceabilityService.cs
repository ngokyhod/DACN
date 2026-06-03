using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

namespace DACS.Services
{
    // 1. ĐỊNH NGHĨA CẤU TRÚC PHẦN TỬ
    [Struct("TrackingEvent")]
    public class TrackingEventDTO
    {
        [Parameter("string", "action", 1)]
        public string Action { get; set; }

        [Parameter("string", "actor", 2)]
        public string Actor { get; set; }

        [Parameter("string", "location", 3)]
        public string Location { get; set; }

        [Parameter("string", "details", 4)]
        public string Details { get; set; }

        [Parameter("uint256", "timestamp", 5)]
        public BigInteger Timestamp { get; set; }
    }

    // 2. VỎ BỌC ĐỂ NHẬN MẢNG TRẢ VỀ (CÁCH CỔ ĐIỂN)
    [FunctionOutput]
    public class GetHistoryOutputDTO : IFunctionOutputDTO
    {
        // Thuộc tính này sẽ hứng nguyên cái mảng tuple[]
        [Parameter("tuple[]", "", 1)]
        public virtual List<TrackingEventDTO> Events { get; set; }
    }

    public class TraceabilityService
    {
        private readonly string _rpcUrl;
        private readonly string _contractAddress;
        private readonly string _privateKey;
        private readonly ILogger<TraceabilityService> _logger;

        // GIỮ NGUYÊN BẢN ABI HOÀN CHỈNH NÀY
        private readonly string _abi = @"[ { ""anonymous"": false, ""inputs"": [ { ""indexed"": true, ""internalType"": ""string"", ""name"": ""trackingId"", ""type"": ""string"" }, { ""indexed"": false, ""internalType"": ""string"", ""name"": ""action"", ""type"": ""string"" }, { ""indexed"": false, ""internalType"": ""string"", ""name"": ""actor"", ""type"": ""string"" }, { ""indexed"": false, ""internalType"": ""uint256"", ""name"": ""timestamp"", ""type"": ""uint256"" } ], ""name"": ""HistoryAdded"", ""type"": ""event"" }, { ""inputs"": [ { ""internalType"": ""string"", ""name"": ""_trackingId"", ""type"": ""string"" }, { ""internalType"": ""string"", ""name"": ""_action"", ""type"": ""string"" }, { ""internalType"": ""string"", ""name"": ""_actor"", ""type"": ""string"" }, { ""internalType"": ""string"", ""name"": ""_location"", ""type"": ""string"" }, { ""internalType"": ""string"", ""name"": ""_details"", ""type"": ""string"" } ], ""name"": ""addHistory"", ""outputs"": [], ""stateMutability"": ""nonpayable"", ""type"": ""function"" }, { ""inputs"": [ { ""internalType"": ""string"", ""name"": ""_trackingId"", ""type"": ""string"" } ], ""name"": ""getHistory"", ""outputs"": [ { ""components"": [ { ""internalType"": ""string"", ""name"": ""action"", ""type"": ""string"" }, { ""internalType"": ""string"", ""name"": ""actor"", ""type"": ""string"" }, { ""internalType"": ""string"", ""name"": ""location"", ""type"": ""string"" }, { ""internalType"": ""string"", ""name"": ""details"", ""type"": ""string"" }, { ""internalType"": ""uint256"", ""name"": ""timestamp"", ""type"": ""uint256"" } ], ""internalType"": ""struct Traceability.TrackingEvent[]"", ""name"": """", ""type"": ""tuple[]"" } ], ""stateMutability"": ""view"", ""type"": ""function"" } ]";

        public TraceabilityService(IConfiguration config, ILogger<TraceabilityService> logger)
        {
            _rpcUrl = config["Blockchain:RpcUrl"];
            _contractAddress = config["Blockchain:ContractAddress"];
            _privateKey = config["Blockchain:PrivateKey"];
            _logger = logger;
        }

        // =========================================================================
        // HÀM GHI (Đã fix lỗi màng bảo vệ null)
        // =========================================================================
        public async Task<string> GhiNhatKyAsync(string maYeuCau, string hanhDong, string nguoiThucHien, string viTri, string chiTiet)
        {
            try
            {
                string safeMaYeuCau = maYeuCau?.Trim() ?? "";
                string safeHanhDong = hanhDong?.Trim() ?? "";
                string safeNguoiThucHien = nguoiThucHien?.Trim() ?? "Hệ thống";
                string safeViTri = viTri?.Trim() ?? "Không rõ";
                string safeChiTiet = chiTiet?.Trim() ?? "";

                if (string.IsNullOrEmpty(safeMaYeuCau)) return null;

                var account = new Account(_privateKey);
                var web3 = new Web3(account, _rpcUrl);
                var contract = web3.Eth.GetContract(_abi, _contractAddress);
                var addHistoryFunction = contract.GetFunction("addHistory");

                var gas = new HexBigInteger(3000000);
                var value = new HexBigInteger(0);

                string txHash = await addHistoryFunction.SendTransactionAsync(
                    account.Address, gas, value, safeMaYeuCau, safeHanhDong, safeNguoiThucHien, safeViTri, safeChiTiet);

                _logger.LogInformation($"[Blockchain Success] Đã ghi block cho {safeMaYeuCau}. TxHash: {txHash}");
                return txHash;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Blockchain Error] Lỗi ghi: {ex.Message}");
                return null;
            }
        }

        // =========================================================================
        // HÀM ĐỌC (SỬ DỤNG CÁCH CỔ ĐIỂN CHẮC CHẮN NHẤT)
        // =========================================================================
        public async Task<List<TrackingEventDTO>> LayLichSuAsync(string maYeuCau)
        {
            try
            {
                string cleanId = maYeuCau?.Trim() ?? "";

                var account = new Account(_privateKey);
                var web3 = new Web3(account, _rpcUrl);

                // 1. Lấy thẳng Hợp đồng ra
                var contract = web3.Eth.GetContract(_abi, _contractAddress);

                // 2. Lấy thẳng Hàm ra
                var getHistoryFunction = contract.GetFunction("getHistory");

                // 3. Gọi hàm và bọc kết quả vào Output DTO
                var result = await getHistoryFunction.CallDeserializingToObjectAsync<GetHistoryOutputDTO>(cleanId);

                // 4. Lấy danh sách bên trong ra
                return result?.Events ?? new List<TrackingEventDTO>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"\n\n=== LỖI ĐỌC GANACHE ===\n{ex}\n========================\n");
                _logger.LogError($"[Blockchain Read Error]: {ex.Message}");
                return new List<TrackingEventDTO>();
            }
        }
    }
}