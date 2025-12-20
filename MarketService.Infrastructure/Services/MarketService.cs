using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Json;
using MarketService.Domain.Entities;
using MarketService.Domain.Interfaces;
using MarketService.Domain.Models;
using MarketService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MarketService.Domain.Commands;

namespace MarketService.Infrastructure.Services;

public class MarketService : IMarketService
{
    private readonly MarketDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _config;
    private readonly ILogger<MarketService> _logger;

    public MarketService(
        MarketDbContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<MarketService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _config = config;
        _logger = logger;
    }

    //Helper
    private HttpClient CreateBlockchainClient()
    {
        var client = _httpClientFactory.CreateClient("BlockchainService");

        var authHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrWhiteSpace(authHeader))
        {
            client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(authHeader);
        }

        return client;
    }

    public async Task<MarketDto> CreateMarketAsync(CreateMarketCommand command, Guid creatorUserId, CancellationToken ct = default)
    {
        // 1. Call BlockchainService to create on-chain market
        var client = CreateBlockchainClient();

        var blockchainRequest = new
        {
            question = command.Question,
            endTime = command.EndTime,
            outcomes = command.Outcomes,
            collateralMint = command.CollateralMint,
            vaultTokenAccount = command.VaultTokenAccount,
        };

        var response = await client.PostAsJsonAsync("api/markets/create", blockchainRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Failed to create on-chain market: {Status} {Body}", response.StatusCode, body);
            throw new InvalidOperationException("Failed to create on-chain market");
        }

        var chainResult = await response.Content.ReadFromJsonAsync<CreateMarketOnChainResponse>(cancellationToken: ct)
                          ?? throw new InvalidOperationException("Invalid response from BlockchainService");

        // 2. Save off-chain market + outcomes
        var market = new Market
        {
            Id = Guid.NewGuid(),
            MarketPubKey = chainResult.MarketPubkey,
            Question = command.Question,
            EndTime = command.EndTime,
            Status = MarketStatus.Open,
            CreatorUserId = creatorUserId,
            CreatedAt = DateTime.UtcNow
        };

        market.Outcomes = command.Outcomes
            .Select((label, index) => new MarketOutcome
            {
                MarketId = market.Id,
                OutcomeIndex = index,
                Label = label
            })
            .ToList();

        _db.Markets.Add(market);
        await _db.SaveChangesAsync(ct);

        return new MarketDto
        {
            Id = market.Id,
            MarketPubkey = market.MarketPubKey,
            Question = market.Question,
            EndTime = market.EndTime,
            Status = market.Status,
            Outcomes = market.Outcomes
                .OrderBy(o => o.OutcomeIndex)
                .Select(o => o.Label)
                .ToArray()
        };
    }

    public async Task<IReadOnlyList<MarketDto>> GetAllMarketsAsync(CancellationToken ct = default)
    {
        var markets = await _db.Markets
            .Include(m => m.Outcomes)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(ct);

        return markets.Select(m => new MarketDto
        {
            Id = m.Id,
            MarketPubkey = m.MarketPubKey,
            Question = m.Question,
            EndTime = m.EndTime,
            Status = m.Status,
            Outcomes = m.Outcomes
                .OrderBy(o => o.OutcomeIndex)
                .Select(o => o.Label)
                .ToArray()
        }).ToList();
    }

    public async Task<MarketDto?> GetMarketByIdAsync(Guid id, CancellationToken ct = default)
    {
        var m = await _db.Markets
            .Include(x => x.Outcomes)
            .SingleOrDefaultAsync(x => x.Id == id, ct);

        if (m == null) return null;

        return new MarketDto
        {
            Id = m.Id,
            MarketPubkey = m.MarketPubKey,
            Question = m.Question,
            EndTime = m.EndTime,
            Status = m.Status,
            Outcomes = m.Outcomes
                .OrderBy(o => o.OutcomeIndex)
                .Select(o => o.Label)
                .ToArray()
        };
    }

    public async Task<MarketResolutionDto> ResolveMarketAsync(ResolveMarketCommand command, CancellationToken ct = default)
    {
        var market = await _db.Markets
            .FirstOrDefaultAsync(m => m.Id == command.MarketId, ct);

        if (market is null)
            throw new InvalidOperationException("Market not found");

        // optional: enforce only creator can resolve (Auth layer already uses Admin role)
        if (market.CreatorUserId != command.ResolverUserId)
        {
            _logger.LogWarning("User {UserId} tried to resolve market {MarketId} they do not own",
                command.ResolverUserId, command.MarketId);
            // up to you: throw or just log
        }

        var client = CreateBlockchainClient();

        var body = new
        {
            winningOutcomeIndex = command.WinningOutcomeIndex
        };

        var response = await client.PostAsJsonAsync(
            $"api/markets/{market.MarketPubKey}/resolve",
            body,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Failed to resolve on-chain market. Status: {Status}, Body: {Body}",
                response.StatusCode, errorBody);

            throw new InvalidOperationException("Failed to resolve on-chain market");
        }

        // update DB copy
        market.Status = MarketStatus.Resolved;          // enum in your Domain
        market.WinningOutcomeIndex = command.WinningOutcomeIndex;
        market.ResolvedAt = DateTime.UtcNow;

        //Add entry in market resolution
        var resolution = new MarketResolution
        {
            Id = Guid.NewGuid(),
            MarketId = market.Id,
            Market = market,
            WinningOutcomeIndex = command.WinningOutcomeIndex,
            Source = ResolutionSource.ManualAdmin,
            EvidenceUrl = command.EvidenceUrl,
            Notes = command.Notes,
            ResolvedAt = market.ResolvedAt!.Value
        };

        _db.MarketResolutions.Add(resolution);
        await _db.SaveChangesAsync(ct);

        return new MarketResolutionDto
        {
            Id = Guid.NewGuid(),
            MarketPubkey = market.MarketPubKey,
            WinningOutcomeIndex = command.WinningOutcomeIndex
        };
    }

    public async Task ClaimWinningsAsync(ClaimWinningsCommand command, CancellationToken ct = default)
    {
        var market = await _db.Markets
            .FirstOrDefaultAsync(m => m.Id == command.MarketId, ct);

        if (market is null)
            throw new InvalidOperationException("Market not found");

        var client = CreateBlockchainClient();

        var body = new
        {
            userCollateralAta = command.BettorTokenAccount,
            vaultTokenAccount = command.VaultTokenAccount,
        };

        var response = await client.PostAsJsonAsync(
            $"api/markets/{market.MarketPubKey}/claim",
            body,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Failed to claim winnings on-chain. Status: {Status}, Body: {Body}",
                response.StatusCode, errorBody);

            throw new InvalidOperationException("Failed to claim winnings");
        }

        var chainResult = await response.Content.ReadFromJsonAsync<ClaimWinningsOnChainResponse>(ct)
                            ?? throw new InvalidOperationException("Invalid response from blockchain service");

        // mark DB position as claimed.
        var position = await _db.MarketPositions
            .FirstOrDefaultAsync(p => p.MarketId == command.MarketId && p.UserId == command.UserId, ct);

        if (position is not null)
        {
            position.Claimed = true;
            position.ClaimedAt = DateTime.UtcNow;
            position.TxSignature = chainResult.TransactionSignature;
            await _db.SaveChangesAsync(ct);
        }
    }



    private sealed class CreateMarketOnChainResponse
    {
        public string MarketPubkey { get; set; } = default!;
        public string TransactionSignature { get; set; } = default!;
    }

    private sealed class ClaimWinningsOnChainResponse
    {
        public string TransactionSignature { get; set; } = default!;

    }
}
