using PortfolioService.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortfolioService.Domain.Interface
{
    public interface IPortfolioService
    {
        Task<PortfolioOverviewDto> GetUserPortfolioAsync(Guid userId, CancellationToken ct = default);
    }
}
