using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortfolioService.Domain.Models
{
    public sealed class PortfolioOverviewDto
    {
        public Guid UserId { get; set; }

        public List<PositionDto> OpenPositions { get; set; } = new List<PositionDto>();
        public List<PositionDto> ResolvedPositions { get; set; } = new List<PositionDto>();
        public List<ClaimablePositionDto> ClaimablePositions { get; set; } = new List<ClaimablePositionDto>();
    }
}
