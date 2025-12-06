namespace BlockchainService.Api.Models.Requests;

public record CreateMarketRequest(
    string Question,
    string[] Outcomes,
    DateTime EndTime,
    string CollateralMint,
    string VaultTokenAccount
);

