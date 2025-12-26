using MarketService.Domain.Commands;
using MarketService.Domain.Entities;
using MarketService.Domain.Models;

namespace MarketService.Domain.Interfaces;

public interface IMarketService
{
    Task<MarketDto> CreateMarketAsync(CreateMarketCommand command, Guid creatorUserId, CancellationToken ct = default);
    Task<IReadOnlyList<MarketDto>> GetAllMarketsAsync(CancellationToken ct = default);
    Task<MarketDto?> GetMarketByIdAsync(Guid id, CancellationToken ct = default);
    // Task<MarketResolutionDto> ResolveMarketAsync(ResolveMarketCommand command, CancellationToken ct = default);
    // Task ClaimWinningsAsync(ClaimWinningsCommand command, CancellationToken ct = default);
}
