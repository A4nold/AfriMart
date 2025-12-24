namespace BlockchainService.Api.Models.Requests;

public record CreateMarketRequest(
    ulong MarketId,
    string Question,
    DateTime EndTime,
    ulong InitialLiquidity,
    string CollateralMint
);

