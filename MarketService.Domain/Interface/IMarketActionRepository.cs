using MarketService.Domain.Entities;

namespace MarketService.Domain.Interface;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public interface IMarketActionRepository
{
    // MUST be concurrency-safe. Implement with unique index on IdempotencyKey.
    Task<MarketAction> GetOrCreateAsync(MarketAction proposed, CancellationToken ct);
    Task<MarketAction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct);
    Task<MarketAction?> GetLatestForMarketAsync(Guid marketId, MarketActionType type, CancellationToken ct);
    Task<MarketAction?> GetLatestForMarketAndUserAsync(Guid marketId, Guid userId, MarketActionType type, CancellationToken ct);
}

