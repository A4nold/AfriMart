using Solnet.Wallet;

namespace MarketService.Application.Dtos;

public sealed record MarketPdas(
    string MarketPubKey,
    string VaultPubKey,
    string VaultAuthorityPubKey
);

public sealed record CreateMarketIdemPayload(ulong MarketSeedId);

public sealed record GetPositionResponse(
    string MarketPubkey,
    string OwnerPubkey,
    string PositionPubkey,
    ulong YesShares,
    ulong NoShares,
    bool Claimed,
    ulong LastSyncedSlot
);

public sealed record MarketV2State(
    ulong Slot,
    ulong MarketId,
    string Authority,
    string Question,
    string CollateralMint,
    string Vault,
    long EndTime,
    byte Status,
    sbyte WinningOutcome,
    ulong YesPool,
    ulong NoPool,
    ulong TotalYesShares,
    ulong TotalNoShares,
    ulong ResolvedVaultBalance,
    ulong ResolvedTotalWinningShares
);

public sealed record MarketStateResponse(ulong Slot, MarketV2State State);
