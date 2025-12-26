namespace MarketService.Application.Requests;

public sealed record BlockchainResolveMarketRequest(
    string MarketPubkey,
    byte WinningOutcomeIndex
);