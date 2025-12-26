using MarketService.Application.Requests;
using MarketService.Application.Responses;

namespace MarketService.Application.Interfaces;

public interface IBlockchainGateway
{
    string ProgramId { get; }
    string AuthorityPubKey { get; } // for seed-derivation alignment (your MVP uses backend authority)

    Task<BlockchainCreateMarketResponse> CreateMarketAsync(BlockchainCreateMarketRequest req, CancellationToken ct);
    Task<BlockchainResolveMarketResponse> ResolveMarketAsync(BlockchainResolveMarketRequest req, CancellationToken ct);
    Task<BlockchainBuyResponse> BuySharesAsync(BlockchainBuyRequest req, CancellationToken ct);
    Task<BlockchainSellResponse> SellSharesAsync(BlockchainSellRequest req, CancellationToken ct);
    Task<BlockchainClaimResponse> ClaimWinningsAsync(BlockchainClaimRequest req, CancellationToken ct);
}