namespace ActoEngine.WebApi.SqlQueries;

public static class SchemaSyncQueries
{
    // Server detection
    public const string GetServerName = @"SELECT @@SERVERNAME";

    // Progress tracking
    public const string UpdateSyncStatus = @"
        UPDATE Projects 
        SET SyncStatus = @Status, 
            SyncProgress = @Progress,
            LastSyncAttempt = GETUTCDATE()
        WHERE ProjectId = @ProjectId";

    public const string GetSyncStatus = @"
        SELECT SyncStatus as Status, SyncProgress, LastSyncAttempt
        FROM Projects
        WHERE ProjectId = @ProjectId";

    // Table sync - Target database queries
    public const string GetTargetTables = @"
        SELECT name 
        FROM sys.tables 
        WHERE type = 'U' 
        ORDER BY name";

    public const string GetTargetTablesWithSchema = @"
        SELECT 
            -- Use INFORMATION_SCHEMA.TABLE_CONSTRAINTS joined with KEY_COLUMN_USAGE to reliably detect PK
            CASE WHEN EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME AND tc.TABLE_NAME = kcu.TABLE_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                  AND kcu.TABLE_NAME = c.TABLE_NAME
                  AND kcu.COLUMN_NAME = c.COLUMN_NAME
            ) THEN 1 ELSE 0 END AS IsPrimaryKey,
            t.name AS TableName
        FROM sys.tables t
            -- Similarly detect foreign keys using constraint metadata rather than name patterns
            CASE WHEN EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME AND tc.TABLE_NAME = kcu.TABLE_NAME
                WHERE tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
                  AND kcu.TABLE_NAME = c.TABLE_NAME
                  AND kcu.COLUMN_NAME = c.COLUMN_NAME
            ) THEN 1 ELSE 0 END AS IsForeignKey,
        ORDER BY s.name, t.name";
    public const string InsertTableMetadata = @"
        SET NOCOUNT ON;
        -- No direct joins required; constraint membership is computed via EXISTS above
            INSERT INTO TablesMetadata (ProjectId, TableName, SchemaName, CreatedAt)
            OUTPUT inserted.TableId INTO @Inserted
            SELECT @ProjectId, @TableName, @SchemaName, GETUTCDATE()
            WHERE NOT EXISTS (
                SELECT 1
                FROM TablesMetadata WITH (UPDLOCK, HOLDLOCK)
                WHERE ProjectId = @ProjectId AND TableName = @TableName
            );
    
            -- If no insert happened, select the existing TableId
            IF NOT EXISTS (SELECT 1 FROM @Inserted)
            BEGIN
                SELECT TableId
                FROM TablesMetadata
                WHERE ProjectId = @ProjectId AND TableName = @TableName;
            END
            ELSE
            BEGIN
                SELECT TableId FROM @Inserted;
            END
    
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0
                ROLLBACK TRANSACTION;
    
            THROW;
        END CATCH;
