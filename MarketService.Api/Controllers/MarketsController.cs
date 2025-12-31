using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MarketService.Application.Commands;
using MarketService.Application.Exception;
using MarketService.Application.Interfaces;
using MarketService.Domain.Models;
using CreateMarketCommand = MarketService.Application.Commands.CreateMarketCommand;
using ValidationException = System.ComponentModel.DataAnnotations.ValidationException;

namespace MarketService.Api.Controllers;

[ApiController]
[Route("api/markets")]
public class MarketsController : ControllerBase
{
    private readonly IMarketApplication _app;
    private readonly IWebHostEnvironment _env;
    private readonly IBlockchainGateway _chain;
    private readonly ICpmmQuoteEngine _quote;
    
    private const ulong FeeBps = 50; // must match on-chain FEE_BPS
    
    // -------------------------------
    // HELPERS
    // -------------------------------
    private static decimal ImpliedPrice(ulong yesPool, ulong noPool, OutcomeSide side)
    {
        decimal y = yesPool;
        decimal n = noPool;
        var denom = y + n;
        if (denom == 0) return 0m;
        
        return side == OutcomeSide.Yes ? (n / denom) : (y / denom);
    }

    private static decimal PriceImpactPct(decimal before, decimal after)
    {
        if (before == 0) return 0m;
        return (after - before) / before * 100m;
    }

    public MarketsController(IMarketApplication app, IWebHostEnvironment env,
        IBlockchainGateway chain, ICpmmQuoteEngine quote)
    {
        _app = app;
        _env = env;
        _chain = chain;
        _quote = quote;
    }

    private string GetIdempotencyKeyOrThrow()
    {
        if (Request.Headers.TryGetValue("Idempotency-Key", out var v) && !string.IsNullOrWhiteSpace(v))
            return v.ToString();

        if (_env.IsDevelopment())
        {
            var generated = Guid.NewGuid().ToString();
            Console.WriteLine($"[DEV] Generated Idempotency-Key: {generated}");
            return generated;
        }
        
        throw new ValidationException("Missing Idempotency-Key header.");
    }

