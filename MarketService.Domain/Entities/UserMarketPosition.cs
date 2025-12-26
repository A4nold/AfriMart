namespace MarketService.Domain.Entities;

public class UserMarketPosition
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }          // Auth user id
    public Guid MarketId { get; set; }
    public Market Market { get; set; } = default!;

    // On-chain pointer (optional but useful)
    public string PositionPubKey { get; set; } = default!; // position_v2 PDA

    // Cached snapshot (chain is truth)
    public ulong YesShares { get; set; }
    public ulong NoShares { get; set; }
    public bool Claimed { get; set; }

    public ulong? LastSyncedSlot { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
}
