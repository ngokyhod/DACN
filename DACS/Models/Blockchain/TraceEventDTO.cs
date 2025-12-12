using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Collections.Generic;
using System.Numerics;

namespace DACS.Models.Blockchain
{
    [FunctionOutput]
    public class GetHistoryOutputDTO : IFunctionOutputDTO
    {
        [Parameter("tuple[]", "", 1)]
        public List<TraceEventDTO> Events { get; set; }
    }

    [FunctionOutput]
    public class TraceEventDTO
    {

        [Parameter("uint256", "timestamp", 1)]
        public BigInteger Timestamp { get; set; }
        public DateTime GetDateTime()
        {
            // Kiểm tra và chuyển đổi BigInteger sang long an toàn
            if (Timestamp > long.MaxValue)
            {
                // Xử lý trường hợp BigInteger quá lớn nếu cần
                return DateTime.MinValue;
            }

            // Timestamp được lưu trên EVM thường là giây (Unix Epoch Time)
            return DateTimeOffset.FromUnixTimeSeconds((long)Timestamp).LocalDateTime;
        }

        [Parameter("string", "status", 2)]
        public string Status { get; set; }

        [Parameter("string", "location", 3)]
        public string Location { get; set; }

        [Parameter("string", "metadata", 4)]
        public string Metadata { get; set; }
    }
}
