using System;
namespace MarketService.Application.Exception;

public abstract class AppException : System.Exception
{
    protected AppException(string message, System.Exception? inner = null) : base(message, inner) { }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message) { }
}

public sealed class ConflictException : AppException
{
    public ConflictException(string message) : base(message) { }
}

public sealed class ValidationException : AppException
{
    public ValidationException(string message) : base(message) { }
}

public sealed class ExternalDependencyException : AppException
{
    public ExternalDependencyException(string message, System.Exception? inner = null) : base(message, inner) { }
}

public sealed class ActionInProgressException : AppException
{
    public string IdempotencyKey { get; }
    public string? TxSignature { get; }

    public ActionInProgressException(
        string idempotencyKey,
        string? txSignature = null,
        System.Exception? inner = null)
        : base($"Action is already in progress for idempotency key '{idempotencyKey}'.", inner)
    {
        IdempotencyKey = idempotencyKey;
        TxSignature = txSignature;
    }
}


// This is a duplicate error class already in BlockchainService.
// Can use a reference of it in the future
public sealed class AnchorProgramException : System.Exception
{
    public string? AnchorCode { get; }
    public int? AnchorNumber { get; }

    public AnchorProgramException(string message, string? anchorCode, int? anchorNumber, System.Exception? inner = null)
        : base(message, inner)
    {
        AnchorCode = anchorCode;
        AnchorNumber = anchorNumber;
    }
}