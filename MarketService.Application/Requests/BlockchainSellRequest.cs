namespace MarketService.Application.Requests;

public sealed record BlockchainSellRequest(
    string MarketPubkey,
    ulong SharesIn,
    ulong MinCollateralOut,
    byte OutcomeIndex
);