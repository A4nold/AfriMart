using System.Text.Json;
using System.Text.Json.Serialization;
using MarketService.Application.Exception;
using MarketService.Domain.Entities;
using MarketService.Domain.Interface;

namespace MarketService.Application.Helper;

// -------------------------------
// Serialization helper
// -------------------------------
public static class JsonX
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, Opts);
    public static T FromJson<T>(string json) => JsonSerializer.Deserialize<T>(json, Opts)!;
}

// -------------------------------
// Action execution helper (idempotent + retry-safe)
// -------------------------------
public sealed class MarketActionExecutor
{
    private readonly IMarketActionRepository _actions;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public MarketActionExecutor(IMarketActionRepository actions, IUnitOfWork uow, IClock clock)
    {
        _actions = actions;
        _uow = uow;
        _clock = clock;
    }

    public async Task<TResult> ExecuteAsync<TResult>(
    Guid marketId,
    Guid userId,
    MarketActionType type,
    string idempotencyKey,
    object request,
    Func<CancellationToken, Task<(string txSig, TResult result)>> chainCall,
    CancellationToken ct)
    {
        var action = await _actions.GetOrCreateAsync(new MarketAction
        {
            Id = Guid.NewGuid(),
            MarketId = marketId,
            RequestedByUserId = userId,
            ActionType = type,
            State = ActionState.Pending,
            IdempotencyKey = idempotencyKey,
            RequestJson = JsonX.ToJson(request),
            CreatedAtUtc = _clock.UtcNow,
            AttemptCount = 0
        }, ct);

        // 1) idempotent replay: confirmed -> return cached
        if (action.State == ActionState.Confirmed && action.ResponseJson != null)
            return JsonX.FromJson<TResult>(action.ResponseJson);

        // 2) submitted -> do NOT re-send
        if (action.State == ActionState.Submitted && !string.IsNullOrWhiteSpace(action.TxSignature))
        {
            // Optional: stale-submitted escape hatch (no schema change)
            var last = action.UpdatedAtUtc ?? action.CreatedAtUtc;
            if (_clock.UtcNow - last < TimeSpan.FromMinutes(2))
                throw new ActionInProgressException(action.IdempotencyKey, action.TxSignature);

            // Past 2 mins: allow retry OR you could throw a different "stuck" exception.
            // If you later add a confirm-by-signature function, call it here instead.
        }

        // 3) permanent failures should be sticky
        if (action.State == ActionState.Failed && action.AnchorErrorNumber != null)
        {
            var ex = new AnchorProgramException(
                action.RpcErrorText ?? "On-chain error",
                action.ErrorCode,
                action.AnchorErrorNumber);

            if (AnchorErrorClassifier.IsPermanent(ex))
                throw ex;

            // else: transient-ish anchor error, allow retry below
        }

        try
        {
            // 4) mark attempt (persist before external call)
            action.AttemptCount++;
            action.UpdatedAtUtc = _clock.UtcNow;
            await _uow.SaveChangesAsync(ct);

            // 5) call chain
            var (sig, result) = await chainCall(ct);

            // 6) record signature ASAP (Submitted) so a crash after send doesn't cause double-send
            action.State = ActionState.Submitted;
            action.TxSignature = sig;
            action.UpdatedAtUtc = _clock.UtcNow;
            await _uow.SaveChangesAsync(ct);

            // If chainCall already confirms/finalizes, now mark confirmed + cache
            action.State = ActionState.Confirmed;
            action.ResponseJson = JsonX.ToJson(result);
            action.ConfirmedAtUtc = _clock.UtcNow;
            action.UpdatedAtUtc = _clock.UtcNow;

            await _uow.SaveChangesAsync(ct);
            return result;
        }
        catch (AnchorProgramException ex)
        {
            action.State = ActionState.Failed;
            action.ErrorCode = ex.AnchorCode;
            action.AnchorErrorNumber = ex.AnchorNumber;
            action.RpcErrorText = ex.Message;
            action.UpdatedAtUtc = _clock.UtcNow;

            await _uow.SaveChangesAsync(ct);
            throw;
        }
        catch (System.Exception ex)
        {
            action.State = ActionState.Failed;
            action.ErrorCode = "TRANSIENT";
            action.RpcErrorText = ex.Message;
            action.UpdatedAtUtc = _clock.UtcNow;

            await _uow.SaveChangesAsync(ct);
            throw new ExternalDependencyException("Blockchain dependency failed.", ex);
        }
    }

}