using MarketService.Domain.Entities;
using MarketService.Domain.Interface;
using MarketService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MarketService.Infrastructure.Repositories;

public class MarketRepository : IMarketRepository
{
    private readonly MarketDbContext _db;

    public MarketRepository(MarketDbContext db)
    {
        _db = db;
    }

    public Task<Market?> GetByPubKeyAsync(string marketPubKey, CancellationToken ct)
        => _db.Set<Market>()
            .FirstOrDefaultAsync(x => x.MarketPubKey == marketPubKey, ct);

    public Task<Market?> FindByAuthorityAndSeedAsync(string authorityPubKey, ulong marketSeedId, CancellationToken ct)
        => _db.Set<Market>()
            .FirstOrDefaultAsync(x => x.AuthorityPubKey == authorityPubKey && x.MarketSeedId == marketSeedId, ct);

    public Task AddAsync(Market market, CancellationToken ct)
        => _db.Set<Market>().AddAsync(market, ct).AsTask();
}