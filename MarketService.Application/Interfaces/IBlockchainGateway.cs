using MarketService.Application.Dtos;
using MarketService.Application.Gateways;
using MarketService.Application.Requests;
using MarketService.Application.Responses;

namespace MarketService.Application.Interfaces;

public interface IBlockchainGateway
{
    string ProgramId { get; }
    string AuthorityPubKey { get; } // for seed-derivation alignment (your MVP uses backend authority)
    
    MarketPdas DeriveMarketPdas(ulong marketSeedId);

    Task<BlockchainCreateMarketResponse> CreateMarketAsync(BlockchainCreateMarketRequest req, CancellationToken ct);
    Task<BlockchainResolveMarketResponse> ResolveMarketAsync(BlockchainResolveMarketRequest req, CancellationToken ct);
    Task<BlockchainBuyResponse> BuySharesAsync(BlockchainBuyRequest req, CancellationToken ct);
    Task<BlockchainSellResponse> SellSharesAsync(BlockchainSellRequest req, CancellationToken ct);
    Task<BlockchainClaimResponse> ClaimWinningsAsync(BlockchainClaimRequest req, CancellationToken ct);
    Task<GetPositionResponse> GetPositionAsync(string marketPubkey, string userPubKey,  CancellationToken ct);
    Task<MarketStateResponse> GetMarketAsync(string marketPubkey, CancellationToken ct);
}

public sealed record BlockchainTxResult(
    string TransactionSignature,
    ulong? ConfirmedSlot = null
    // optional: IReadOnlyList<string>? Logs = null
);
