namespace ActoEngine.WebApi.Features.Auth;

public static class ExtensionAuthQueries
{
    public const string InsertAuthCode = """
        INSERT INTO ExtensionAuthCodes
            (UserID, ClientId, RedirectUri, CodeHash, CodeChallenge, CodeChallengeMethod, State, ExpiresAt)
        VALUES
            (@UserID, @ClientId, @RedirectUri, @CodeHash, @CodeChallenge, @CodeChallengeMethod, @State, @ExpiresAt)
        """;

    public const string GetAuthCodeByHash = """
        SELECT AuthCodeId, UserID, ClientId, RedirectUri, CodeHash, CodeChallenge, CodeChallengeMethod, State, ExpiresAt, ConsumedAt
        FROM ExtensionAuthCodes
        WHERE CodeHash = @CodeHash
        """;

    public const string MarkAuthCodeConsumed = """
        UPDATE ExtensionAuthCodes
        SET ConsumedAt = GETUTCDATE()
        WHERE AuthCodeId = @AuthCodeId AND ConsumedAt IS NULL
        """;

    public const string InsertTokenSession = """
        INSERT INTO ExtensionTokenSessions
            (UserID, ClientId, AccessToken, AccessExpiresAt, RefreshToken, RefreshExpiresAt)
        VALUES
            (@UserID, @ClientId, @AccessToken, @AccessExpiresAt, @RefreshToken, @RefreshExpiresAt)
        """;

    public const string GetSessionByRefreshToken = """
        SELECT SessionId, UserID, ClientId, AccessToken, AccessExpiresAt, RefreshToken, RefreshExpiresAt, RevokedAt, UpdatedAt
        FROM ExtensionTokenSessions
        WHERE RefreshToken = @RefreshToken AND RevokedAt IS NULL
        """;

    public const string GetSessionByAccessToken = """
        SELECT SessionId, UserID, ClientId, AccessToken, AccessExpiresAt, RefreshToken, RefreshExpiresAt, RevokedAt, UpdatedAt
        FROM ExtensionTokenSessions
        WHERE AccessToken = @AccessToken AND RevokedAt IS NULL
        """;

    public const string RotateTokenSession = """
        UPDATE ExtensionTokenSessions
        SET AccessToken = @AccessToken,
            AccessExpiresAt = @AccessExpiresAt,
            RefreshToken = @RefreshToken,
            RefreshExpiresAt = @RefreshExpiresAt,
            UpdatedAt = GETUTCDATE()
        WHERE SessionId = @SessionId
          AND RevokedAt IS NULL
          AND (UpdatedAt = @ExpectedUpdatedAt OR (UpdatedAt IS NULL AND @ExpectedUpdatedAt IS NULL))
        """;
}
