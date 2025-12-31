namespace MarketService.Domain.Models;

public enum OutcomeSide : byte {Yes =0, No =1}

public sealed record BuyQuote(
    ulong MarketId,
    OutcomeSide Side,
    ulong GrossCollateralIn,
    ulong FeePaid,
    ulong NetCollateralIn,
    ulong SharesOut,
    ulong NewYesPool,
    ulong NewNoPool);
    
public sealed record SellQuote(
    ulong MarketId,
    OutcomeSide Side,
    ulong SharesIn,
    ulong GrossCollateralOut,
    ulong FeePaid,
    ulong NetCollateralOut,
    ulong NewYesPool,
    ulong NewNoPool);

public sealed record SellQuoteResponse(
    string MarketPubKey,
    OutcomeSide OutcomeIndex,
    ulong SharesIn,
    ulong GrossCollateralOut,
    ulong FeePaid,
    ulong NetCollateralOut,
    ulong MinCollateralOut, // for tx
    ulong NewYesPool,
    ulong NewNoPool,

    decimal ImpliedPriceBefore,
    decimal ImpliedPriceAfter,
    decimal PriceImpactPct

    // QuoteMarketSnapshotDto MarketBefore,
    // QuoteMarketSnapshotDto MarketAfter
);

public sealed record BuyQuoteRequest(
    OutcomeSide OutcomeIndex,
    ulong CollateralIn);
    //ushort SlippageBps = 200); // 2% default

public sealed record BuyQuoteResponse(
    string MarketPubKey,
    OutcomeSide OutcomeIndex,
    ulong GrossCollateralIn,
    ulong FeePaid,
    ulong NetCollateralIn,
    ulong EstimatedSharesOut,
    ulong MinSharesOut,
    ulong NewYesPool,
    ulong NewNoPool,

    decimal ImpliedPriceBefore,
    decimal ImpliedPriceAfter,
    decimal PriceImpactPct

    // QuoteMarketSnapshotDto MarketBefore,
    // QuoteMarketSnapshotDto MarketAfter
);

public sealed record SellQuoteRequest(
    OutcomeSide OutcomeIndex,
    ulong SharesIn
    //ushort SlippageBps = 200 // 2% default
);

public sealed record QuoteMarketSnapshotDto(
    ulong YesPool,
    ulong NoPool,
    ulong TotalYesShares,
    ulong TotalNoShares,
    byte Status,
    sbyte WinningOutcome,
    long EndTime
    );
    