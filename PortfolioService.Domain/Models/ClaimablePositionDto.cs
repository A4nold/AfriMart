using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortfolioService.Domain.Models
{
    public sealed class ClaimablePositionDto
    {
        public Guid PositionId { get; set; }
        public Guid MarketId { get; set; }
        public string MarketQuestion { get; set; } = default!;
        public string OutcomeLabel { get; set; } = default!;
        public ulong ClaimableAmount { get; set; }

        public DateTime ResolvedAt { get; set; }
        public byte WinningOutcomeIndex { get; set; }
    }
}
