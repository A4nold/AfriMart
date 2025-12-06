namespace BlockchainService.Api.Models.Requests
{
    public class ClaimWinningsRequest
    {
        /// <summary>
        /// User's USDC token account to receive funds
        /// (must be the same one that placed the bet).
        /// </summary>
        public string UserCollateralAta { get; set; } = default!;

        /// <summary>
        /// Market vault token account that holds collateral.
        /// Same vault you passed when creating the market.
        /// </summary>
        public string VaultTokenAccount { get; set; } = default!;
    }
}

//public record ClaimWinningsRequest(
//    string UserTokenAccount,
//    string VaultTokenAccount
//);

