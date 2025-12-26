using MarketService.Application.Commands;
using MarketService.Application.Exception;
using MarketService.Application.Helper;
using MarketService.Application.Interfaces;
using MarketService.Application.Requests;
using MarketService.Domain.Commands;
using MarketService.Domain.Entities;
using MarketService.Domain.Interface;
using CreateMarketCommand = MarketService.Application.Commands.CreateMarketCommand;

namespace MarketService.Application.Services;

public sealed class MarketApplication : IMarketApplication
{
    private readonly IMarketRepository _markets;
    private readonly IUserPositionRepository _positions;
    private readonly IMarketActionRepository _actions;
    private readonly IBlockchainGateway _chain;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly MarketActionExecutor _exec;

    public MarketApplication(
        IMarketRepository markets,
        IUserPositionRepository positions,
        IMarketActionRepository actions,
        IBlockchainGateway chain,
        IUnitOfWork uow,
        IClock clock)
    {
        _markets = markets;
        _positions = positions;
        _actions = actions;
        _chain = chain;
        _uow = uow;
        _clock = clock;
        _exec = new MarketActionExecutor(actions, uow, clock);
    }

    public async Task<CreateMarketResult> CreateMarketAsync(CreateMarketCommand cmd, CancellationToken ct)
    {
        if (cmd.InitialLiquidity == 0) throw new ValidationException("InitialLiquidity must be > 0.");
        if (cmd.EndTimeUtc <= _clock.UtcNow) throw new ValidationException("EndTimeUtc must be in the future.");
        if (string.IsNullOrWhiteSpace(cmd.CollateralMint)) throw new ValidationException("CollateralMint is required.");

        // Natural idempotency for create (seed + authority)
        var existing = await _markets.FindByAuthorityAndSeedAsync(_chain.AuthorityPubKey, cmd.MarketSeedId, ct);
        if (existing is not null)
        {
            // if we have a confirmed creation action, return it; else return market anyway
            var act = await _actions.GetLatestForMarketAsync(existing.Id, MarketActionType.Create, ct);
            if (act?.State == ActionState.Confirmed && act.TxSignature != null)
                return new CreateMarketResult(existing.Id, existing.MarketPubKey, act.TxSignature);

            return new CreateMarketResult(existing.Id, existing.MarketPubKey, existing.CreatedAtUtc.ToString("O"));
        }

        // Create market row BEFORE chain call (so we can attach actions)
        var market = new Domain.Entities.Market
        {
            Id = Guid.NewGuid(),
            MarketPubKey = "", // will fill after chain call
            Question = cmd.Question,
            EndTimeUtc = cmd.EndTimeUtc,
            Status = Domain.Entities.MarketStatus.Open,
            CreatorUserId = cmd.CreatorUserId,
            CreatedAtUtc = _clock.UtcNow,
            WinningOutcomeIndex = null,
            ResolvedAtUtc = null,
            SettledAtUtc = null,
            Outcomes = new List<Domain.Entities.MarketOutcome>
            {
                new() { OutcomeIndex = 0, Label = "YES" },
                new() { OutcomeIndex = 1, Label = "NO" }
            }
        };
        
        market.MarketSeedId = cmd.MarketSeedId;
        market.AuthorityPubKey = _chain.AuthorityPubKey;
        market.ProgramId = _chain.ProgramId;
        market.CollateralMint = cmd.CollateralMint;

        await _markets.AddAsync(market, ct);
        await _uow.SaveChangesAsync(ct);

        // Execute create action idempotently
        return await _exec.ExecuteAsync(
            marketId: market.Id,
            userId: cmd.CreatorUserId,
            type: MarketActionType.Create,
            idempotencyKey: cmd.IdempotencyKey,
            request: cmd,
            chainCall: async (innerCt) =>
            {
                var res = await _chain.CreateMarketAsync(new BlockchainCreateMarketRequest(
                    MarketId: cmd.MarketSeedId,
                    Question: cmd.Question,
                    EndTimeUtc: cmd.EndTimeUtc,
                    InitialLiquidity: cmd.InitialLiquidity,
                    CollateralMint: cmd.CollateralMint
                ), innerCt);

                // persist market pubkey now that we have it
                market.MarketPubKey = res.MarketPubkey;
                await _uow.SaveChangesAsync(innerCt);

                var result = new CreateMarketResult(market.Id, res.MarketPubkey, res.TransactionSignature);
                return (res.TransactionSignature, result);
            },
            ct);
    }

