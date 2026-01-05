namespace AuthService.Domain.Entities;

public sealed class WalletLoginChallenge
{
    public Guid Id { get; set; }

    public string WalletPubkey { get; set; } = default!;
    public string Nonce { get; set; } = default!;
    public string Message { get; set; } = default!;
    
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
}