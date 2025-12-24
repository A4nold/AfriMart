namespace BlockchainService.Api.Exceptions;

public class AnchorProgramException: Exception
{
    public string? AnchorCode { get; }
    public int? AnchorNumber { get; }

    public AnchorProgramException(string message, string? anchorCode, int? anchorNumber)
        : base(message)
    {
        AnchorCode = anchorCode;
        AnchorNumber = anchorNumber;
    }
    
}