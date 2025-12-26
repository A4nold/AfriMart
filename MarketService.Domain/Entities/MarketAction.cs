namespace MarketService.Domain.Entities;

public class MarketAction
{
    public Guid Id { get; set; }

    public Guid MarketId { get; set; }
    public Market Market { get; set; } = default!;

    // Who requested it (Auth user id). For admin-only actions this is admin.
    public Guid? RequestedByUserId { get; set; }

    public MarketActionType ActionType { get; set; }
    public ActionState State { get; set; }

    // Idempotency is crucial for retries and “double clicks”
    public string IdempotencyKey { get; set; } = default!;

    // What we sent to BlockchainService (exact request params)
    public string RequestJson { get; set; } = default!;
    

    // What we got back
    public string? ResponseJson { get; set; }
    public string? TxSignature { get; set; }
    public ulong? ConfirmedSlot { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }

    // Error mapping from BlockchainService helper
    public string? ErrorCode { get; set; }          // e.g. MARKET_NOT_OPEN, ALREADY_CLAIMED
    public int? AnchorErrorNumber { get; set; }     // e.g. 6001, 6007
    public string? RpcErrorText { get; set; }       // raw detail for debugging
    
    public int AttemptCount { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
