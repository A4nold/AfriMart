namespace MarketService.Domain.Models;

public enum OutcomeSide : byte {Yes =0, No =1}

public sealed record BuyQuote(
    OutcomeSide Side,
    ulong GrossCollateralIn,
    ulong FeePaid,
    ulong NetCollateralIn,
    ulong SharesOut,
    ulong NewYesPool,
    ulong NewNoPool);
    
public sealed record SellQuote(
    OutcomeSide Side,
    ulong SharesIn,
    ulong GrossCollateralOut,
    ulong FeePaid,
    ulong NetCollateralOut,
    ulong NewYesPool,
    ulong NewNoPool);    
    