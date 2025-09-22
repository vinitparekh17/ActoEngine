using System.Security.Claims;
using ActoX.Application.DTOs;

namespace ActoX.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResult> LoginAsync(string username, string password);
        Task<AuthResult> RefreshSessionAsync(string refreshToken);
        Task LogoutAsync(string refreshToken);
        Task LogoutByUserIdAsync(int userId);
        ClaimsPrincipal? ValidateAccessToken(string accessToken);
        Task<TokenRotationResult?> RotateTokensAsync(string currentAccessToken, string refreshToken);
    }
}