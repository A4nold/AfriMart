using PortfolioService.Domain.Interface;
using PortfolioService.Domain.Models;

namespace PortfolioService.Application.Interface;

public interface IPortfolioService
{
    public Task<List<UserMarketPositionDto>> GetAllAsync(Guid userId, CancellationToken ct);
    public Task<List<UserMarketPositionDto>> GetOpenAsync(Guid userId, CancellationToken ct);
    public Task<List<UserMarketPositionDto>> GetResolvedAsync(Guid userId, CancellationToken ct);
    public Task<UserMarketPositionDto?> GetByMarketIdAsync(Guid userId, Guid id, CancellationToken ct);
}