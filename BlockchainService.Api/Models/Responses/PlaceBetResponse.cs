namespace BlockchainService.Api.Models.Responses;

public record PlaceBetResponse(
    string MarketPubkey,
    string BettorTokenAccount,
    ulong StakeAmount,
    byte OutcomeIndex,
    string TransactionSignature
);