";
    public const string GetTableId = @"
        SELECT TableId 
        FROM TablesMetadata 
        WHERE ProjectId = @ProjectId AND TableName = @TableName";

    public const string GetTableMetaByProjectId = @"
        SELECT TableId, TableName 
        FROM TablesMetadata 
        WHERE ProjectId = @ProjectId";

    // Column sync - Target database queries
    public const string GetTargetColumns = @"
        SELECT
            c.name AS ColumnName,
            typ.name AS DataType,
            c.max_length AS MaxLength,
            c.precision AS Precision,
            c.scale AS Scale,
            c.is_nullable AS IsNullable,
            CASE WHEN kc.type = 'PK' THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsPrimaryKey,
            CASE WHEN fk.parent_column_id IS NOT NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsForeignKey,
            c.column_id AS ColumnOrder
        FROM sys.columns c
            INNER JOIN sys.tables t ON c.object_id = t.object_id
            INNER JOIN sys.types typ ON c.user_type_id = typ.user_type_id
            LEFT JOIN sys.index_columns ic ON ic.object_id = c.object_id AND ic.column_id = c.column_id AND ic.is_included_column = 0
            LEFT JOIN sys.key_constraints kc ON kc.parent_object_id = c.object_id AND kc.unique_index_id = ic.index_id AND kc.type = 'PK'
            LEFT JOIN sys.foreign_key_columns fk ON fk.parent_object_id = c.object_id AND fk.parent_column_id = c.column_id
        WHERE t.name = @TableName
        ORDER BY c.column_id";

    public const string InsertColumnMetadata = @"
        IF NOT EXISTS (
            SELECT 1 FROM ColumnsMetadata 
            WHERE TableId = @TableId AND ColumnName = @ColumnName
        )
        INSERT INTO ColumnsMetadata (
            TableId, ColumnName, DataType, MaxLength, Precision, Scale,
            IsNullable, IsPrimaryKey, IsForeignKey, ColumnOrder
        )
        VALUES (
            @TableId, @ColumnName, @DataType, @MaxLength, @Precision, @Scale,
            @IsNullable, @IsPrimaryKey, @IsForeignKey, @ColumnOrder
        )";

    // Stored Procedure sync - Target database queries
    public const string GetTargetStoredProcedures = @"
        SELECT
            p.name AS ProcedureName,
            SCHEMA_NAME(p.schema_id) AS SchemaName,
            OBJECT_DEFINITION(p.object_id) AS Definition
        FROM sys.procedures p
        WHERE p.type = 'P'
        ORDER BY p.name";

    public const string InsertSpMetadata = @"
        IF NOT EXISTS (
            SELECT 1 FROM SpMetadata
            WHERE ProjectId = @ProjectId AND ProcedureName = @ProcedureName AND ClientId = @ClientId
        )
        INSERT INTO SpMetadata (
            ProjectId, ClientId, SchemaName, ProcedureName, Definition, CreatedBy, CreatedAt
        )
        VALUES (
            @ProjectId, @ClientId, @SchemaName, @ProcedureName, @Definition, @UserId, GETUTCDATE()
        )";

    // Foreign key relationships - Target database queries
    public const string GetForeignKeys = @"
        SELECT 
            fk.name AS ForeignKeyName,
            OBJECT_NAME(fk.parent_object_id) AS TableName,
            COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ColumnName,
            OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
            COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ReferencedColumn
        FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
        INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
        WHERE t.name = @TableName";

    public const string GetForeignKeysForTables = @"
        SELECT 
            fk.name AS ForeignKeyName,
            OBJECT_NAME(fk.parent_object_id) AS TableName,
            COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ColumnName,
            OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
            COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ReferencedColumn,
            fk.delete_referential_action_desc AS OnDeleteAction,
            fk.update_referential_action_desc AS OnUpdateAction
        FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
        INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
        WHERE t.name IN @TableNames";

    public const string InsertForeignKeyMetadata = @"
        IF NOT EXISTS (
            SELECT 1 FROM ForeignKeyMetadata 
            WHERE TableId = @TableId AND ColumnId = @ColumnId 
                AND ReferencedTableId = @ReferencedTableId 
                AND ReferencedColumnId = @ReferencedColumnId
        )
        INSERT INTO ForeignKeyMetadata (
            TableId, ColumnId, ReferencedTableId, ReferencedColumnId, OnDeleteAction, OnUpdateAction
        )
        VALUES (
            @TableId, @ColumnId, @ReferencedTableId, @ReferencedColumnId, @OnDeleteAction, @OnUpdateAction
        )";

    public const string InsertForeignKeyMetadataByNames = @"
        -- Resolve IDs from names and project
        DECLARE @ParentTableId INT = (
            SELECT TableId FROM TablesMetadata 
            WHERE ProjectId = @ProjectId AND TableName = @TableName
        );
        DECLARE @RefTableId INT = (
            SELECT TableId FROM TablesMetadata 
            WHERE ProjectId = @ProjectId AND TableName = @ReferencedTable
        );
        DECLARE @ParentColumnId INT = (
            SELECT ColumnId FROM ColumnsMetadata 
            WHERE TableId = @ParentTableId AND ColumnName = @ColumnName
        );
        DECLARE @RefColumnId INT = (
            SELECT ColumnId FROM ColumnsMetadata 
            WHERE TableId = @RefTableId AND ColumnName = @ReferencedColumn
        );

        IF @ParentTableId IS NOT NULL AND @RefTableId IS NOT NULL 
           AND @ParentColumnId IS NOT NULL AND @RefColumnId IS NOT NULL
        BEGIN
            IF NOT EXISTS (
                SELECT 1 
                FROM ForeignKeyMetadata 
                WHERE TableId = @ParentTableId 
                  AND ColumnId = @ParentColumnId 
                  AND ReferencedTableId = @RefTableId 
                  AND ReferencedColumnId = @RefColumnId
            )
            BEGIN
                INSERT INTO ForeignKeyMetadata (
                    TableId, ColumnId, ReferencedTableId, ReferencedColumnId, OnDeleteAction, OnUpdateAction
                )
                VALUES (
                    @ParentTableId, @ParentColumnId, @RefTableId, @RefColumnId, @OnDeleteAction, @OnUpdateAction
                );
            END
        END";

    // Schema reading - Target database queries
    public const string GetTableSchema = @"
        SELECT 
            c.TABLE_SCHEMA AS SchemaName,
            c.TABLE_NAME AS TableName,
            c.COLUMN_NAME AS ColumnName,
            c.DATA_TYPE AS DataType,
            c.CHARACTER_MAXIMUM_LENGTH AS MaxLength,
            CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS IsNullable,
            CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey,
            CASE WHEN COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') = 1 
                 THEN 1 ELSE 0 END AS IsIdentity,
            CASE WHEN fk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IsForeignKey,
            c.COLUMN_DEFAULT AS DefaultValue
        FROM 
            INFORMATION_SCHEMA.COLUMNS c
        LEFT JOIN 
            INFORMATION_SCHEMA.KEY_COLUMN_USAGE pk 
            ON c.TABLE_NAME = pk.TABLE_NAME 
            AND c.COLUMN_NAME = pk.COLUMN_NAME
            AND pk.CONSTRAINT_NAME LIKE 'PK_%'
        LEFT JOIN
            INFORMATION_SCHEMA.KEY_COLUMN_USAGE fk
            ON c.TABLE_NAME = fk.TABLE_NAME
            AND c.COLUMN_NAME = fk.COLUMN_NAME
            AND fk.CONSTRAINT_NAME LIKE 'FK_%'
        WHERE 
            c.TABLE_NAME = @TableName
        ORDER BY 
            c.ORDINAL_POSITION";

    public const string GetAllTables = @"
        SELECT t.name AS TableName
        FROM sys.tables t
        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
        ORDER BY s.name, t.name";

    // Stored metadata queries - ActoEngine database queries
    // Lightweight queries for list endpoints (minimal bandwidth)
    public const string GetTablesListMinimal = @"
        SELECT TableId, TableName, SchemaName
        FROM TablesMetadata
        WHERE ProjectId = @ProjectId
        ORDER BY TableName";

    public const string GetStoredProceduresListMinimal = @"
        SELECT SpId, ProcedureName, SchemaName
        FROM SpMetadata
        WHERE ProjectId = @ProjectId
        ORDER BY ProcedureName";

    // Full metadata queries (for detail views)
    public const string GetStoredTables = @"
        SELECT TableId, ProjectId, TableName, SchemaName, Description, CreatedAt
        FROM TablesMetadata
        WHERE ProjectId = @ProjectId
        ORDER BY TableName";

    public const string GetStoredColumns = @"
        SELECT ColumnId, TableId, ColumnName, DataType, MaxLength,
               Precision, Scale, IsNullable, IsPrimaryKey, IsForeignKey,
               DefaultValue, Description, ColumnOrder
        FROM ColumnsMetadata
        WHERE TableId = @TableId
        ORDER BY ColumnOrder";

    public const string GetStoredStoredProcedures = @"
        SELECT SpId, ProjectId, ClientId, ProcedureName, Definition,
               Description, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM SpMetadata
        WHERE ProjectId = @ProjectId
        ORDER BY ProcedureName";

    public const string GetTableById = @"
        SELECT TableId, ProjectId, TableName, SchemaName, Description, CreatedAt 
        FROM TablesMetadata 
        WHERE TableId = @TableId";

    public const string GetColumnById = @"
        SELECT ColumnId, TableId, ColumnName, DataType, MaxLength, 
               Precision, Scale, IsNullable, IsPrimaryKey, IsForeignKey, 
               DefaultValue, Description, ColumnOrder
        FROM ColumnsMetadata 
        WHERE ColumnId = @ColumnId";

    public const string GetSpById = @"
        SELECT SpId, ProjectId, ClientId, ProcedureName, Definition, 
               Description, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM SpMetadata 
        WHERE SpId = @SpId";

    public const string GetStoredTableByName = @"
        SELECT TableId, TableName, SchemaName
        FROM TablesMetadata
        WHERE ProjectId = @ProjectId AND TableName = @TableName";

    public const string GetStoredTableColumns = @"
        SELECT 
            c.ColumnName, 
            c.DataType, 
            c.MaxLength,
            c.Precision,
            c.Scale,
            c.IsNullable, 
            c.IsPrimaryKey, 
            c.IsForeignKey, 
            c.DefaultValue,
            -- Foreign Key Information
            rt.TableName AS ReferencedTable,
            rc.ColumnName AS ReferencedColumn,
            fk.OnDeleteAction,
            fk.OnUpdateAction
        FROM ColumnsMetadata c
        LEFT JOIN ForeignKeyMetadata fk ON c.ColumnId = fk.ColumnId
        LEFT JOIN TablesMetadata rt ON fk.ReferencedTableId = rt.TableId
        LEFT JOIN ColumnsMetadata rc ON fk.ReferencedColumnId = rc.ColumnId
        WHERE c.TableId = @TableId
        ORDER BY c.ColumnOrder";
}