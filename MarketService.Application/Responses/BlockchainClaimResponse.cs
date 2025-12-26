namespace MarketService.Application.Responses;

public sealed record BlockchainClaimResponse(
    string MarketPubkey,
    string TransactionSignature
);