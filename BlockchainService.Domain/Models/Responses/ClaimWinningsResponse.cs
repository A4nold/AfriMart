namespace BlockchainService.Api.Models.Responses
{
    public class ClaimWinningsResponse
    {
        public string MarketPublickey { get; set; } = default!;
        public string TransactionSignature { get; set; } = default!;
    }
}

