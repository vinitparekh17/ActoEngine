namespace ActoEngine.WebApi.SqlQueries;

public static class SeedDataQueries
{
    public const string GetDefaultClientId = @"
        SELECT ClientId
        FROM Clients
        WHERE ClientName = 'Default Client' AND IsActive = 1;";

    public const string InsertDefaultClient = @"
        INSERT INTO Clients (ClientName, IsActive, CreatedAt, CreatedBy)
        VALUES ('Default Client', 1, GETUTCDATE(), @UserId);

        SELECT CAST(SCOPE_IDENTITY() AS INT) AS ClientId;";
}