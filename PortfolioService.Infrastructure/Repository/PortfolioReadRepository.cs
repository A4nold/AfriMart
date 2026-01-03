using MarketService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PortfolioService.Domain.Interface;
using PortfolioService.Domain.Models;
using PortfolioService.Infrastructure.Data;
using MarketStatus = PortfolioService.Domain.Models.MarketStatus;

namespace PortfolioService.Infrastructure.Repository;

public class PortfolioReadRepository : IPortfolioReadRepository
{
    private readonly PortfolioDbContext _db;
    private readonly ILogger<PortfolioReadRepository> _logger;

    public PortfolioReadRepository(PortfolioDbContext db, ILogger<PortfolioReadRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<List<UserMarketPositionDto>> GetAllAsync(Guid userId, CancellationToken ct)
        => BaseQuery(userId).ToListAsync(ct);


    public Task<List<UserMarketPositionDto>> GetOpenAsync(Guid userId, CancellationToken ct)
        => BaseQuery(userId)
            .Where(x => x.Status == MarketStatus.Open && x.HasExposure)
            .OrderByDescending(x => x.EndTimeUtc)
            .ToListAsync(ct);

    public Task<List<UserMarketPositionDto>> GetResolvedAsync(Guid userId, CancellationToken ct)
        => BaseQuery(userId)
            .Where(x => x.Status == MarketStatus.Resolved)
            .OrderByDescending(x => x.EndTimeUtc)
            .ToListAsync(ct);

    public Task<UserMarketPositionDto?> GetByMarketIdAsync(Guid userId, Guid marketId,
        CancellationToken ct)
        => BaseQuery(userId)
            .Where(x => x.MarketId == marketId)
            .SingleOrDefaultAsync(ct);
    
    private IQueryable<UserMarketPositionDto> BaseQuery(Guid userId)
    {
        return _db.UserMarketPositions
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new
            {
                Pos = p,
                Market = p.Market,
            })
            .OrderByDescending(x => x.Market.EndTimeUtc)
            .Select(x => new UserMarketPositionDto(
                x.Pos.MarketId,
                x.Market.MarketPubKey,
                x.Market.Question,
                x.Market.EndTimeUtc,
                x.Pos.YesShares,
                x.Pos.NoShares,
                x.Pos.Claimed,
                (PortfolioService.Domain.Models.MarketStatus?)x.Market.Status,
                x.Market.WinningOutcomeIndex,
                (x.Pos.YesShares > 0 || x.Pos.NoShares > 0),
                (x.Pos.YesShares > 0 && x.Pos.NoShares > 0) ? ExposureSide.Mixed :
                (x.Pos.YesShares > 0) ? ExposureSide.Yes : (x.Pos.NoShares > 0) ? ExposureSide.No : ExposureSide.None,
                x.Market.Status == MarketService.Domain.Entities.MarketStatus.Resolved,
                (PortfolioService.Domain.Models.MarketStatus?)x.Market.Status == MarketStatus.Resolved &&
                (
                    (x.Market.WinningOutcomeIndex == 0 && x.Pos.YesShares > 0) ||
                    (x.Market.WinningOutcomeIndex == 1 && x.Pos.NoShares > 0)
                ),
                x.Market.Status == MarketService.Domain.Entities.MarketStatus.Resolved &&
                !x.Pos.Claimed && ((x.Market.WinningOutcomeIndex == 0 && x.Pos.YesShares > 0) ||
                               (x.Market.WinningOutcomeIndex == 1 && x.Pos.NoShares > 0)),
                x.Pos.LastSyncedAtUtc,
                x.Pos.LastSyncedSlot
            ));
    }
}
