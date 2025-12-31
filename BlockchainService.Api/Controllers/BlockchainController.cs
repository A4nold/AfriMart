using BlockchainService.Api.Dto;
using BlockchainService.Api.Exceptions;
using Microsoft.AspNetCore.Authorization;
using BlockchainService.Api.Models.Requests;
using BlockchainService.Api.Models.Responses;
using BlockchainService.Api.Services;
using BlockchainService.Domain.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Solnet.Wallet;

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

    // ---- Anchor error mapping helpers ----
    private static ObjectResult AnchorConflict(string appCode, string message, AnchorProgramException ex)
        => new ConflictObjectResult(new
        {
            code = appCode,
            message,
            anchor = new
            {
                code = ex.AnchorCode,
                number = ex.AnchorNumber
            }
        });

    private static ObjectResult AnchorBadRequest(string appCode, string message, AnchorProgramException ex)
        => new BadRequestObjectResult(new
        {
            code = appCode,
            message,
            anchor = new
            {
                code = ex.AnchorCode,
                number = ex.AnchorNumber
            }
        });

    private static ObjectResult AnchorServerError(string message, AnchorProgramException ex)
        => new ObjectResult(new
        {
            code = "PROGRAM_ERROR",
            message,
            anchor = new
            {
                code = ex.AnchorCode,
                number = ex.AnchorNumber
            }
        })
        { StatusCode = 500 };

    private static ObjectResult MapAnchorError(AnchorProgramException ex)
    {
        // Match the error numbers you’ve observed:
        // 6001 -> InvalidMarketStatus
        // 6007 -> AlreadyClaimed
        return ex.AnchorNumber switch
        {
            6001 => AnchorConflict("MARKET_NOT_OPEN", "Market is not open (resolved/cancelled).", ex),
            6007 => AnchorConflict("ALREADY_CLAIMED", "Position already claimed.", ex),

            // Add more as you hit them in tests:
            // 6003 => BadRequest...
            // etc...

            _ => AnchorBadRequest("ANCHOR_ERROR", ex.Message, ex)
        };
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("create")]
    public async Task<ActionResult<CreateMarketResponse>> Create([FromBody] CreateMarketRequest request)
    {
        try
        {
            var result = await _client.CreateMarketAsync(
                request.MarketId,
                request.Question,
                request.EndTime.ToUniversalTime(),
                request.InitialLiquidity
            );

            return Ok(new CreateMarketResponse(
                result.MarketPubkey,
                result.TransactionSignature
            ));
        }
        catch (AnchorProgramException ex)
        {
            return MapAnchorError(ex);
        }
        catch (Exception ex)
        {
            // Keep this simple for MVP; you can add structured logging later.
            return StatusCode(500, new { code = "INTERNAL_ERROR", message = ex.Message });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{marketPubkey}/resolve")]
    public async Task<ActionResult<ResolveMarketResponse>> Resolve(
        string marketPubkey,
        [FromBody] ResolveMarketRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _client.ResolveMarketAsync(marketPubkey, request.WinningOutcomeIndex, ct);

            return Ok(new ResolveMarketResponse(
                result.MarketPubkey,
                result.TransactionSignature
            ));
        }
        catch (AnchorProgramException ex)
        {
            return MapAnchorError(ex);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { code = "INTERNAL_ERROR", message = ex.Message });
        }
    }

    [HttpPost("{marketPubkey}/buy")]
    public async Task<ActionResult<BuySharesResponse>> BuyShares(
        [FromRoute] string marketPubkey,
        [FromBody] BuySharesRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _client.BuySharesAsync(
                marketPubkey,
                request.MaxCollateralIn,
                request.MinSharesOut,
                request.OutcomeIndex,
                ct
            );

            return Ok(new BuySharesResponse(
                result.MarketPubkey,
                result.UserCollateralAta,
                result.MaxCollateralIn,
                result.MinSharesOut,
                result.OutcomeIndex,
                result.TransactionSignature
            ));
        }
        catch (AnchorProgramException ex)
        {
            return MapAnchorError(ex);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { code = "INTERNAL_ERROR", message = ex.Message });
        }
    }

    [HttpPost("{marketPubkey}/sell")]
    [Authorize]
    public async Task<ActionResult<SellSharesResponse>> SellShares(
        [FromRoute] string marketPubkey,
        [FromBody] SellSharesRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await _client.SellSharesAsync(
                marketPubkey,
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
        catch (AnchorProgramException ex)
        {
            return MapAnchorError(ex);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { code = "INTERNAL_ERROR", message = ex.Message });
        }
    }

    /// <summary>
    /// Claim winnings for a specific resolved market.
    /// </summary>
    [HttpPost("{marketPubkey}/claim")]
    [Authorize]
    public async Task<IActionResult> ClaimWinnings(
        [FromRoute] string marketPubkey,
        CancellationToken ct)
    {
        try
        {
            var txSig = await _client.ClaimWinningsAsync(marketPubkey, ct);

            return Ok(new
            {
                MarketPubkey = marketPubkey,
                TransactionSignature = txSig
            });
        }
        catch (AnchorProgramException ex)
        {
            return MapAnchorError(ex);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { code = "INTERNAL_ERROR", message = ex.Message });
        }
    }
    
    [HttpGet("{marketPubkey}/positions/{userPubkey}")]
    [Authorize]
    public async Task<ActionResult<GetPositionOnChain>> GetPosition(
        [FromRoute] string marketPubkey,
        [FromRoute] string userPubkey,
        CancellationToken ct)
    {
        try
        {
            var pos = await _client.GetPositionAsync(marketPubkey, userPubkey, ct);
            return Ok(pos);
        }
        catch (AnchorProgramException ex)
        {
            return MapAnchorError(ex);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { code = "INTERNAL_ERROR", message = ex.Message });
        }
    }

    [HttpGet("{marketPubkey}/state")]
    [Authorize]
    public async Task<ActionResult<PredictionProgramClient.MarketV2State>> GetMarket(
        [FromRoute] string marketPubkey, CancellationToken ct)
    {
        try
        {
            var pk = new PublicKey(marketPubkey);
            var (slot, marketV2State) = await _client.GetMarketAsync(pk, ct);
            return Ok(new MarketStateResponse{
                Slot = slot, State = marketV2State
            });
        }
        catch (AnchorProgramException ex)
        {
            return MapAnchorError(ex);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { code = "INTERNAL_ERROR", message = ex.Message });
        }
        
    }

}
