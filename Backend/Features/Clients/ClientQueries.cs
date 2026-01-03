namespace ActoEngine.WebApi.SqlQueries;

public static class ClientSqlQueries
{
    public const string CheckTableExists = @"
        SELECT CASE
            WHEN OBJECT_ID('dbo.Clients', 'U') IS NOT NULL THEN CAST(1 AS BIT)
            ELSE CAST(0 AS BIT)
        END";

    public const string GetById = @"
        SELECT ClientId, ClientName, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM Clients
        WHERE ClientId = @ClientId AND IsActive = 1";

    public const string GetByName = @"
        SELECT ClientId, ClientName, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM Clients
        WHERE ClientName = @ClientName AND IsActive = 1";

    public const string GetByNameAny = @"
        SELECT ClientId, ClientName, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM Clients
        WHERE ClientName = @ClientName";

    public const string GetAll = @"
        SELECT ClientId, ClientName, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM Clients
        WHERE IsActive = 1
        ORDER BY CreatedAt DESC";

    public const string GetCount = @"
        SELECT COUNT(*)
        FROM Clients
        WHERE IsActive = 1";

    public const string Insert = @"
        INSERT INTO Clients (ClientName, IsActive, CreatedAt, CreatedBy)
        VALUES (@ClientName, @IsActive, @CreatedAt, @CreatedBy);

        SELECT CAST(SCOPE_IDENTITY() AS INT) AS ClientId;";

    public const string Reactivate = @"
        UPDATE Clients
        SET IsActive = 1,
            UpdatedAt = @UpdatedAt,
            UpdatedBy = @UpdatedBy
        WHERE ClientName = @ClientName;";

    public const string Update = @"
        UPDATE Clients
        SET ClientName = @ClientName,
            UpdatedAt = @UpdatedAt,
            UpdatedBy = @UpdatedBy
        WHERE ClientId = @ClientId AND IsActive = 1";

    public const string SoftDelete = @"
        UPDATE Clients
        SET IsActive = 0,
            UpdatedAt = @UpdatedAt,
            UpdatedBy = @UpdatedBy
        WHERE ClientId = @ClientId AND IsActive = 1";
}