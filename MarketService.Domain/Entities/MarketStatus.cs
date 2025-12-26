using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketService.Domain.Entities
{
    public enum MarketStatus:byte
    {
        Open = 0,
        Resolved = 1,
        Cancelled = 2
    }

    public enum MarketActionType:byte
    {
        Create = 0,
        Buy = 1,
        Sell = 2,
        Resolve = 3,
        Claim = 4
    }

    public enum ActionState : byte
    {
        Pending = 0,
        Submitted = 1,
        Confirmed = 2,
        Failed = 3
    }
    
}
