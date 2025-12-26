using MarketService.Domain.Entities;

namespace MarketService.Application.Commands;

public sealed record ResolveMarketCommand(
    Guid ResolverUserId,
    string MarketPubKey,
    byte WinningOutcomeIndex,
    string IdempotencyKey
);

public sealed record ResolveMarketResult(
    Guid MarketId,
    string MarketPubKey,
    byte WinningOutcomeIndex,
    string TxSignature
);

