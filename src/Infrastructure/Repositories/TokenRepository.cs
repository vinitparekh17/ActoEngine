using ActoX.Domain.Entities;
using ActoX.Domain.Interfaces;
using ActoX.Infrastructure.Data;
using ActoX.Infrastructure.Data.Sql;
using Microsoft.Extensions.Logging;

namespace ActoX.Infrastructure.Repositories
{
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

        public async Task<TokenSession?> GetByUserIdAsync(int userId)
        {
            var dto = await QueryFirstOrDefaultAsync<TokenSessionDto>(
                TokenSqlQueries.GetByUserId,
                new { UserID = userId });

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
}