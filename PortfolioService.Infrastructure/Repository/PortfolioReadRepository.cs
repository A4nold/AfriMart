using System.Linq.Expressions;
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
        => BaseEntityQuery(userId).OrderByDescending(p => p.Market.EndTimeUtc)
            .Select(MapExpr).ToListAsync(ct);


    public Task<List<UserMarketPositionDto>> GetOpenAsync(Guid userId, CancellationToken ct)
        => BaseEntityQuery(userId)
            .Where(p => p.Market.Status == MarketService.Domain.Entities.MarketStatus.Open)
            .Where(p => p.YesShares > 0 || p.NoShares > 0)
            .OrderByDescending(p => p.Market.EndTimeUtc)
            .Select(MapExpr).ToListAsync(ct);

    public Task<List<UserMarketPositionDto>> GetResolvedAsync(Guid userId, CancellationToken ct)
        => BaseEntityQuery(userId)
            .Where(p => p.Market.Status == MarketService.Domain.Entities.MarketStatus.Resolved)
            .Where(p => p.YesShares > 0 || p.NoShares > 0)
            .OrderByDescending(p => p.Market.EndTimeUtc)
            .Select(MapExpr).ToListAsync(ct);

    public Task<UserMarketPositionDto?> GetByMarketIdAsync(Guid userId, Guid marketId,
        CancellationToken ct)
        => BaseEntityQuery(userId)
            .Where(p => p.MarketId == marketId)
            .Select(MapExpr)
            .SingleOrDefaultAsync(ct);

    private IQueryable<PositionRow> BaseRows(Guid userId)
    {
        return _db.UserMarketPositions
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.Market.EndTimeUtc)
            .Select(p => new PositionRow(
                p.MarketId,
                p.Market.MarketPubKey,
                p.Market.Question,
                p.Market.EndTimeUtc,
                p.YesShares,
                p.NoShares,
                p.Claimed,
                (PortfolioService.Domain.Models.MarketStatus?)p.Market.Status,
                p.Market.WinningOutcomeIndex,
                p.LastSyncedAtUtc,
                p.LastSyncedSlot
            ));
    }

    private IQueryable<UserMarketPosition> BaseEntityQuery(Guid userId)
    {
        return _db.UserMarketPositions
            .AsNoTracking()
            .Where(p => p.UserId == userId);
    }

    private static readonly Expression<Func<UserMarketPosition, UserMarketPositionDto>> MapExpr =
        p => new UserMarketPositionDto(
            p.MarketId,
            p.Market.MarketPubKey,
            p.Market.Question,
            p.Market.EndTimeUtc,
            p.YesShares,
            p.NoShares,
            p.Claimed,

            (MarketStatus?)p.Market.Status,
            p.Market.WinningOutcomeIndex,
            (p.YesShares > 0UL || p.NoShares > 0UL),
            (p.YesShares > 0UL && p.NoShares > 0UL) ? ExposureSide.Mixed
            : (p.YesShares > 0UL) ? ExposureSide.Yes
            : (p.NoShares > 0UL) ? ExposureSide.No : ExposureSide.None,
            ((MarketStatus?)p.Market.Status) == MarketStatus.Resolved,
            ((MarketStatus?)p.Market.Status) == MarketStatus.Resolved
            && p.Market.WinningOutcomeIndex != null
            && (
                (p.Market.WinningOutcomeIndex == (byte)0 && p.YesShares > 0UL) ||
                (p.Market.WinningOutcomeIndex == (byte)1 && p.NoShares > 0UL)
            ),
            ((MarketStatus?)p.Market.Status) == MarketStatus.Resolved
            && !p.Claimed
            && p.Market.WinningOutcomeIndex != null
            && (
                (p.Market.WinningOutcomeIndex == (byte)0 && p.YesShares > 0UL) ||
                (p.Market.WinningOutcomeIndex == (byte)1 && p.NoShares > 0UL)),
            p.LastSyncedAtUtc,
            p.LastSyncedSlot
        );
}
