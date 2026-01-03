using PortfolioService.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortfolioService.Domain.Interface
{
    public interface IPortfolioReadRepository
    {
        Task<List<UserMarketPositionDto>> GetAllAsync(Guid userId, CancellationToken ct);
        Task<List<UserMarketPositionDto>> GetOpenAsync(Guid userId, CancellationToken ct);
        Task<List<UserMarketPositionDto>> GetResolvedAsync(Guid userId, CancellationToken ct);
        Task<UserMarketPositionDto?> GetByMarketIdAsync(Guid userId, Guid marketId, CancellationToken ct);
    }
}
