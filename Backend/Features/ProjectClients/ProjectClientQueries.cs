namespace ActoEngine.WebApi.Features.ProjectClients;

public static class ProjectClientSqlQueries
{
    public const string CheckTableExists = @"
        SELECT CASE
            WHEN OBJECT_ID('dbo.ProjectClients', 'U') IS NOT NULL THEN CAST(1 AS BIT)
            ELSE CAST(0 AS BIT)
        END";

    public const string GetById = @"
        SELECT ProjectClientId, ProjectId, ClientId, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM ProjectClients
        WHERE ProjectClientId = @ProjectClientId AND IsActive = 1";

    public const string GetByProjectAndClient = @"
        SELECT ProjectClientId, ProjectId, ClientId, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM ProjectClients
        WHERE ProjectId = @ProjectId AND ClientId = @ClientId AND IsActive = 1";

    public const string GetClientsByProject = @"
        SELECT
            pc.ProjectClientId,
            pc.ProjectId,
            pc.ClientId,
            c.ClientName,
            pc.IsActive,
            pc.CreatedAt,
            pc.CreatedBy,
            pc.UpdatedAt,
            pc.UpdatedBy
        FROM ProjectClients pc
        INNER JOIN Clients c ON pc.ClientId = c.ClientId
        WHERE pc.ProjectId = @ProjectId AND pc.IsActive = 1
        ORDER BY c.ClientName";

    public const string GetProjectsByClient = @"
        SELECT
            pc.ProjectClientId,
            pc.ProjectId,
            p.ProjectName,
            pc.ClientId,
            pc.IsActive,
            pc.CreatedAt,
            pc.CreatedBy,
            pc.UpdatedAt,
            pc.UpdatedBy
        FROM ProjectClients pc
        INNER JOIN Projects p ON pc.ProjectId = p.ProjectId
        WHERE pc.ClientId = @ClientId AND pc.IsActive = 1
        ORDER BY p.ProjectName";

    public const string GetAll = @"
        SELECT ProjectClientId, ProjectId, ClientId, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM ProjectClients
        WHERE IsActive = 1
        ORDER BY CreatedAt DESC";

    public const string IsLinked = @"
        SELECT CASE
            WHEN EXISTS (
                SELECT 1 FROM ProjectClients
                WHERE ProjectId = @ProjectId AND ClientId = @ClientId AND IsActive = 1
            ) THEN CAST(1 AS BIT)
            ELSE CAST(0 AS BIT)
        END";

    public const string GetByProjectAndClientAny = @"
        SELECT ProjectClientId, ProjectId, ClientId, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM ProjectClients
        WHERE ProjectId = @ProjectId AND ClientId = @ClientId";

    public const string Insert = @"
        INSERT INTO ProjectClients (ProjectId, ClientId, IsActive, CreatedAt, CreatedBy)
        VALUES (@ProjectId, @ClientId, @IsActive, @CreatedAt, @CreatedBy);

        SELECT CAST(SCOPE_IDENTITY() AS INT) AS ProjectClientId;";

    public const string Update = @"
        UPDATE ProjectClients
        SET IsActive = @IsActive,
            UpdatedAt = @UpdatedAt,
            UpdatedBy = @UpdatedBy
        WHERE ProjectClientId = @ProjectClientId;";

    public const string Reactivate = @"
        UPDATE ProjectClients
        SET IsActive = 1,
            UpdatedAt = @UpdatedAt,
            UpdatedBy = @UpdatedBy
        WHERE ProjectId = @ProjectId AND ClientId = @ClientId;";

    public const string SoftDelete = @"
        UPDATE ProjectClients
        SET IsActive = 0,
            UpdatedAt = @UpdatedAt,
            UpdatedBy = @UpdatedBy
        WHERE ProjectId = @ProjectId AND ClientId = @ClientId AND IsActive = 1";

    public const string HardDelete = @"
        DELETE FROM ProjectClients
        WHERE ProjectId = @ProjectId AND ClientId = @ClientId";
}
