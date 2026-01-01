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
        string ownerPubkey,
        string positionPubkey,
        ulong yesShares,
        ulong noShares,
        bool claimed,
        ulong? lastSyncedSlot,
        CancellationToken ct)
    {
        var set = _db.Set<UserMarketPosition>();

        var existing = await set.FirstOrDefaultAsync(
            x => x.UserId == userId && x.MarketId == marketId,
            ct);
        
        var now = _clock.UtcNow;

        if (existing is null)
        {
            var created = new UserMarketPosition
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MarketId = marketId,
                
                OwnerPubkey = ownerPubkey,
                PositionPubKey = positionPubkey,

                YesShares = yesShares,
                NoShares = noShares,
                Claimed = claimed,

                LastSyncedSlot = lastSyncedSlot,
                LastSyncedAtUtc = now,
                
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await set.AddAsync(created, ct);
            return;
        }

        // Update snapshot with chain truth
        existing.OwnerPubkey = ownerPubkey;
        existing.PositionPubKey = positionPubkey;
        existing.YesShares = yesShares;
        existing.NoShares = noShares;
        existing.Claimed = claimed;
        existing.LastSyncedSlot = lastSyncedSlot;
        existing.LastSyncedAtUtc = now;
        
        existing.UpdatedAtUtc = now;
    }

    public async Task MarkClaimedAsync(Guid userId, Guid marketId, CancellationToken ct)
    {
        // Note: txSig should be persisted on MarketAction. Snapshot just marks claimed.
        var set = _db.Set<UserMarketPosition>();

        var existing = await set.FirstOrDefaultAsync(
            x => x.UserId == userId && x.MarketId == marketId,
            ct);

        if (existing == null)
        {
            throw new InvalidOperationException(
                $"Cannot mark claimed: no UserMarketPosition exists for user {userId} on market {marketId}");
        }
        
        if (string.IsNullOrWhiteSpace(existing.PositionPubKey))
        {
            throw new InvalidOperationException(
                $"Cannot mark claimed: PositionPubKey missing for user {userId} on market {marketId}");
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