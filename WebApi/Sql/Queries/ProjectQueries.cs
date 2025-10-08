namespace ActoEngine.WebApi.Sql.Queries;

public static class ProjectSqlQueries
{
    public const string CheckTableExists = @"
        SELECT CASE 
            WHEN OBJECT_ID('dbo.Projects', 'U') IS NOT NULL THEN CAST(1 AS BIT) 
            ELSE CAST(0 AS BIT) 
        END";

    public const string AddOrUpdateProject = @"
            IF EXISTS (SELECT 1 FROM Projects WHERE ProjectId = @ProjectId)
            BEGIN
                UPDATE Projects
                SET ProjectName = @ProjectName, Description = @Description, 
                    DatabaseName = @DatabaseName, ConnectionString = @ConnectionString,
                    UpdatedAt = GETDATE(), UpdatedBy = @UserId
                WHERE ProjectId = @ProjectId;
                SELECT @ProjectId;
            END
            ELSE
            BEGIN
                INSERT INTO Projects (ProjectName, Description, DatabaseName, ConnectionString, CreatedBy)
                VALUES (@ProjectName, @Description, @DatabaseName, @ConnectionString, @UserId);
                SELECT SCOPE_IDENTITY();
            END";

    // Internal queries - include ConnectionString for system operations
    public const string GetByIdInternal = @"
        SELECT ProjectId, ProjectName, Description, DatabaseName, ConnectionString, 
               IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM Projects 
        WHERE ProjectId = @ProjectID AND IsActive = 1";

    // Public queries - exclude ConnectionString for API responses
    public const string GetById = @"
        SELECT ProjectId, ProjectName, Description, DatabaseName, 
               IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy,
               CASE WHEN ConnectionString IS NOT NULL AND ConnectionString != '' THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS HasConnection
        FROM Projects 
        WHERE ProjectId = @ProjectID AND IsActive = 1";

    public const string GetByName = @"
        SELECT ProjectId, ProjectName, Description, DatabaseName, 
               IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy,
               CASE WHEN ConnectionString IS NOT NULL AND ConnectionString != '' THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS HasConnection
        FROM Projects 
        WHERE ProjectName = @ProjectName AND CreatedBy = @CreatedBy AND IsActive = 1";

    public const string GetAll = @"
        SELECT ProjectId, ProjectName, Description, DatabaseName, 
               IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy,
               CASE WHEN ConnectionString IS NOT NULL AND ConnectionString != '' THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS HasConnection
        FROM Projects 
        WHERE IsActive = 1
        ORDER BY CreatedAt DESC";

    public const string GetCount = @"
        SELECT COUNT(*) 
        FROM Projects 
        WHERE CreatedBy = @CreatedBy AND IsActive = 1";

    public const string Insert = @"
        IF NOT EXISTS (SELECT 1 FROM Projects WHERE ProjectName = @ProjectName AND CreatedBy = @CreatedBy)
        BEGIN
            INSERT INTO Projects (ProjectName, Description, DatabaseName, ConnectionString, 
                                 IsActive, CreatedAt, CreatedBy)
            VALUES (@ProjectName, @Description, @DatabaseName, @ConnectionString, 
                    @IsActive, @CreatedAt, @CreatedBy);
            SELECT SCOPE_IDENTITY();
        END";

    public const string Update = @"
        UPDATE Projects 
        SET ProjectName = @ProjectName,
            Description = @Description,
            DatabaseName = @DatabaseName,
            ConnectionString = @ConnectionString,
            UpdatedAt = @UpdatedAt,
            UpdatedBy = @UpdatedBy
        WHERE ProjectId = @ProjectID AND CreatedBy = @CreatedBy AND IsActive = 1";

    public const string SoftDelete = @"
        UPDATE Projects 
        SET IsActive = 0,
            UpdatedAt = @UpdatedAt,
            UpdatedBy = @UpdatedBy
        WHERE ProjectId = @ProjectID AND CreatedBy = @CreatedBy AND IsActive = 1";

    public const string TestConnection = @"
        SELECT @@VERSION as ServerVersion";
}