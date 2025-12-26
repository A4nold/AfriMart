namespace MarketService.Domain.Interface;

public interface IMarketRepository
{
    Task<Domain.Entities.Market?> GetByPubKeyAsync(string marketPubKey, CancellationToken ct);

    // Natural idempotency for Create (seed + authority)
    Task<Domain.Entities.Market?> FindByAuthorityAndSeedAsync(string authorityPubKey, ulong marketSeedId, CancellationToken ct);

    Task AddAsync(Domain.Entities.Market market, CancellationToken ct);
    
}