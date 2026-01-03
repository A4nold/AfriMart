using PortfolioService.Application.Interface;
using PortfolioService.Domain.Interface;
using PortfolioService.Domain.Models;

namespace PortfolioService.Application.Services;

public class PortfolioService : IPortfolioService
{
    private readonly IPortfolioReadRepository _repo;

    public PortfolioService(IPortfolioReadRepository repo)
    {
        _repo = repo;
    }
    
    public Task<List<UserMarketPositionDto>> GetAllAsync(Guid userId, CancellationToken ct)
        => _repo.GetAllAsync(userId, ct);

    public Task<List<UserMarketPositionDto>> GetOpenAsync(Guid userId, CancellationToken ct)
        => _repo.GetOpenAsync(userId, ct);

    public Task<List<UserMarketPositionDto>> GetResolvedAsync(Guid userId, CancellationToken ct)
        => _repo.GetResolvedAsync(userId, ct);

    public Task<UserMarketPositionDto?> GetByMarketIdAsync(Guid userId, Guid marketId, CancellationToken ct)
        => _repo.GetByMarketIdAsync(userId, marketId, ct);
}