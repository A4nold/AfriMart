namespace BlockchainService.Api.Models.Requests;

public class SellSharesRequest
{
    public ulong SharesIn { get; set; }
    public ulong MinCollateralOut { get; set; } = 0; // slippage guard
    public byte OutcomeIndex { get; set; } // 0 = YES, 1 = NO
}