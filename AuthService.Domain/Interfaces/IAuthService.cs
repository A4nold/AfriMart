using AuthService.Domain.Entities;
using AuthService.Domain.Models;
using AuthService.Domain.Models.Requests;
using AuthService.Domain.Models.Responses;

namespace AuthService.Domain.Interfaces
{
    public interface IAuthService
    {
        Task<User> SeedAdminAsync(AdminSeedRequest request);
        Task<LoginResponse> LoginAsync(LoginRequest request);
        Task<LoginResponse> RefreshAsync(string refreshToken);
        Task<WalletChallengeDto> CreateChallengeAsync(string walletPubkey, CancellationToken ct);
        Task<LoginResponse> VerifyChallengeAsync(string walletPubkey,string signatureBase64, 
            string challengeId, CancellationToken ct);
        Task LogOutAsync(string refreshToken);
        Task LogoutAllAsync(Guid userId);

    }
}
