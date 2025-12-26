namespace MarketService.Application.Commands;

public sealed record CreateMarketCommand(
    Guid CreatorUserId,
    ulong MarketSeedId,
    string Question,
    DateTime EndTimeUtc,
    ulong InitialLiquidity,
    string CollateralMint,
    string IdempotencyKey
);

public sealed record CreateMarketResult(
    Guid MarketId,
    string MarketPubKey,
    string TxSignature
);