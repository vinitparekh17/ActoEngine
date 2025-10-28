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

    // Table sync - Target database queries
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
    public const string GetStoredTables = @"
        SELECT TableId, ProjectId, TableName, Description, CreatedAt 
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

    public const string GetStoredTableByName = @"
        SELECT TableId, TableName 
        FROM TablesMetadata 
        WHERE ProjectId = @ProjectId AND TableName = @TableName";

    public const string GetStoredTableColumns = @"
        SELECT ColumnName, DataType, MaxLength, IsNullable, IsPrimaryKey, IsForeignKey, DefaultValue
        FROM ColumnsMetadata 
        WHERE TableId = @TableId
        ORDER BY ColumnOrder";

    public const string VerifyTableExists = @"
        SELECT COUNT(*)
        FROM sys.tables t
        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
        WHERE s.name = @SchemaName AND t.name = @TableName";

    public const string GetQuotedTableName = @"
        SELECT QUOTENAME(@SchemaName) + '.' + QUOTENAME(@TableName)";
}