using MarketService.Domain.Entities;
using MarketService.Domain.Interface;
using Microsoft.EntityFrameworkCore;
using MarketService.Infrastructure.Data;
using Npgsql;

namespace MarketService.Infrastructure.Repositories;

public sealed class MarketActionRepository : IMarketActionRepository
{
    private readonly MarketDbContext _db;

    public MarketActionRepository(MarketDbContext db)
    {
        _db = db;
    }

    public async Task<MarketAction> GetOrCreateAsync(MarketAction proposed, CancellationToken ct)
    {
        // Fast path: already exists
        var existing = await _db.MarketActions
            .FirstOrDefaultAsync(x => x.IdempotencyKey == proposed.IdempotencyKey, ct);

        if (existing != null)
            return existing;

        // Try to insert; unique index on IdempotencyKey enforces idempotency
        _db.MarketActions.Add(proposed);

        try
        {
            await _db.SaveChangesAsync(ct);
            return proposed;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Another request won the race — detach our proposed entity
            _db.Entry(proposed).State = EntityState.Detached;

            // Load the canonical row
            var winner = await _db.MarketActions
                .FirstAsync(x => x.IdempotencyKey == proposed.IdempotencyKey, ct);

            return winner;
        }
    }

    public Task<MarketAction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
        => _db.MarketActions
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);

    public Task<MarketAction?> GetLatestForMarketAsync(Guid marketId, MarketActionType type, CancellationToken ct)
        => _db.MarketActions
            .Where(x => x.MarketId == marketId && x.ActionType == type)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
    
    public Task<MarketAction?> GetLatestForMarketAndUserAsync(
        Guid marketId,
        Guid userId,
        MarketActionType type,
        CancellationToken ct)
        => _db.Set<MarketAction>()
            .Where(x =>
                x.MarketId == marketId &&
                x.RequestedByUserId == userId &&
                x.ActionType == type &&
                x.State == ActionState.Confirmed &&
                x.TxSignature != null &&
                x.TxSignature != "")
            .OrderByDescending(x => x.ConfirmedAtUtc ?? x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
    
    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        // PostgreSQL unique constraint violation = SQLSTATE 23505
        if (ex.InnerException is PostgresException pg &&
            pg.SqlState == PostgresErrorCodes.UniqueViolation)
            return true;

        return false;
    }
}
