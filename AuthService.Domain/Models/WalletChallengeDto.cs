namespace AuthService.Domain.Models;

public sealed record WalletChallengeRequest(string WalletPubkey);

public sealed record WalletChallengeDto(
    Guid ChallengeId,
    string WalletPubkey,
    string Nonce,
    string MessageToSign,
    DateTime ExpiresAtUtc
);

public sealed record WalletChallengeResponse(
    Guid ChallengeId,
    string WalletPubkey,
    string Nonce,
    string Message,
    DateTime ExpiresAtUtc
    );
    
//signature is usually base58 from phantom, but we'll accept base64 too.
public sealed record WalletVerifyRequest(
    Guid ChallengeId,
    string WalletPubkey,
    string Signature
    );

public sealed record WalletVerifyResponse(
    Guid UserId,
    string WalletPubkey,
    string Jwt
);    