using ActoEngine.Domain.Entities;

namespace ActoEngine.WebApi.Models.Responses.Auth;

public class AuthTokenResponse
{
    public required string Token { get; set; }
    public required string RefreshToken { get; set; }
    public User User { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
}

