using ActoEngine.WebApi.Services.Database;
using ActoEngine.Domain.Entities;
using ActoEngine.WebApi.SqlQueries;

namespace ActoEngine.WebApi.Repositories;

public interface ITokenRepository
{
    Task StoreTokensAsync(int userId, string sessionToken, string refreshToken, DateTime sessionExpiry, DateTime refreshExpiry);
    Task<TokenSession?> GetByRefreshTokenHashAsync(string refreshToken);
    Task<TokenSession?> GetBySessionTokenHashAsync(string sessionToken);
    Task RotateTokensAsync(int userId, string newSessionToken, string newRefreshToken, DateTime newSessionExpiry, DateTime newRefreshExpiry);
    Task UpdateAccessTokenAsync(int userId, string newSessionToken, DateTime newSessionExpiry);
    Task DeleteByRefreshTokenHashAsync(string refreshToken);
    Task DeleteByUserIdAsync(int userId);
    Task CleanupExpiredTokensAsync();
}

public class TokenRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<TokenRepository> logger)
    : BaseRepository(connectionFactory, logger), ITokenRepository
{
    public async Task StoreTokensAsync(
        int userId,
        string sessionToken,
        string refreshToken,
        DateTime sessionExpiry,
        DateTime refreshExpiry)
    {
        var parameters = new
        {
            UserID = userId,
            SessionToken = sessionToken,
            SessionExpiresAt = sessionExpiry,
            RefreshToken = refreshToken,
            RefreshExpiresAt = refreshExpiry
        };

        await ExecuteAsync(TokenSqlQueries.Insert, parameters);
    }

    public async Task<TokenSession?> GetByRefreshTokenHashAsync(string refreshToken)
    {
        var dto = await QueryFirstOrDefaultAsync<TokenSessionDto>(
            TokenSqlQueries.GetByRefreshToken,
            new { RefreshToken = refreshToken });

        return dto?.ToDomain();
    }

    public async Task<TokenSession?> GetBySessionTokenHashAsync(string sessionToken)
    {
        var dto = await QueryFirstOrDefaultAsync<TokenSessionDto>(
            TokenSqlQueries.GetBySessionToken,
            new { SessionToken = sessionToken });

        return dto?.ToDomain();
    }

    public async Task RotateTokensAsync(
        int userId,
        string newSessionToken,
        string newRefreshToken,
        DateTime newSessionExpiry,
        DateTime newRefreshExpiry)
    {
        var parameters = new
        {
            UserID = userId,
            SessionToken = newSessionToken,
            SessionExpiresAt = newSessionExpiry,
            RefreshToken = newRefreshToken,
            RefreshExpiresAt = newRefreshExpiry
        };

        var rowsAffected = await ExecuteAsync(TokenSqlQueries.Update, parameters);
        if (rowsAffected == 0)
            throw new InvalidOperationException($"No token session found for UserID {userId}");
    }

    public async Task UpdateAccessTokenAsync(int userId, string newSessionToken, DateTime newSessionExpiry)
    {
        var parameters = new
        {
            UserID = userId,
            SessionToken = newSessionToken,
            SessionExpiresAt = newSessionExpiry
        };

        var rowsAffected = await ExecuteAsync(TokenSqlQueries.UpdateAccessToken, parameters);
        if (rowsAffected == 0)
            throw new InvalidOperationException($"No token session found for UserID {userId}");
    }

    public async Task DeleteByRefreshTokenHashAsync(string refreshToken)
    {
        var rowsAffected = await ExecuteAsync(
            TokenSqlQueries.DeleteByRefreshToken,
            new { RefreshToken = refreshToken });

        if (rowsAffected == 0)
            throw new InvalidOperationException("Refresh token not found");
    }

    public async Task DeleteByUserIdAsync(int userId)
    {
        await ExecuteAsync(
            TokenSqlQueries.DeleteByUserId,
            new { UserID = userId });
    }

    public async Task CleanupExpiredTokensAsync()
    {
        await ExecuteAsync(TokenSqlQueries.DeleteExpired, new { Now = DateTime.UtcNow });
    }

    private class TokenSessionDto
    {
        public int UserID { get; set; }
        public string SessionToken { get; set; } = string.Empty;
        public DateTime SessionExpiresAt { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime RefreshExpiresAt { get; set; }

        public TokenSession ToDomain()
        {
            return new TokenSession
            {
                UserID = UserID,
                SessionToken = SessionToken,
                SessionExpiresAt = SessionExpiresAt,
                RefreshToken = RefreshToken,
                RefreshExpiresAt = RefreshExpiresAt
            };
        }
    }
}