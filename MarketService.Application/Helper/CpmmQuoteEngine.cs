using MarketService.Application.Dtos;
using MarketService.Application.Interfaces;
using MarketService.Domain.Models;

namespace MarketService.Application.Helper;

public class CpmmQuoteEngine : ICpmmQuoteEngine
{
    private const ulong BpsDenom = 10_000;

    public BuyQuote QuoteBuy(
        MarketStateResponse m,
        OutcomeSide side,
        ulong maxCollateralIn,
        ulong feeBps)
    {
        if (m.State.YesPool == 0 || m.State.NoPool == 0) throw new InvalidOperationException("Invalid liquidity.");
        if (maxCollateralIn == 0) throw new ArgumentException("maxCollateralIn must be > 0.");
        if (feeBps >= BpsDenom) throw new ArgumentException("feeBps must be < 10_000.");

        var (netIn, fee) = ApplyFeeIn(maxCollateralIn, feeBps);

        (ulong newYes, ulong newNo, ulong sharesOut) = side switch
        {
            OutcomeSide.Yes => CpmmBuyYes(m.State.YesPool, m.State.NoPool, netIn),
            OutcomeSide.No  => CpmmBuyNo(m.State.YesPool, m.State.NoPool, netIn),
            _ => throw new ArgumentOutOfRangeException(nameof(side))
        };

        if (sharesOut == 0) throw new InvalidOperationException("Zero shares out (trade too small).");

        return new BuyQuote(
            MarketId:m.State.MarketId,
            Side: side,
            GrossCollateralIn: maxCollateralIn,
            FeePaid: fee,
            NetCollateralIn: netIn,
            SharesOut: sharesOut,
            NewYesPool: newYes,
            NewNoPool: newNo);
    }

    public SellQuote QuoteSell(
        MarketStateResponse m,
        OutcomeSide side,
        ulong sharesIn,
        ulong feeBps)
    {
        if (m.State.YesPool == 0 || m.State.NoPool == 0) throw new InvalidOperationException("Invalid liquidity.");
        if (sharesIn == 0) throw new ArgumentException("sharesIn must be > 0.");
        if (feeBps >= BpsDenom) throw new ArgumentException("feeBps must be < 10_000.");

        // CPMM computes gross out
        (ulong newYes, ulong newNo, ulong grossOut) = side switch
        {
            OutcomeSide.Yes => CpmmSellYes(m.State.YesPool, m.State.NoPool, sharesIn),
            OutcomeSide.No  => CpmmSellNo(m.State.YesPool, m.State.NoPool, sharesIn),
            _ => throw new ArgumentOutOfRangeException(nameof(side))
        };

        if (grossOut == 0) throw new InvalidOperationException("Zero collateral out (trade too small).");

        // Fee is taken from output => user receives netOut
        var (netOut, fee) = ApplyFeeOut(grossOut, feeBps);

        // IMPORTANT: your on-chain code keeps the fee in the vault and then
        // “adds fee back” into the opposite pool after computing new pools.
        // Mirror that so your quotes match simulation.
        var feeKept = grossOut - netOut;
        if (feeKept > 0)
        {
            switch (side)
            {
                case OutcomeSide.Yes:
                    // YES sell pays out from NO reserve; keeping fee increases NO reserve
                    newNo = checked(newNo + feeKept);
                    break;

                case OutcomeSide.No:
                    // NO sell pays out from YES reserve; keeping fee increases YES reserve
                    newYes = checked(newYes + feeKept);
                    break;
            }
        }

        return new SellQuote(
            MarketId:m.State.MarketId,
            Side: side,
            SharesIn: sharesIn,
            GrossCollateralOut: grossOut,
            FeePaid: fee,
            NetCollateralOut: netOut,
            NewYesPool: newYes,
            NewNoPool: newNo);
    }

    // ---------------------------
    // Slippage helpers (optional)
    // ---------------------------

    public ulong ApplySlippageDown(ulong amount, ushort slippageBps)
    {
        // returns floor(amount * (1 - slippageBps/10_000))
        if (slippageBps >= BpsDenom) return 0;
        UInt128 a = amount;
        UInt128 keep = (UInt128)(BpsDenom - slippageBps);
        return (ulong)((a * keep) / BpsDenom);
    }

    // ---------------------------
    // CPMM math (matches Rust)
    // ---------------------------

    // Buying YES: add net_in to NO reserve, take YES out.
    private static (ulong newYes, ulong newNo, ulong sharesOut) CpmmBuyYes(ulong yesPool, ulong noPool, ulong netIn)
    {
        UInt128 x = yesPool;
        UInt128 y = noPool;
        UInt128 dy = netIn;

        UInt128 k = x * y;
        UInt128 yNew = y + dy;
        UInt128 xNew = k / yNew;

        UInt128 outShares = x - xNew;

        return ((ulong)xNew, (ulong)yNew, (ulong)outShares);
    }

    // Buying NO: add net_in to YES reserve, take NO out.
    private static (ulong newYes, ulong newNo, ulong sharesOut) CpmmBuyNo(ulong yesPool, ulong noPool, ulong netIn)
    {
        UInt128 x = noPool;
        UInt128 y = yesPool;
        UInt128 dy = netIn;

        UInt128 k = x * y;
        UInt128 yNew = y + dy;
        UInt128 xNew = k / yNew;

        UInt128 outShares = x - xNew;

        // Map back to (yes_pool, no_pool)
        return ((ulong)yNew, (ulong)xNew, (ulong)outShares);
    }

    // Selling YES: add shares_in to YES reserve, take NO out.
    private static (ulong newYes, ulong newNo, ulong grossOut) CpmmSellYes(ulong yesPool, ulong noPool, ulong sharesIn)
    {
        UInt128 x = yesPool;
        UInt128 y = noPool;
        UInt128 dx = sharesIn;

        UInt128 k = x * y;
        UInt128 xNew = x + dx;
        UInt128 yNew = k / xNew;

        UInt128 outCollat = y - yNew;

        return ((ulong)xNew, (ulong)yNew, (ulong)outCollat);
    }

    // Selling NO: add shares_in to NO reserve, take YES out.
    private static (ulong newYes, ulong newNo, ulong grossOut) CpmmSellNo(ulong yesPool, ulong noPool, ulong sharesIn)
    {
        UInt128 x = noPool;
        UInt128 y = yesPool;
        UInt128 dx = sharesIn;

        UInt128 k = x * y;
        UInt128 xNew = x + dx;
        UInt128 yNew = k / xNew;

        UInt128 outCollat = y - yNew;

        // Map back to (yes_pool, no_pool)
        return ((ulong)yNew, (ulong)xNew, (ulong)outCollat);
    }

    // ---------------------------
    // Fees (matches Rust)
    // ---------------------------

    private static (ulong net, ulong fee) ApplyFeeIn(ulong grossIn, ulong feeBps)
    {
        // fee = grossIn * feeBps / 10_000 (floor)
        UInt128 fee = ((UInt128)grossIn * feeBps) / BpsDenom;
        var feeU64 = (ulong)fee;
        return (grossIn - feeU64, feeU64);
    }

    private static (ulong net, ulong fee) ApplyFeeOut(ulong grossOut, ulong feeBps)
    {
        UInt128 fee = ((UInt128)grossOut * feeBps) / BpsDenom;
        var feeU64 = (ulong)fee;
        return (grossOut - feeU64, feeU64);
    }
}
