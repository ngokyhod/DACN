using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;

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
