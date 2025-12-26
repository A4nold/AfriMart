namespace MarketService.Application.Requests;

public sealed record BlockchainCreateMarketRequest(
    ulong MarketId,
    string Question,
    DateTime EndTimeUtc,
    ulong InitialLiquidity,
    string CollateralMint
);