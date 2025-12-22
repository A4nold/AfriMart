namespace BlockchainService.Api.Models.Responses;

public record SellSharesResponse(
    string MarketPubkey,
    string UserCollateralAta,
    ulong SharesIn,
    ulong MinCollateralOut,
    byte OutcomeIndex,
    string TransactionSignature
);