    public async Task<ResolveMarketResult> ResolveMarketAsync(ResolveMarketCommand cmd, CancellationToken ct)
    {
        var market = await _markets.GetByPubKeyAsync(cmd.MarketPubKey, ct)
            ?? throw new NotFoundException("Market not found.");

        if (cmd.WinningOutcomeIndex > 1)
            throw new ValidationException("WinningOutcomeIndex must be 0 or 1.");

        return await _exec.ExecuteAsync(
            marketId: market.Id,
            userId: cmd.ResolverUserId,
            type: MarketActionType.Resolve,
            idempotencyKey: cmd.IdempotencyKey,
            request: cmd,
            chainCall: async (innerCt) =>
            {
                // Optional but recommended: reload for freshest status
                var freshMarket = await _markets.GetByPubKeyAsync(cmd.MarketPubKey, innerCt)
                    ?? throw new NotFoundException("Market not found.");

                if (freshMarket.Status == Domain.Entities.MarketStatus.Resolved)
                {
                    // If you want strictness:
                    throw new ConflictException("Market is already resolved.");

                    // Or if you want idempotent-ish success when same winner:
                    // if (freshMarket.WinningOutcomeIndex == cmd.WinningOutcomeIndex)
                    //     return ("", new ResolveMarketResult(freshMarket.Id, freshMarket.MarketPubKey, cmd.WinningOutcomeIndex, ""));
                    // throw new ConflictException("Market is already resolved with a different outcome.");
                }

                var res = await _chain.ResolveMarketAsync(new BlockchainResolveMarketRequest(
                    MarketPubkey: cmd.MarketPubKey,
                    WinningOutcomeIndex: cmd.WinningOutcomeIndex
                ), innerCt);

                freshMarket.Status = Domain.Entities.MarketStatus.Resolved;
                freshMarket.WinningOutcomeIndex = cmd.WinningOutcomeIndex;
                freshMarket.ResolvedAtUtc = _clock.UtcNow;

                var result = new ResolveMarketResult(
                    freshMarket.Id,
                    freshMarket.MarketPubKey,
                    cmd.WinningOutcomeIndex,
                    res.TransactionSignature);

                return (res.TransactionSignature, result); 
            }, 
            ct);
    }
    
    public async Task<BuySharesResult> BuySharesAsync(BuySharesCommand cmd, CancellationToken ct)
    {
        var market = await _markets.GetByPubKeyAsync(cmd.MarketPubKey, ct)
            ?? throw new NotFoundException("Market not found.");

        if (cmd.OutcomeIndex > 1) throw new ValidationException("OutcomeIndex must be 0 or 1.");
        if (cmd.MaxCollateralIn == 0) throw new ValidationException("MaxCollateralIn must be > 0.");

        return await _exec.ExecuteAsync(
            marketId: market.Id,
            userId: cmd.UserId,
            type: MarketActionType.Buy,
            idempotencyKey: cmd.IdempotencyKey,
            request: cmd,
            chainCall: async (innerCt) =>
            {
                var freshMarket = await _markets.GetByPubKeyAsync(cmd.MarketPubKey, innerCt)
                                  ?? throw new NotFoundException("Market not found.");

                if (freshMarket.Status != Domain.Entities.MarketStatus.Open)
                    throw new ConflictException("Market is not open for trading.");

                var res = await _chain.BuySharesAsync(new BlockchainBuyRequest(
                    MarketPubkey: cmd.MarketPubKey,
                    MaxCollateralIn: cmd.MaxCollateralIn,
                    MinSharesOut: cmd.MinSharesOut,
                    OutcomeIndex: cmd.OutcomeIndex
                ), innerCt);

                await _positions.UpsertAfterTradeAsync(cmd.UserId, market.Id, cmd.OutcomeIndex, res.TransactionSignature, innerCt);

                var result = new BuySharesResult(market.Id, market.MarketPubKey, cmd.OutcomeIndex, res.TransactionSignature);
                return (res.TransactionSignature, result);
            },
            ct);
    }

