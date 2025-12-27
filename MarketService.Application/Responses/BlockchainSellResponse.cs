namespace MarketService.Application.Responses;

public sealed record BlockchainSellResponse(
    string MarketPubkey,
    string TransactionSignature,
    string SharesSold
);