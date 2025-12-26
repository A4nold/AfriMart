using MarketService.Domain.Entities;
using MarketService.Domain.Interface;
using MarketService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MarketService.Infrastructure.Repositories;

public class UserPositionRepository : IUserPositionRepository
{
    private readonly MarketDbContext _db;
    private readonly IClock _clock;

    public UserPositionRepository(MarketDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task UpsertAfterTradeAsync(
        Guid userId,
        Guid marketId,
        byte outcomeIndex,
        string txSig,
        CancellationToken ct)
    {
        // Note: outcomeIndex + txSig are not persisted in UserMarketPosition with your current schema.
        // They should live in MarketAction (ledger). This snapshot row just ensures PortfolioService has a row to read.

        var set = _db.Set<UserMarketPosition>();

        var existing = await set.FirstOrDefaultAsync(
            x => x.UserId == userId && x.MarketId == marketId,
            ct);

        if (existing == null)
        {
            var created = new UserMarketPosition
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MarketId = marketId,

                // Optional: if you don't know PositionPubKey yet, keep it empty.
                // If you *do* know it elsewhere, update it in a different call.
                PositionPubKey = string.Empty,

                YesShares = 0,
                NoShares = 0,
                Claimed = false,

                LastSyncedSlot = null,
                LastSyncedAtUtc = _clock.UtcNow
            };

            await set.AddAsync(created, ct);
            return;
        }

        // Don’t touch share numbers here (chain is truth) unless you’re actually syncing them.
        // This just records “we had activity” so the read model can decide to refresh.
        existing.LastSyncedAtUtc = _clock.UtcNow;
    }
    
    // Invariant: UserMarketPosition must exist before claim.
    // If missing, state is inconsistent and must be investigated.
    public async Task MarkClaimedAsync(Guid userId, Guid marketId, CancellationToken ct)
    {
        // Note: txSig should be persisted on MarketAction. Snapshot just marks claimed.
        var set = _db.Set<UserMarketPosition>();

        var existing = await set.FirstOrDefaultAsync(
            x => x.UserId == userId && x.MarketId == marketId,
            ct);

        if (existing == null)
        {
            // This should not normally happen.
            // Either:
            // - snapshot drift
            // - out-of-order execution
            // - bug
            throw new InvalidOperationException(
                $"Cannot mark claimed: no UserMarketPosition exists for user {userId} on market {marketId}");
        }

        if (existing.Claimed)
        {
            // Idempotent safety: already claimed
            return;
        }

        existing.Claimed = true;
        existing.LastSyncedAtUtc = _clock.UtcNow;
    }

    public async Task EnsureExistsAsync(Guid userId, Guid marketId, CancellationToken ct)
    {
        var exists = await _db.Set<UserMarketPosition>()
            .AnyAsync(x => x.UserId == userId && x.MarketId == marketId, ct);

        if (!exists)
        {
            throw new InvalidOperationException(
                $"UserMarketPosition does not exist for user {userId} and market {marketId}.");
        }
    }
}