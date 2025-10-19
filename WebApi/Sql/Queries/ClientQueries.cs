namespace ActoEngine.WebApi.Sql.Queries;

public static class ClientSqlQueries
{
    public const string CheckTableExists = @"
        SELECT CASE
            WHEN OBJECT_ID('dbo.Clients', 'U') IS NOT NULL THEN CAST(1 AS BIT)
            ELSE CAST(0 AS BIT)
        END";

    public const string GetById = @"
        SELECT ClientId, ProjectId, ClientName, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM Clients
        WHERE ClientId = @ClientId AND IsActive = 1";

    public const string GetByName = @"
        SELECT ClientId, ProjectId, ClientName, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM Clients
        WHERE ClientName = @ClientName AND ProjectId = @ProjectId AND IsActive = 1";

    public const string GetAll = @"
        SELECT ClientId, ProjectId, ClientName, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM Clients
        WHERE IsActive = 1
        ORDER BY CreatedAt DESC";

    public const string GetAllByProject = @"
        SELECT ClientId, ProjectId, ClientName, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM Clients
        WHERE ProjectId = @ProjectId AND IsActive = 1
        ORDER BY CreatedAt DESC";

    public const string GetCount = @"
        SELECT COUNT(*)
        FROM Clients
        WHERE ProjectId = @ProjectId AND IsActive = 1";

    public const string Insert = @"
        IF NOT EXISTS (SELECT 1 FROM Clients WHERE ClientName = @ClientName AND ProjectId = @ProjectId)
        BEGIN
            INSERT INTO Clients (ClientName, ProjectId, IsActive, CreatedAt, CreatedBy)
            VALUES (@ClientName, @ProjectId, @IsActive, @CreatedAt, @CreatedBy);
            SELECT SCOPE_IDENTITY();
        END";

    public const string Update = @"
        UPDATE Clients
        SET ClientName = @ClientName,
            UpdatedAt = @UpdatedAt,
            UpdatedBy = @UpdatedBy
        WHERE ClientId = @ClientId AND ProjectId = @ProjectId AND IsActive = 1";

    public const string SoftDelete = @"
        UPDATE Clients
        SET IsActive = 0,
            UpdatedAt = @UpdatedAt,
            UpdatedBy = @UpdatedBy
        WHERE ClientId = @ClientId AND ProjectId = @ProjectId AND IsActive = 1";
}