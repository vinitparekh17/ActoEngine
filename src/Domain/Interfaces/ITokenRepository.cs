using ActoX.Domain.Entities;

namespace ActoX.Domain.Interfaces
{
    public interface ITokenRepository
    {
        Task StoreTokensAsync(int userId, string sessionToken, string refreshToken, DateTime sessionExpiry, DateTime refreshExpiry);
        Task<TokenSession?> GetByRefreshTokenHashAsync(string refreshToken);
        Task<TokenSession?> GetBySessionTokenHashAsync(string sessionToken);
        Task<TokenSession?> GetByUserIdAsync(int userId);
        Task RotateTokensAsync(int userId, string newSessionToken, string newRefreshToken, DateTime newSessionExpiry, DateTime newRefreshExpiry);
        Task DeleteByRefreshTokenHashAsync(string refreshToken);
        Task DeleteByUserIdAsync(int userId);
        Task CleanupExpiredTokensAsync();
    }
}