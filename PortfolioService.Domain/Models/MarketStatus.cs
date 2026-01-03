namespace PortfolioService.Domain.Models;

public enum MarketStatus:byte
{
    Open = 0,
    Resolved = 1,
    Cancelled = 2
}

public enum ExposureSide : byte
{
    None = 0,
    Yes = 1,
    No = 2,
    Mixed = 3
}