    public async Task<SellSharesResult> SellSharesAsync(SellSharesCommand cmd, CancellationToken ct)
    {
        var market = await _markets.GetByPubKeyAsync(cmd.MarketPubKey, ct)
                     ?? throw new NotFoundException("Market not found.");

        if (cmd.OutcomeIndex > 1) throw new ValidationException("OutcomeIndex must be 0 or 1.");
        if (cmd.SharesIn == 0) throw new ValidationException("SharesIn must be > 0.");

        return await _exec.ExecuteAsync(
            marketId: market.Id,
            userId: cmd.UserId,
            type: MarketActionType.Sell,
            idempotencyKey: cmd.IdempotencyKey,
            request: cmd,
            chainCall: async (innerCt) =>
            {
                var freshMarket = await _markets.GetByPubKeyAsync(cmd.MarketPubKey, innerCt)
                                  ?? throw new NotFoundException("Market not found.");

                if (freshMarket.Status != Domain.Entities.MarketStatus.Open)
                    throw new ConflictException("Market is not open for trading.");

                var res = await _chain.SellSharesAsync(new BlockchainSellRequest(
                    MarketPubkey: cmd.MarketPubKey,
                    SharesIn: cmd.SharesIn,
                    MinCollateralOut: cmd.MinCollateralOut,
                    OutcomeIndex: cmd.OutcomeIndex
                ), innerCt);

                await _positions.UpsertAfterTradeAsync(
                    cmd.UserId, freshMarket.Id, cmd.OutcomeIndex, res.TransactionSignature, innerCt);

                var result = new SellSharesResult(
                    freshMarket.Id, freshMarket.MarketPubKey, cmd.OutcomeIndex, res.TransactionSignature);

                return (res.TransactionSignature, result);
            },
            ct);
    }

    public async Task<ClaimWinningsResult> ClaimWinningsAsync(ClaimWinningsCommand cmd, CancellationToken ct)
    {
        var market = await _markets.GetByPubKeyAsync(cmd.MarketPubKey, ct)
                     ?? throw new NotFoundException("Market not found.");

        return await _exec.ExecuteAsync(
            marketId: market.Id,
            userId: cmd.UserId,
            type: MarketActionType.Claim,
            idempotencyKey: cmd.IdempotencyKey,
            request: cmd,
            chainCall: async (innerCt) =>
            {
                var freshMarket = await _markets.GetByPubKeyAsync(cmd.MarketPubKey, innerCt)
                                  ?? throw new NotFoundException("Market not found.");

                if (freshMarket.Status != Domain.Entities.MarketStatus.Resolved)
                    throw new ConflictException("Market is not resolved yet.");

                await _positions.EnsureExistsAsync(cmd.UserId, market.Id, innerCt);

                try
                {
                    var res = await _chain.ClaimWinningsAsync(
                        new BlockchainClaimRequest(cmd.MarketPubKey),
                        innerCt);

                    await _positions.MarkClaimedAsync(
                        cmd.UserId, market.Id, innerCt);

                    var result = new ClaimWinningsResult(
                        market.Id, market.MarketPubKey, res.TransactionSignature);

                    return (res.TransactionSignature, result);
                }
                catch (AnchorProgramException ex) when (AnchorErrorClassifier.IsAlreadyClaimed(ex))
                {
                    await _positions.MarkClaimedAsync(cmd.UserId, market.Id, innerCt);

                    var previous = await _actions.GetLatestForMarketAndUserAsync(
                        market.Id, cmd.UserId, MarketActionType.Claim, innerCt);

                    if (previous?.TxSignature is { Length: > 0 })
                    {
                        var result = new ClaimWinningsResult(market.Id, market.MarketPubKey, previous.TxSignature);
                        return (previous.TxSignature, result);
                    }

                    // If we can’t find it, surface a clear error (rare: e.g. claimed outside your system)
                    throw new ConflictException("Winnings are already claimed, but no previous claim transaction was found.");
                }
            },
            ct);
    }

}