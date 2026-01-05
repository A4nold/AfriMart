namespace MarketService.Application.Responses;

public sealed record SyncPositionResult(
    Guid MarketId,
    string MarketPubkey,
    string PositionPubkey,
    ulong YesShares,
    ulong NoShares,
    bool Claimed,
    ulong? LastSyncedSlot,
    DateTime SyncedAtUtc
);