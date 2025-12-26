namespace MarketService.Application.Responses;

public sealed record BlockchainCreateMarketResponse(
    string MarketPubkey,
    string TransactionSignature
);