namespace MarketService.Application.Requests;

public sealed record BlockchainBuyRequest(
    string MarketPubkey,
    ulong MaxCollateralIn,
    ulong MinSharesOut,
    byte OutcomeIndex
);