using MarketService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PortfolioService.Domain.Interface;
using PortfolioService.Domain.Models;
using PortfolioService.Infrastructure.Data;

namespace PortfolioService.Infrastructure.Repository;

public class PortfolioService : IPortfolioService
{
    private readonly PortfolioDbContext _db;
    private readonly ILogger<PortfolioService> _logger;

    public PortfolioService(PortfolioDbContext db, ILogger<PortfolioService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PortfolioOverviewDto> GetUserPortfolioAsync(Guid userId, CancellationToken ct = default)
    {
        // Load positions with markets + outcomes
        var positions = await _db.UserMarketPositions
            .Include(p => p.Market)
            .ThenInclude(m => m.Outcomes)
            .Where(p => p.UserId == userId)
            .ToListAsync(ct);

        var overview = new PortfolioOverviewDto
        {
            UserId = userId
        };

        foreach (var p in positions)
        {
            var market = p.Market;
            // var winningIndex = market.WinningOutcomeIndex; // assume byte? type
            // var isResolved = market.Status == MarketStatus.Resolved;
            //bool? won = null;

            // if (isResolved && winningIndex.HasValue)
            // {
            //     won = (byte)p.OutcomeIndex == winningIndex.Value;
            // }

            // var outcomeLabel = market.Outcomes
            //     .FirstOrDefault(o => o.OutcomeIndex == p.OutcomeIndex)?.Label ?? "";

            var dto = new PositionDto
            {
                // PositionId = p.Id,
                // MarketId = p.MarketId,
                // MarketQuestion = market.Question,
                // // MarketStatus = market.Status.ToString(),
                // // MarketEndTime = market.EndTime,
                //
                // OutcomeIndex = p.OutcomeIndex,
                // OutcomeLabel = outcomeLabel,
                //
                // StakeAmount = p.StakeAmount,
                // Claimed = p.Claimed,
                // Won = won,
                //
                // TxSignature = p.TxSignature,
                //
                // PlacedAt = p.PlacedAt,
                // ClaimedAt = p.ClaimedAt
            };

            // if (!isResolved)
            // {
            //     overview.OpenPositions.Add(dto);
            // }
            // else
            // {
            //     overview.ResolvedPositions.Add(dto);
            // }
        }

        // Claimable = resolved, not claimed, won == true
        overview.ClaimablePositions = overview.ResolvedPositions
            .Where(r => r.Won == true && !r.Claimed)
            .Select(r => new ClaimablePositionDto
            {
                PositionId = r.PositionId,
                MarketId = r.MarketId,
                MarketQuestion = r.MarketQuestion,
                OutcomeLabel = r.OutcomeLabel,
                ClaimableAmount = r.StakeAmount, // for now; you can later use true payout
                ResolvedAt = r.ClaimedAt ?? DateTime.UtcNow, // or from resolution
                WinningOutcomeIndex = (byte)r.OutcomeIndex
            })
            .ToList();

        return overview;
    }
}
