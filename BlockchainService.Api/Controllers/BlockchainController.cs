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
            request.MarketId,
            request.Question,
            request.EndTime.ToUniversalTime(),
            request.InitialLiquidity,
            request.CollateralMint,
            request.AuthorityCollateralAta
        );

        var response = new CreateMarketResponse(
            result.MarketPubkey,
            result.TransactionSignature
        );

        return Ok(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{marketPubkey}/resolve")]
    public async Task<ActionResult<ResolveMarketResponse>> Resolve(string marketPubkey, [FromBody] ResolveMarketRequest request, 
        CancellationToken ct)
    {
        var result = await _client.ResolveMarketAsync(marketPubkey, request.WinningOutcomeIndex, ct);

        var response = new ResolveMarketResponse(
            result.MarketPubkey,
            result.TransactionSignature
        );

        return Ok(response);
    }

    [HttpPost("{marketPubkey}/bet")]
    public async Task<ActionResult<BuySharesResponse>> BuyShares(
        [FromRoute] string marketPubkey,
        [FromBody] BuySharesRequest request, CancellationToken ct)
    {
        var result = await _client.BuySharesAsync(
            marketPubkey,
            request.UserCollateralAta,
            request.MaxCollateralIn,
            request.MinSharesOut,
            request.OutcomeIndex,
            ct
        );


        var response = new BuySharesResponse(
            result.MarketPubkey,
            result.UserCollateralAta,
            result.MaxCollateralIn,
            result.MinSharesOut,
            result.OutcomeIndex,
            result.TransactionSignature);

        return Ok(response);
    }
    
    [HttpPost("{marketPubkey}/sell")]
    [Authorize]
    public async Task<ActionResult<SellSharesResponse>> SellShares(
        [FromRoute] string marketPubkey,
        [FromBody] SellSharesRequest request,
        CancellationToken ct)
    {
        var result = await _client.SellSharesAsync(
            marketPubkey,
            request.UserCollateralAta,
            request.SharesIn,
            request.MinCollateralOut,
            request.OutcomeIndex,
            ct
        );

        return Ok(new SellSharesResponse(
            result.MarketPubkey,
            result.UserCollateralAta,
            result.SharesIn,
            result.MinCollateralOut,
            result.OutcomeIndex,
            result.TransactionSignature
        ));
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
            ct: ct
        );

        return Ok(new
        {
            MarketPubkey = marketPubkey,
            TransactionSignature = txSig
        });
    }
}
