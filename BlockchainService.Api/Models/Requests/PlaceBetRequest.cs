namespace BlockchainService.Api.Models.Requests;

public class PlaceBetRequest 
{
    public string BettorTokenAccount { get; set; } = default!;
    public string VaultTokenAccount { get; set; } = default!;
    public ulong StakeAmount { get; set; }
    public byte OutcomeIndex { get; set; }
}

