namespace BlockchainService.Api.Models.Responses;

public record CreateMarketResponse(
    string MarketPubkey,
    string TransactionSignature
);

