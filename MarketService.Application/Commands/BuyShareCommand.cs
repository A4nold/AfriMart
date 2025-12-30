namespace MarketService.Application.Commands;

public sealed record BuySharesCommand(
    Guid UserId,
    string MarketPubKey,
    ulong MaxCollateralIn,
    //ulong MinSharesOut,
    byte OutcomeIndex,
    string IdempotencyKey
);

public sealed record BuySharesResult(
    Guid MarketId,
    string MarketPubKey,
    byte OutcomeIndex,
    string TxSignature
);