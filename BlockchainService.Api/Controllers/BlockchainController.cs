using Microsoft.AspNetCore.Authorization;
using BlockchainService.Api.Models.Requests;
using BlockchainService.Api.Models.Responses;
using BlockchainService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BlockchainService.Api.Controllers;

[ApiController]
[Route("api/markets")]
public class BlockchainController : ControllerBase
{
    private readonly PredictionProgramClient _client;

    public BlockchainController(PredictionProgramClient client)
    {
        _client = client;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("create")]
    public async Task<ActionResult<CreateMarketResponse>> Create([FromBody] CreateMarketRequest request)
    {
        var result = await _client.CreateMarketAsync(
            request.Question,
            request.Outcomes,
            request.EndTime,
            request.CollateralMint,
            request.VaultTokenAccount
        );

        var response = new CreateMarketResponse(
            result.MarketPubkey,
            result.TransactionSignature
        );

        return Ok(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{marketPubkey}/resolve")]
    public async Task<ActionResult<ResolveMarketResponse>> Resolve(string marketPubkey, [FromBody] ResolveMarketRequest request)
    {
        var result = await _client.ResolveMarketAsync(marketPubkey, request.WinningOutcomeIndex);

        var response = new ResolveMarketResponse(
            result.MarketPubkey,
            result.TransactionSignature
        );

        return Ok(response);
    }

    [HttpPost("{marketPubkey}/bet")]
    public async Task<ActionResult<PlaceBetResponse>> PlaceBet(
        string marketPubkey,
        [FromBody] PlaceBetRequest request)
    {
        var result = await _client.PlaceBetAsync(
            marketPubkey,
            request.BettorTokenAccount,
            request.VaultTokenAccount,
            request.StakeAmount,
            request.OutcomeIndex);

        var response = new PlaceBetResponse(
            result.MarketPubkey,
            result.BettorTokenAccount,
            result.StakeAmount,
            result.OutcomeIndex,
            result.TransactionSignature);

        return Ok(response);
    }

    /// <summary>
    /// Claim winnings for a specific resolved market.
    /// </summary>
    [HttpPost("{marketPubkey}/claim")]
    [Authorize] // must be logged in
    public async Task<IActionResult> ClaimWinnings(
        string marketPubkey,
        [FromBody] ClaimWinningsRequest request,
        CancellationToken ct)
    {
        // For MVP, on-chain `user` == backend authority.
        // We only need the token accounts from the caller.
        var txSig = await _client.ClaimWinningsAsync(
            marketPubkey: marketPubkey,
            userCollateralAta: request.UserCollateralAta,
            vaultTokenAccount: request.VaultTokenAccount,
            ct: ct
        );

        return Ok(new
        {
            MarketPubkey = marketPubkey,
            TransactionSignature = txSig
        });
    }
}
