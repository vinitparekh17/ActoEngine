using ActoEngine.WebApi.Infrastructure.Database;
using ActoEngine.WebApi.Shared;

namespace ActoEngine.WebApi.Features.Auth;

public interface IExtensionAuthRepository
{
    Task StoreAuthorizationCodeAsync(ExtensionAuthCode code, CancellationToken ct = default);
    Task<ExtensionAuthCode?> GetAuthorizationCodeByHashAsync(string codeHash, CancellationToken ct = default);
    Task<bool> MarkAuthorizationCodeConsumedAsync(int authCodeId, CancellationToken ct = default);

    Task StoreTokenSessionAsync(ExtensionTokenSession session, CancellationToken ct = default);
    Task<ExtensionTokenSession?> GetSessionByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken ct = default);
    Task<ExtensionTokenSession?> GetSessionByAccessTokenHashAsync(string accessTokenHash, CancellationToken ct = default);
    Task<bool> RotateTokenSessionAsync(int sessionId, string accessTokenHash, DateTime accessExpiresAt, string refreshTokenHash, DateTime refreshExpiresAt, CancellationToken ct = default);
}

public class ExtensionAuthRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<ExtensionAuthRepository> logger) : BaseRepository(connectionFactory, logger), IExtensionAuthRepository
{
    public Task StoreAuthorizationCodeAsync(ExtensionAuthCode code, CancellationToken ct = default)
    {
        return ExecuteAndIgnoreResultAsync(ExtensionAuthQueries.InsertAuthCode, code, ct);
    }

    public Task<ExtensionAuthCode?> GetAuthorizationCodeByHashAsync(string codeHash, CancellationToken ct = default)
    {
        return QueryFirstOrDefaultAsync<ExtensionAuthCode>(
            ExtensionAuthQueries.GetAuthCodeByHash,
            new { CodeHash = codeHash },
            ct);
    }

    public async Task<bool> MarkAuthorizationCodeConsumedAsync(int authCodeId, CancellationToken ct = default)
    {
        var rows = await ExecuteAsync(ExtensionAuthQueries.MarkAuthCodeConsumed, new { AuthCodeId = authCodeId }, ct);
        return rows > 0;
    }

    public Task StoreTokenSessionAsync(ExtensionTokenSession session, CancellationToken ct = default)
    {
        return ExecuteAndIgnoreResultAsync(ExtensionAuthQueries.InsertTokenSession, session, ct);
    }

    public Task<ExtensionTokenSession?> GetSessionByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken ct = default)
    {
        return QueryFirstOrDefaultAsync<ExtensionTokenSession>(
            ExtensionAuthQueries.GetSessionByRefreshToken,
            new { RefreshToken = refreshTokenHash },
            ct);
    }

    public Task<ExtensionTokenSession?> GetSessionByAccessTokenHashAsync(string accessTokenHash, CancellationToken ct = default)
    {
        return QueryFirstOrDefaultAsync<ExtensionTokenSession>(
            ExtensionAuthQueries.GetSessionByAccessToken,
            new { AccessToken = accessTokenHash },
            ct);
    }

    public async Task<bool> RotateTokenSessionAsync(
        int sessionId,
        string accessTokenHash,
        DateTime accessExpiresAt,
        string refreshTokenHash,
        DateTime refreshExpiresAt,
        CancellationToken ct = default)
    {
        var rows = await ExecuteAsync(
            ExtensionAuthQueries.RotateTokenSession,
            new
            {
                SessionId = sessionId,
                AccessToken = accessTokenHash,
                AccessExpiresAt = accessExpiresAt,
                RefreshToken = refreshTokenHash,
                RefreshExpiresAt = refreshExpiresAt
            },
            ct);

        return rows > 0;
    }

    private async Task ExecuteAndIgnoreResultAsync(string sql, object parameters, CancellationToken ct)
    {
        await ExecuteAsync(sql, parameters, ct);
    }
}
