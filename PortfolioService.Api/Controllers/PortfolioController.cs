using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioService.Domain.Interface;
using PortfolioService.Domain.Models;
using System.Security.Claims;
using PortfolioService.Application.Interface;

namespace PortfolioService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PortfolioController : ControllerBase
{
    private readonly IPortfolioService _app;

    public PortfolioController(IPortfolioService app)
    {
        _app = app;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserMarketPositionDto>>> GetAll(CancellationToken ct)
    {
        var userIdStr = GetUserId();
        var res =  await _app.GetAllAsync(userIdStr, ct);
        return Ok(res);
    }

    [HttpGet("open")]
    public async Task<ActionResult<IReadOnlyList<UserMarketPositionDto>>> GetOpenAsync(Guid userId,
        CancellationToken ct)
    {
        var userIdStr = GetUserId();
        var res = await _app.GetOpenAsync(userIdStr, ct);
        return Ok(res);
    }

    [HttpGet("resolved")]
    public async Task<ActionResult<IReadOnlyList<UserMarketPositionDto>>> GetResolvedAsync(Guid userId,
        CancellationToken ct)
    {
        var userIdStr = GetUserId();
        var res = await _app.GetResolvedAsync(userIdStr, ct);
        return Ok(res);
    }

    [HttpGet("{marketId:guid}")]
    public async Task<ActionResult<UserMarketPositionDto>> GetByMarketIdAsync(Guid marketId, CancellationToken ct)
    {
        var userIdStr = GetUserId();
        var res = await _app.GetByMarketIdAsync(userIdStr, marketId, ct);
        if (res is null) return NotFound();
        return Ok(res);
    }

    private Guid GetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(rawUserId) || !Guid.TryParse(rawUserId, out var userId))
            throw new UnauthorizedAccessException("Missing/Invalid user id claim");
        
        return userId;
    }
}

