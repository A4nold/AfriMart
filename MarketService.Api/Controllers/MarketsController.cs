using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MarketService.Application.Commands;
using MarketService.Application.Exception;
using MarketService.Application.Interfaces;
using CreateMarketCommand = MarketService.Application.Commands.CreateMarketCommand;
using ValidationException = System.ComponentModel.DataAnnotations.ValidationException;

namespace MarketService.Api.Controllers;

[ApiController]
[Route("api/markets")]
public class MarketsController : ControllerBase
{
    private readonly IMarketApplication _app;
    private readonly IWebHostEnvironment _env;

    public MarketsController(IMarketApplication app, IWebHostEnvironment env)
    {
        _app = app;
        _env = env;
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
                CollateralMint: req.CollateralMint,
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
    public async Task<IActionResult> Buy([FromRoute] string marketPubkey, [FromBody] BuySharesApiRequest req, CancellationToken ct)
    {
        try
        {
            var cmd = new BuySharesCommand(
                UserId: req.UserId,
                MarketPubKey: marketPubkey,
                MaxCollateralIn: req.MaxCollateralIn,
                MinSharesOut: req.MinSharesOut,
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
    public async Task<IActionResult> Sell([FromRoute] string marketPubkey, [FromBody] SellSharesApiRequest req, CancellationToken ct)
    {
        try
        {
            var cmd = new SellSharesCommand(
                UserId: req.UserId,
                MarketPubKey: marketPubkey,
                SharesIn: req.SharesIn,
                MinCollateralOut: req.MinCollateralOut,
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
}

// -------------------------------
// API request DTOs
// -------------------------------
public sealed record CreateMarketApiRequest(
    Guid CreatorUserId,
    ulong MarketId,
    string Question,
    DateTime EndTime,
    ulong InitialLiquidity,
    string CollateralMint
);

public sealed record ResolveMarketApiRequest(Guid ResolverUserId, byte WinningOutcomeIndex);

public sealed record BuySharesApiRequest(Guid UserId, ulong MaxCollateralIn, ulong MinSharesOut, byte OutcomeIndex);

public sealed record SellSharesApiRequest(Guid UserId, ulong SharesIn, ulong MinCollateralOut, byte OutcomeIndex);

public sealed record ClaimWinningsApiRequest(Guid UserId);