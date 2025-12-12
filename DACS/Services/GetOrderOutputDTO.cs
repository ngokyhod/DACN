using Nethereum.ABI.FunctionEncoding.Attributes;
using DACS.Services;
namespace DACS.Services
{
    [FunctionOutput]
    public class GetOrderOutputDTO : IFunctionOutputDTO
    {
        [Parameter("string", "M_DonHang", 1)]
        public string M_DonHang { get; set; }

        [Parameter("string", "status", 2)]
        public string TrangThai { get; set; }

        [Parameter("string", "location", 3)]
        public string ShippingAddress { get; set; }

        [Parameter("string", "metadata", 4)]
        public string Metadata { get; set; }
    }
}