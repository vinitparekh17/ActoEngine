namespace ActoEngine.WebApi.Sql.Queries;

public static class TokenSqlQueries
{
    public const string Insert = @"
            INSERT INTO TokenSessions (UserID, SessionToken, SessionExpiresAt, RefreshToken, RefreshExpiresAt)
            VALUES (@UserID, @SessionToken, @SessionExpiresAt, @RefreshToken, @RefreshExpiresAt)";

    public const string GetByRefreshToken = @"
            SELECT UserID, SessionToken, SessionExpiresAt, RefreshToken, RefreshExpiresAt
            FROM TokenSessions 
            WHERE RefreshToken = @RefreshToken AND RefreshExpiresAt > GETUTCDATE()";

    public const string GetBySessionToken = @"
            SELECT UserID, SessionToken, SessionExpiresAt, RefreshToken, RefreshExpiresAt
            FROM TokenSessions 
            WHERE SessionToken = @SessionToken AND SessionExpiresAt > GETUTCDATE()";

    public const string GetByUserId = @"
            SELECT UserID, SessionToken, SessionExpiresAt, RefreshToken, RefreshExpiresAt
            FROM TokenSessions 
            WHERE UserID = @UserID";

    public const string Update = @"
            UPDATE TokenSessions 
            SET SessionToken = @SessionToken, 
                SessionExpiresAt = @SessionExpiresAt,
                RefreshToken = @RefreshToken,
                RefreshExpiresAt = @RefreshExpiresAt
            WHERE UserID = @UserID";

    public const string UpdateAccessToken = @"
            UPDATE TokenSessions 
            SET SessionToken = @SessionToken, 
                SessionExpiresAt = @SessionExpiresAt
            WHERE UserID = @UserID";

    public const string DeleteByRefreshToken = @"
            DELETE FROM TokenSessions WHERE RefreshToken = @RefreshToken";

    public const string DeleteByUserId = @"
            DELETE FROM TokenSessions WHERE UserID = @UserID";

    public const string DeleteExpired = @"
            DELETE FROM TokenSessions 
            WHERE RefreshExpiresAt <= @Now OR SessionExpiresAt <= @Now";
}