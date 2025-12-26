namespace MarketService.Application.Responses;

public sealed record BlockchainBuyResponse
(string MarketPubKey, string TransactionSignature
    );