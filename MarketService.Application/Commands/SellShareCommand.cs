namespace MarketService.Application.Commands;

public sealed record SellSharesCommand(
    Guid UserId,
    string MarketPubKey,
    ulong SharesIn,
    //ulong MinCollateralOut,
    byte OutcomeIndex,
    string IdempotencyKey
);

public sealed record SellSharesResult(
    Guid MarketId,
    string MarketPubKey,
    byte OutcomeIndex,
    string TxSignature
);