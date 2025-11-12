namespace ActoEngine.WebApi.SqlQueries;

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
                    DatabaseName = @DatabaseName, IsLinked = @IsLinked,
                    UpdatedAt = GETDATE(), UpdatedBy = @UserId
                WHERE ProjectId = @ProjectId;
                SELECT @ProjectId;
            END
            ELSE
            BEGIN
                INSERT INTO Projects (ProjectName, Description, DatabaseName, IsLinked, CreatedBy)
                VALUES (@ProjectName, @Description, @DatabaseName, @IsLinked, @UserId);
                SELECT SCOPE_IDENTITY();
            END";

    public const string GetById = @"
        SELECT ProjectId, ProjectName, Description, DatabaseName, IsLinked,
               IsActive, SyncStatus, SyncProgress, LastSyncAttempt,
               CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM Projects
        WHERE ProjectId = @ProjectID AND IsActive = 1";

    public const string GetByName = @"
        SELECT ProjectId, ProjectName, Description, DatabaseName, IsLinked,
               IsActive, SyncStatus, SyncProgress, LastSyncAttempt,
               CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM Projects
        WHERE ProjectName = @ProjectName AND CreatedBy = @CreatedBy AND IsActive = 1";

    public const string GetAll = @"
        SELECT ProjectId, ProjectName, Description, DatabaseName, IsLinked,
               IsActive, SyncStatus, SyncProgress, LastSyncAttempt,
               CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
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
            INSERT INTO Projects (ProjectName, Description, DatabaseName, IsLinked,
                                 IsActive, CreatedAt, CreatedBy)
            VALUES (@ProjectName, @Description, @DatabaseName, @IsLinked,
                    @IsActive, @CreatedAt, @CreatedBy);
            SELECT SCOPE_IDENTITY();
        END";

    public const string Update = @"
        UPDATE Projects
        SET ProjectName = @ProjectName,
            Description = @Description,
            DatabaseName = @DatabaseName,
            IsLinked = @IsLinked,
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