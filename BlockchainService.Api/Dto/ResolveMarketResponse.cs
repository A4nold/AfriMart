namespace BlockchainService.Api.Models.Responses;

public record ResolveMarketResponse(
    string MarketPubkey,
    string TransactionSignature
);