    public static ulong GetMarketU64Seeds()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);

        if (!BitConverter.IsLittleEndian)
            bytes.Reverse();

        return BitConverter.ToUInt64(bytes);
    }

    // ---- Create ----
    [Authorize(Roles = "Admin")]
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateMarketApiRequest req, CancellationToken ct)
    {
        try
        {
            var cmd = new CreateMarketCommand(
                CreatorUserId: req.CreatorUserId,
                MarketSeedId: GetMarketU64Seeds(),
                Question: req.Question,
                EndTimeUtc: req.EndTime.ToUniversalTime(),
                InitialLiquidity: req.InitialLiquidity,
                IdempotencyKey: GetIdempotencyKeyOrThrow()
            );

            var result = await _app.CreateMarketAsync(cmd, ct);
            return Ok(new { result.MarketId, result.MarketPubKey,MarketSeedId = cmd.MarketSeedId, result.TxSignature });
        }
        catch (Exception ex) { return MapException(ex); }
    }

    // ---- Resolve ----
    [Authorize(Roles = "Admin")]
    [HttpPost("{marketPubkey}/resolve")]
    public async Task<IActionResult> Resolve([FromRoute] string marketPubkey, [FromBody] ResolveMarketApiRequest req, CancellationToken ct)
    {
        try
        {
            var cmd = new ResolveMarketCommand(
                ResolverUserId: req.ResolverUserId,
                MarketPubKey: marketPubkey,
                WinningOutcomeIndex: req.WinningOutcomeIndex,
                IdempotencyKey: GetIdempotencyKeyOrThrow()
            );

            var result = await _app.ResolveMarketAsync(cmd, ct);
            return Ok(new { result.MarketId, result.MarketPubKey, result.WinningOutcomeIndex, result.TxSignature });
        }
        catch (Exception ex) { return MapException(ex); }
    }

    // ---- Buy ----
    [Authorize]
    [HttpPost("{marketPubkey}/buy")]
    public async Task<IActionResult> Buy([FromRoute] string marketPubkey, [FromBody] BuySharesCommand req, CancellationToken ct)
    {
        try
        {
            var cmd = new BuySharesCommand(
                UserId: req.UserId,
                MarketPubKey: marketPubkey,
                MaxCollateralIn: req.MaxCollateralIn,
                //MinSharesOut: req.MinSharesOut,
                OutcomeIndex: req.OutcomeIndex,
                IdempotencyKey: GetIdempotencyKeyOrThrow()
            );

            var result = await _app.BuySharesAsync(cmd, ct);
            return Ok(result);
        }
        catch (Exception ex) { return MapException(ex); }
    }

    // ---- Sell ----
    [Authorize]
    [HttpPost("{marketPubkey}/sell")]
    public async Task<IActionResult> Sell([FromRoute] string marketPubkey, [FromBody] SellSharesCommand req, CancellationToken ct)
    {
        try
        {
            var cmd = new SellSharesCommand(
                UserId: req.UserId,
                MarketPubKey: marketPubkey,
                SharesIn: req.SharesIn,
                //MinCollateralOut: req.MinCollateralOut,
                OutcomeIndex: req.OutcomeIndex,
                IdempotencyKey: GetIdempotencyKeyOrThrow()
            );

            var result = await _app.SellSharesAsync(cmd, ct);
            return Ok(result);
        }
        catch (Exception ex) { return MapException(ex); }
    }

    // ---- Claim ----
    [Authorize]
    [HttpPost("{marketPubkey}/claim")]
    public async Task<IActionResult> Claim([FromRoute] string marketPubkey, [FromBody] ClaimWinningsApiRequest req, CancellationToken ct)
    {
        try
        {
            var cmd = new ClaimWinningsCommand(
                UserId: req.UserId,
                MarketPubKey: marketPubkey,
                IdempotencyKey: GetIdempotencyKeyOrThrow()
            );

            var result = await _app.ClaimWinningsAsync(cmd, ct);
            return Ok(result);
        }
        catch (Exception ex) { return MapException(ex); }
    }
    
    // ---- Quote Buy ----
    [Authorize]
    [HttpPost("{marketPubkey}/quote/buy")]
    public async Task<IActionResult> QuoteBuy(
        [FromRoute] string marketPubkey,
        [FromBody] BuyQuoteRequest req,
        CancellationToken ct)
    {
        try
        {
            var mv2 = await _chain.GetMarketAsync(marketPubkey, ct);
            if (mv2.State.Status != 0) throw new ConflictException("Market is not open");
            //quote maths
            var quote = _quote.QuoteBuy(mv2, req.OutcomeIndex, req.CollateralIn, FeeBps);

            var priceBefore = ImpliedPrice(mv2.State.YesPool, mv2.State.NoPool, req.OutcomeIndex);
            var priceAfter = ImpliedPrice(quote.NewYesPool, quote.NewNoPool, req.OutcomeIndex);
            var impactPct = PriceImpactPct(priceBefore, priceAfter);

            return Ok(new BuyQuoteResponse(
                MarketPubKey: marketPubkey,
                OutcomeIndex: req.OutcomeIndex,
                GrossCollateralIn: req.CollateralIn,
                FeePaid: quote.FeePaid,
                NetCollateralIn:quote.NetCollateralIn,
                EstimatedSharesOut: quote.SharesOut,
                MinSharesOut: quote.SharesOut,
                NewYesPool: quote.NewYesPool,
                NewNoPool: quote.NewNoPool,
                ImpliedPriceBefore:priceBefore,
                ImpliedPriceAfter:priceAfter,
                PriceImpactPct:impactPct
                ));
        }
        catch (Exception ex)
        {
            return MapExceptions(ex);
        }
    } 
    
    // ---- Quote Sell ----
    [Authorize]
    [HttpPost("{marketPubkey}/quote/sell")]
    public async Task<ActionResult<SellQuote>> QuoteSell(
        [FromRoute] string marketPubkey,
        [FromBody] SellQuoteRequest req,
        CancellationToken ct)
    {
        try
        {
            var mv2 = await _chain.GetMarketAsync(marketPubkey, ct);
            if (mv2.State.Status != 0) throw new ConflictException("Market is not open");
            var quote = _quote.QuoteSell(mv2, req.OutcomeIndex, req.SharesIn, FeeBps);
            
            var priceBefore = ImpliedPrice(mv2.State.YesPool, mv2.State.NoPool, req.OutcomeIndex);
            var priceAfter = ImpliedPrice(quote.NewYesPool, quote.NewNoPool, req.OutcomeIndex);
            var impactPct = PriceImpactPct(priceBefore, priceAfter);

            return Ok(new SellQuoteResponse(
                MarketPubKey: marketPubkey,
                OutcomeIndex: req.OutcomeIndex,
                SharesIn:quote.SharesIn,
                GrossCollateralOut:quote.GrossCollateralOut,
                FeePaid: quote.FeePaid,
                NetCollateralOut:quote.NetCollateralOut,
                MinCollateralOut:quote.NetCollateralOut,
                NewYesPool: quote.NewYesPool,
                NewNoPool: quote.NewNoPool,
                ImpliedPriceBefore:priceBefore,
                ImpliedPriceAfter:priceAfter,
                PriceImpactPct:impactPct));
        }
        catch (Exception ex)
        {
            return MapExceptions(ex);
        }
    }

    // -------------------------------
    // Exception -> HTTP mapping
    // -------------------------------
    private IActionResult MapException(Exception ex)
    {
        return ex switch
        {
            ValidationException ve => BadRequest(new ProblemDetails { Title = "Validation error", Detail = ve.Message, Status = 400 }),
            NotFoundException nfe => NotFound(new ProblemDetails { Title = "Not found", Detail = nfe.Message, Status = 404 }),
            ConflictException ce => Conflict(new ProblemDetails { Title = "Conflict", Detail = ce.Message, Status = 409 }),

            AnchorProgramException ape => BadRequest(new ProblemDetails
            {
                Title = "On-chain program error",
                Detail = ape.Message,
                Status = 400,
                Extensions =
                {
                    ["anchorCode"] = ape.AnchorCode,
                    ["anchorNumber"] = ape.AnchorNumber
                }
            }),

            ExternalDependencyException ede => StatusCode(503, new ProblemDetails { Title = "Blockchain dependency failed", Detail = ede.Message, Status = 503 }),

            _ => StatusCode(500, new ProblemDetails { Title = "Server error", Detail = ex.Message, Status = 500 })
        };
    }
    
    private ActionResult MapExceptions(Exception ex)
    {
        return ex switch
        {
            ValidationException ve => BadRequest(new ProblemDetails { Title = "Validation error", Detail = ve.Message, Status = 400 }),
            NotFoundException nfe => NotFound(new ProblemDetails { Title = "Not found", Detail = nfe.Message, Status = 404 }),
            ConflictException ce => Conflict(new ProblemDetails { Title = "Conflict", Detail = ce.Message, Status = 409 }),

            AnchorProgramException ape => BadRequest(new ProblemDetails
            {
                Title = "On-chain program error",
                Detail = ape.Message,
                Status = 400,
                Extensions =
                {
                    ["anchorCode"] = ape.AnchorCode,
                    ["anchorNumber"] = ape.AnchorNumber
                }
            }),

            ExternalDependencyException ede => StatusCode(503, new ProblemDetails { Title = "Blockchain dependency failed", Detail = ede.Message, Status = 503 }),

            _ => StatusCode(500, new ProblemDetails { Title = "Server error", Detail = ex.Message, Status = 500 })
        };
    }
}

// -------------------------------
// API request DTOs
// -------------------------------
public sealed record CreateMarketApiRequest(
    Guid CreatorUserId,
    ulong MarketId,
    string Question,
    DateTime EndTime,
    ulong InitialLiquidity
);

public sealed record ResolveMarketApiRequest(Guid ResolverUserId, byte WinningOutcomeIndex);

//public sealed record BuySharesApiRequest(Guid UserId, ulong MaxCollateralIn, ulong MinSharesOut, byte OutcomeIndex);

//public sealed record SellSharesApiRequest(Guid UserId, ulong SharesIn, ulong MinCollateralOut, byte OutcomeIndex);

public sealed record ClaimWinningsApiRequest(Guid UserId);