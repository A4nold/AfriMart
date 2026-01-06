using AuthService.Api.Dtos;
using AuthService.Domain.Interfaces;
using AuthService.Domain.Models.Requests;
using AuthService.Domain.Models.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AuthService.Domain.Entities;
using AuthService.Domain.Models;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("admin/seed")]
    public async Task<IActionResult> SeedAdmin([FromBody] AdminSeedRequest request)
    {
        try
        {
            var user = await _authService.SeedAdminAsync(request);

            return Created("", new
            {
                user.Id,
                user.Email,
                user.Alias,
                Roles = new[] { "Admin" }
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var result = await _authService.LoginAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Login Failed", detail = ex.Message });
        }
    }

    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var result = await _authService.RefreshAsync(request.RefreshToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Failed to Refresh Token", detail = ex.Message });
        }
        
    }

    [HttpPost("wallet/challenge")]
    [ProducesResponseType(typeof(WalletLoginChallenge), StatusCodes.Status200OK)]
    public async Task<ActionResult<WalletChallengeDto>> WalletLoginChallenge(
        [FromBody] WalletChallengeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.WalletPubkey))
            return BadRequest("Wallet Pubkey is required");
        
        var dto = await _authService.CreateChallengeAsync(request.WalletPubkey, ct);
        
        return Ok(new WalletChallengeResponse(dto.ChallengeId,
            dto.WalletPubkey, dto.Nonce,dto.MessageToSign, dto.ExpiresAtUtc));
    }

    [HttpPost("wallet/verify")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResponse>> VerifyWalletChallenge(
        [FromBody] WalletVerifyRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.WalletPubkey))
            return BadRequest("Wallet Pubkey is required");
        if (string.IsNullOrWhiteSpace(request.ChallengeId.ToString()))
            return BadRequest("Challenge Id is required");
        if (string.IsNullOrWhiteSpace(request.Signature))
            return BadRequest("Signature is required");
        
        var res = await _authService.VerifyChallengeAsync(request.WalletPubkey, 
            request.Signature, request.ChallengeId.ToString(), ct);
        
        return Ok(res);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogOut([FromBody] RefreshTokenRequest request)
    {
        await _authService.LogOutAsync(request.RefreshToken);
        // Even if token was already invalid, we just return 204 - idempotent
        return NoContent();
    }

    [HttpPost("logout-all")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAll()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        await _authService.LogoutAllAsync(userId);
        return NoContent();
    }
    
    
}
