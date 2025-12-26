namespace MarketService.Application.Requests;

public sealed record BlockchainClaimRequest(
    string MarketPubkey
);