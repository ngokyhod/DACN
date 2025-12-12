using DACS.Models.Blockchain;
using System.Collections.Generic;

namespace DACS.Models.Blockchain
{
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
}
