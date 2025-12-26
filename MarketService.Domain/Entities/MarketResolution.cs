using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketService.Domain.Entities
{
    public class MarketResolution
    {
        public Guid Id { get; set; }

        public Guid MarketId { get; set; }
        public Market Market { get; set; } = default!;

        public int WinningOutcomeIndex { get; set; }
        public ResolutionSource Source { get; set; }
        
        public Guid ResolvedByUserId { get; set; }
        public string ResolveTxSignature { get; set; } = default!;
        
        public string? EvidenceUrl { get; set; }      // optional: link to API / news / tx
        public string? Notes { get; set; }

        public DateTime ResolvedAt { get; set; }
    }
}
