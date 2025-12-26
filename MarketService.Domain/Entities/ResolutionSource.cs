using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketService.Domain.Entities
{
    public enum ResolutionSource
    {
        ManualAdmin = 0,
        ExternalApi = 1,
        Oracle = 2
    }
}
