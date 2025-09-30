namespace ActoEngine.WebApi.Sql.Queries;

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

    // Table sync
    public const string GetTargetTables = @"
        SELECT name 
        FROM sys.tables 
        WHERE type = 'U' 
        ORDER BY name";

    public const string InsertTableMetadata = @"
        IF NOT EXISTS (
            SELECT 1 FROM TablesMetadata 
            WHERE ProjectId = @ProjectId AND TableName = @TableName
        )
        INSERT INTO TablesMetadata (ProjectId, TableName, CreatedAt)
        VALUES (@ProjectId, @TableName, GETUTCDATE());
        
        SELECT TableId FROM TablesMetadata 
        WHERE ProjectId = @ProjectId AND TableName = @TableName";

    public const string GetTableId = @"
        SELECT TableId 
        FROM TablesMetadata 
        WHERE ProjectId = @ProjectId AND TableName = @TableName";

    // Column sync
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
            LEFT JOIN sys.key_constraints kc ON kc.parent_object_id = c.object_id AND kc.unique_index_id = ic.index_id AND kc.type = 'PK' -- Join to key constraints
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

    // Stored Procedure sync
    public const string GetTargetStoredProcedures = @"
        SELECT 
            p.name AS ProcedureName,
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
            ProjectId, ClientId, ProcedureName, Definition, CreatedBy, CreatedAt
        )
        VALUES (
            @ProjectId, @ClientId, @ProcedureName, @Definition, @UserId, GETUTCDATE()
        )";

    // Foreign key relationships
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

    public const string InsertForeignKeyMetadata = @"
        IF NOT EXISTS (
            SELECT 1 FROM ForeignKeyMetadata 
            WHERE TableId = @TableId AND ForeignKeyName = @ForeignKeyName
        )
        INSERT INTO ForeignKeyMetadata (
            TableId, ForeignKeyName, ColumnName, ReferencedTable, ReferencedColumn, CreatedAt
        )
        VALUES (
            @TableId, @ForeignKeyName, @ColumnName, @ReferencedTable, @ReferencedColumn, GETUTCDATE()
        )";
}