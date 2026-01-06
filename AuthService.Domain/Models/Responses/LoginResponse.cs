namespace AuthService.Domain.Models.Responses;

public class LoginResponse
{
    public Guid UserId { get; set; }
    public string? Email { get; set; }
    public string? Alias { get; set; } 
    public IList<string> Roles { get; set; } = new List<string>();

    public string AccessToken { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
}
