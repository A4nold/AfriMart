using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioService.Domain.Interface;
using PortfolioService.Domain.Models;
using System.Security.Claims;

namespace PortfolioService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortfolioController : ControllerBase
{
    private readonly IPortfolioService _portfolioService;

    public PortfolioController(IPortfolioService portfolioService)
    {
        _portfolioService = portfolioService;
    }

    [HttpGet("myportfolio")]
    [Authorize]
    public async Task<ActionResult<PortfolioOverviewDto>> GetMyPortfolio(CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var overview = await _portfolioService.GetUserPortfolioAsync(userId, ct);
        return Ok(overview);
    }
}

