using ActoEngine.Domain.Entities;

namespace ActoEngine.WebApi.Models.Responses.Auth;

/// <summary>
/// Response returned after successful authentication.
/// Uses UserBasicInfo instead of full User entity to avoid exposing PasswordHash/PasswordSalt.
/// </summary>
public class AuthTokenResponse
{
    public required string Token { get; set; }
    public required string RefreshToken { get; set; }
    /// <summary>
    /// Safe user DTO that excludes sensitive fields like PasswordHash and PasswordSalt.
    /// </summary>
    public UserBasicInfo? User { get; set; }
    public DateTime ExpiresAt { get; set; }
}
