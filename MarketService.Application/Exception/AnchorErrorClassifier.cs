namespace MarketService.Application.Exception;

public static class AnchorErrorClassifier
{
    // Your program:
    // 6001 InvalidMarketStatus
    // 6007 AlreadyClaimed
    public static bool IsPermanent(AnchorProgramException ex)
        => ex.AnchorNumber is 6001 or 6007;

    public static bool IsAlreadyClaimed(AnchorProgramException ex)
        => ex.AnchorNumber == 6007;

    public static bool IsInvalidMarketStatus(AnchorProgramException ex)
        => ex.AnchorNumber == 6001;
}