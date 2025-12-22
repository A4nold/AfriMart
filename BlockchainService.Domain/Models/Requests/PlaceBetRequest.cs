namespace BlockchainService.Api.Models.Requests;

public class BuySharesRequest 
{
    public string UserCollateralAta { get; set; } = default!;
    public ulong MaxCollateralIn { get; set; }
    public ulong MinSharesOut { get; set; } = 0; // allow 0 for MVP 
    public byte OutcomeIndex { get; set; } // 0 = YES, 1 = NO
}

