namespace ActoEngine.WebApi.SqlQueries;

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
        WHERE ClientId = @ClientId AND ProjectId = @ProjectId AND IsActive = 1";

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
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
    
        SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
        BEGIN TRANSACTION;
    
        BEGIN TRY
            DECLARE @ExistingId INT;
    
            -- Lock matching rows to prevent concurrent inserts
            SELECT @ExistingId = ClientId
            FROM Clients WITH (UPDLOCK, HOLDLOCK)
            WHERE ClientName = @ClientName AND ProjectId = @ProjectId;
    
            IF @ExistingId IS NOT NULL
            BEGIN
                -- Reactivate if soft-deleted
                IF EXISTS (
                    SELECT 1 FROM Clients
                    WHERE ClientId = @ExistingId AND IsActive = 0
                )
                BEGIN
                    UPDATE Clients
                    SET IsActive = 1,
                        UpdatedAt = @CreatedAt,
                        UpdatedBy = @CreatedBy
                    WHERE ClientId = @ExistingId;
                END
    
                SELECT @ExistingId AS ClientId;
            END
            ELSE
            BEGIN
                -- Insert new client
                INSERT INTO Clients (ClientName, ProjectId, IsActive, CreatedAt, CreatedBy)
                VALUES (@ClientName, @ProjectId, @IsActive, @CreatedAt, @CreatedBy);
    
                SELECT CAST(SCOPE_IDENTITY() AS INT) AS ClientId;
            END
    
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0
                ROLLBACK TRANSACTION;
    
            THROW;
        END CATCH;
";

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