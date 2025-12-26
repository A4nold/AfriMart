namespace MarketService.Application.Responses;

public sealed record BlockchainResolveMarketResponse(
    string MarketPubkey,
    string TransactionSignature
);