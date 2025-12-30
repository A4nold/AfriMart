namespace BlockchainService.Api.Dto;

public sealed record GetPositionOnChain(
    string MarketPubkey,
    string OwnerPubkey,
    string PositionPubkey,
    ulong YesShares,
    ulong NoShares,
    bool Claimed,
    ulong? LastSyncedSlot
);

public sealed record GetMarketResponse(
    string MarketPubkey,
    ulong MarketId,
    string AuthorityPubkey,
    string Question,
    string CollateralMint,
    string VaultPubkey,
    long EndTime,
    byte Status,
    sbyte WinningOutcome,
    ulong YesPool,
    ulong NoPool,
    ulong TotalYesShares,
    ulong TotalNoShares,
    ulong ResolvedVaultBalance,
    ulong ResolvedTotalWinningShares,
    ulong? LastSyncedSlot
);
