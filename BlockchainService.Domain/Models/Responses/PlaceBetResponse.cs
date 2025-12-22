namespace BlockchainService.Api.Models.Responses;

public record BuySharesResponse(
    string MarketPubkey,
    string UserCollateralAta,
    ulong MaxCollateralIn,
    ulong MinSharesOut,
    byte OutcomeIndex,
    string TransactionSignature
);

