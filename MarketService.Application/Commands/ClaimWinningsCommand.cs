namespace MarketService.Application.Commands;

public sealed record ClaimWinningsCommand(
    Guid UserId,
    string MarketPubKey,
    string IdempotencyKey
);

public sealed record ClaimWinningsResult(
    Guid MarketId,
    string MarketPubKey,
    string TxSignature
);
