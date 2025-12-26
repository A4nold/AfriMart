// using System.Net.Http.Headers;
// using Microsoft.AspNetCore.Http;
// using System.Net.Http.Json;
// using MarketService.Domain.Entities;
// using MarketService.Domain.Interfaces;
// using MarketService.Domain.Models;
// using MarketService.Infrastructure.Data;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
// using MarketService.Domain.Commands;
//
// namespace MarketService.Infrastructure.Services;
//
// public class PositionService : IPositionService
// {
//     private readonly MarketDbContext _db;
//     private readonly IHttpClientFactory _httpClientFactory;
//     private readonly IHttpContextAccessor _httpContextAccessor;
//     private readonly ILogger<PositionService> _logger;
//
//     public PositionService(MarketDbContext db, 
//         IHttpContextAccessor httpContextAccessor, 
//         IHttpClientFactory httpClientFactory,
//         ILogger<PositionService> logger)
//     {
//         _db = db;
//         _httpContextAccessor = httpContextAccessor;
//         _httpClientFactory = httpClientFactory;
//         _logger = logger;
//     }
//
//     public async Task<IReadOnlyList<PositionDto>> GetPositionsForUserAsync(Guid userId, CancellationToken ct = default)
//     {
//         var positions = await _db.MarketPositions
//             .Include(p => p.Market)
//                 .ThenInclude(m => m.Outcomes)
//             .Where(p => p.UserId == userId)
//             .OrderByDescending(p => p.PlacedAt)
//             .ToListAsync(ct);
//
//         return positions.Select(p =>
//         {
//             var label = p.Market.Outcomes
//                 .FirstOrDefault(o => o.OutcomeIndex == p.OutcomeIndex)?.Label ?? "";
//
//             return new PositionDto
//             {
//                 Id = p.Id,
//                 MarketId = p.MarketId,
//                 MarketPubkey = p.Market.MarketPubKey,
//                 Question = p.Market.Question,
//                 OutcomeIndex = p.OutcomeIndex,
//                 OutcomeLabel = label,
//                 StakeAmount = p.StakeAmount,
//                 TxSignature = p.TxSignature,
//                 PlacedAt = p.PlacedAt,
//                 Claimed = p.Claimed,
//                 ClaimedAt = p.ClaimedAt
//             };
//         }).ToList();
//     }
//
//     public async Task<PositionDto> PlaceBetAsync(
//         Guid marketId, Guid userId, 
//         PlaceBetCommand command, 
//         CancellationToken ct = default)
//     {
//         //Load market
//         var market = await _db.Markets
//             .Include(m => m.Outcomes)
//             .SingleOrDefaultAsync(m => m.Id == marketId, ct);
//
//         if (market == null)
//             throw new InvalidOperationException("Market not found");
//
//         if (market.Status != Domain.Entities.MarketStatus.Open)
//             throw new InvalidOperationException("Market is not Open for Prediction");
//
//         if (command.OutcomeIndex < 0 || command.OutcomeIndex >= market.Outcomes.Count)
//             throw new InvalidOperationException("Invalid outcome Index");
//
//         //Call blockchain service to place bets on chain.
//         var client = _httpClientFactory.CreateClient("BlockchainService");
//
//         var authHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
//         if (!string.IsNullOrWhiteSpace(authHeader))
//         {
//             client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(authHeader);
//         }
//
//         var onChainRequest = new
//         {
//             outcomeIndex = command.OutcomeIndex,
//             stakeAmount = command.StakeAmount,
//             bettorTokenAccount = command.UserCollateralAta,
//             vaultTokenAccount = command.VaultTokenAta
//         };
//
//         var response = await client.PostAsJsonAsync($"api/markets/{market.MarketPubKey}/bet",
//             onChainRequest, ct);
//
//         if (!response.IsSuccessStatusCode)
//         {
//             var body = await response.Content.ReadAsStringAsync(ct);
//             _logger.LogError($"Failed to place on-chain prediction. Status:{response.StatusCode}, Body:{body}");
//             throw new InvalidOperationException("Failed to place on-chain prediction");
//         }
//
//         var chainResult = await response.Content.ReadFromJsonAsync<PlaceBetOnChainResponse>(ct)
//                             ?? throw new InvalidOperationException("Invalid response from blockchain service");
//
//         //save off-chain MarketPosition
//         var position = new MarketPosition
//         {
//             Id = Guid.NewGuid(),
//             UserId = userId,
//             MarketId = marketId,
//             OutcomeIndex = command.OutcomeIndex,
//             StakeAmount = command.StakeAmount,
//             TxSignature = chainResult.TransactionSignature,
//             PlacedAt = DateTime.UtcNow,
//             Claimed = false
//         };
//
//         _db.MarketPositions.Add(position);
//         await _db.SaveChangesAsync(ct);
//
//         var outcomeLabel = market.Outcomes
//             .FirstOrDefault(o => o.OutcomeIndex == command.OutcomeIndex)?.Label ?? "";
//
//         return new PositionDto
//         {
//             Id = position.Id,
//             MarketId = position.MarketId,
//             MarketPubkey = market.MarketPubKey,
//             Question = market.Question,
//             OutcomeLabel = outcomeLabel,
//             StakeAmount = position.StakeAmount,
//             TxSignature = position.TxSignature,
//             PlacedAt = position.PlacedAt,
//             Claimed = position.Claimed,
//             ClaimedAt = position.ClaimedAt
//         };
//     }
//
//     private sealed class PlaceBetOnChainResponse
//     {
//         public string TransactionSignature { get; set; } = default!;
//     }
// }
