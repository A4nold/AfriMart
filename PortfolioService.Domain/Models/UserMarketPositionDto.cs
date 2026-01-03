namespace PortfolioService.Domain.Models;

public sealed record UserMarketPositionDto(
    Guid MarketId,
    string MarketPubkey,
    string Question,
    DateTime EndTimeUtc,
    
    ulong YesShares,
    ulong NoShares,
    bool Claimed,
    
    MarketStatus? Status,
    byte? WinningOutcomeIndex,
    
    
    //computed
    bool HasExposure,
    ExposureSide ExposureSide,
    bool IsResolved,
    bool HasWinningShares,
    bool CanClaim,
    
    DateTime? LastSyncedAtUtc,
    ulong? LastSyncedSlot
    );
    
    public sealed record PositionRow(
        Guid MarketId,
        string MarketPubkey,
        string Question,
        DateTime EndTimeUtc,
        
        ulong YesShares,
        ulong NoShares,
        bool Claimed,
        
        MarketStatus? Status,
        byte? WinningOutcomeIndex,
        
        DateTime? LastSyncedAtUtc,
        ulong? LastSyncedSlot
        );