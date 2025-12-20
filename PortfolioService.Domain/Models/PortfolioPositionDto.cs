using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortfolioService.Domain.Models
{
    public sealed class PortfolioPositionDto
    {
        public Guid PositionId { get; set; }
        public Guid MarketId { get; set; }
        public string MarketQuestion { get; set; } = default!;
        public string MarketStatus { get; set; } = default!;
        public DateTime MarketEndTime { get; set; }

        public int OutcomeIndex { get; set; }
        public string OutcomeLabel { get; set; } = default!;

        public ulong StakeAmount { get; set; }
        public bool Claimed { get; set; }
        public bool? Won { get; set; }

        public string? TxSignature { get; set; }

        public DateTime PlacedAt { get; set; }
        public DateTime? ClaimedAt { get; set; }
    }
}
