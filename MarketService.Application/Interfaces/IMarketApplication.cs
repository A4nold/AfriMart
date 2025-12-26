using MarketService.Application.Commands;

namespace MarketService.Application.Interfaces;

public interface IMarketApplication
{
    Task<CreateMarketResult> CreateMarketAsync(CreateMarketCommand cmd, CancellationToken ct);
    Task<ResolveMarketResult> ResolveMarketAsync(ResolveMarketCommand cmd, CancellationToken ct);
    Task<BuySharesResult> BuySharesAsync(BuySharesCommand cmd, CancellationToken ct);
    Task<SellSharesResult> SellSharesAsync(SellSharesCommand cmd, CancellationToken ct);
    Task<ClaimWinningsResult> ClaimWinningsAsync(ClaimWinningsCommand cmd, CancellationToken ct);
}