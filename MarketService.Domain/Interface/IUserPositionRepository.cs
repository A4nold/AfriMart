namespace MarketService.Domain.Interface;

public interface IUserPositionRepository
{
    // optional: if you want to store a snapshot for PortfolioService
    Task UpsertAfterTradeAsync(Guid userId, Guid marketId, byte outcomeIndex, string txSig, CancellationToken ct);
    Task MarkClaimedAsync(Guid userId, Guid marketId, CancellationToken ct);
    Task EnsureExistsAsync(Guid userId, Guid marketId, CancellationToken ct);
}