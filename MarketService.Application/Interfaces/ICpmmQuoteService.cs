using MarketService.Application.Dtos;
using MarketService.Domain.Models;

namespace MarketService.Application.Interfaces;

public interface ICpmmQuoteEngine
{
    BuyQuote QuoteBuy(
        MarketStateResponse m,
        OutcomeSide side,
        ulong maxCollateralIn,
        ulong feeBps);

    SellQuote QuoteSell(
        MarketStateResponse m,
        OutcomeSide side,
        ulong sharesIn,
        ulong feeBps);

    ulong ApplySlippageDown(ulong amount, ushort slippageBps);
}