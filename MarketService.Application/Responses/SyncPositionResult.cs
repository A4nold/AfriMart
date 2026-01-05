namespace MarketService.Application.Responses;

public sealed record SyncPositionResult(
    string MarketPubkey,
    string OwnerPubkey,
    string PositionPubkey,
    ulong YesShares,
    ulong NoShares,
    bool Claimed,
    ulong? LastSyncedSlot,
    DateTime SyncedAtUtc
);