-- V023: Persist richer schema metadata for the patcher engine

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.ColumnsMetadata')
      AND name = N'IsIdentity'
)
BEGIN
    ALTER TABLE dbo.ColumnsMetadata
        ADD IsIdentity BIT NOT NULL CONSTRAINT DF_ColumnsMetadata_IsIdentity DEFAULT 0;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SyncSchemaMetadata
    @ProjectId INT,
    @UserId INT,
    @DatabaseName SYSNAME = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DbName SYSNAME = @DatabaseName;
    DECLARE @Sql NVARCHAR(MAX);

    IF @DbName IS NULL
    BEGIN
        SELECT @DbName = DatabaseName
        FROM dbo.Projects
        WHERE ProjectId = @ProjectId;
    END

    IF @DbName IS NULL
    BEGIN
        THROW 50001, 'Project database name is not configured.', 1;
    END;

    SET @Sql = N'
    MERGE dbo.TablesMetadata AS target
    USING (
        SELECT t.name AS TableName, s.name AS SchemaName
        FROM ' + QUOTENAME(@DbName) + N'.sys.tables t
        INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.schemas s ON s.schema_id = t.schema_id
    ) AS source
    ON (target.ProjectId = @ProjectId AND target.TableName = source.TableName AND target.SchemaName = source.SchemaName)
    WHEN MATCHED AND target.IsDeleted = 1 THEN
        UPDATE SET IsDeleted = 0
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (ProjectId, TableName, SchemaName, CreatedAt, IsDeleted)
        VALUES (@ProjectId, source.TableName, source.SchemaName, GETUTCDATE(), 0)
    WHEN NOT MATCHED BY SOURCE AND target.ProjectId = @ProjectId THEN
        UPDATE SET IsDeleted = 1;';

    EXEC sp_executesql @Sql, N'@ProjectId INT', @ProjectId;

    SET @Sql = N'
    MERGE dbo.ColumnsMetadata AS target
    USING (
        SELECT
            tm.TableId,
            c.name AS ColumnName,
            typ.name AS DataType,
            c.max_length AS MaxLength,
            c.precision AS Precision,
            c.scale AS Scale,
            c.is_nullable AS IsNullable,
            CASE WHEN pk.column_id IS NULL THEN 0 ELSE 1 END AS IsPrimaryKey,
            CASE WHEN fkc.parent_column_id IS NULL THEN 0 ELSE 1 END AS IsForeignKey,
            c.is_identity AS IsIdentity,
            dc.definition AS DefaultValue,
            c.column_id AS ColumnOrder
        FROM ' + QUOTENAME(@DbName) + N'.sys.columns c
        INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.tables t ON t.object_id = c.object_id
        INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.schemas s ON s.schema_id = t.schema_id
        INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.types typ ON typ.user_type_id = c.user_type_id
        INNER JOIN dbo.TablesMetadata tm ON tm.ProjectId = @ProjectId AND tm.TableName = t.name AND tm.SchemaName = s.name
        LEFT JOIN ' + QUOTENAME(@DbName) + N'.sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        LEFT JOIN (
            SELECT ic.object_id, ic.column_id
            FROM ' + QUOTENAME(@DbName) + N'.sys.indexes i
            INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            WHERE i.is_primary_key = 1 AND ic.is_included_column = 0
        ) pk ON pk.object_id = c.object_id AND pk.column_id = c.column_id
        LEFT JOIN ' + QUOTENAME(@DbName) + N'.sys.foreign_key_columns fkc ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
    ) AS source
    ON (target.TableId = source.TableId AND target.ColumnName = source.ColumnName)
    WHEN MATCHED THEN
        UPDATE SET
            DataType = source.DataType,
            MaxLength = source.MaxLength,
            Precision = source.Precision,
            Scale = source.Scale,
            IsNullable = source.IsNullable,
            IsPrimaryKey = source.IsPrimaryKey,
            IsForeignKey = source.IsForeignKey,
            IsIdentity = source.IsIdentity,
            DefaultValue = source.DefaultValue,
            ColumnOrder = source.ColumnOrder
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (TableId, ColumnName, DataType, MaxLength, Precision, Scale, IsNullable, IsPrimaryKey, IsForeignKey, IsIdentity, DefaultValue, ColumnOrder)
        VALUES (source.TableId, source.ColumnName, source.DataType, source.MaxLength, source.Precision, source.Scale, source.IsNullable, source.IsPrimaryKey, source.IsForeignKey, source.IsIdentity, source.DefaultValue, source.ColumnOrder)
    WHEN NOT MATCHED BY SOURCE AND target.TableId IN (SELECT TableId FROM dbo.TablesMetadata WHERE ProjectId = @ProjectId) THEN
        DELETE;';

    EXEC sp_executesql @Sql, N'@ProjectId INT', @ProjectId;

    SET @Sql = N'
    MERGE dbo.IndexMetadata AS target
    USING (
        SELECT DISTINCT
            tm.TableId,
            i.name AS IndexName,
            i.is_unique AS IsUnique,
            i.is_primary_key AS IsPrimaryKey
        FROM ' + QUOTENAME(@DbName) + N'.sys.indexes i
        INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.tables t ON t.object_id = i.object_id
        INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.schemas s ON s.schema_id = t.schema_id
        INNER JOIN dbo.TablesMetadata tm ON tm.ProjectId = @ProjectId AND tm.TableName = t.name AND tm.SchemaName = s.name
        WHERE i.name IS NOT NULL
          AND i.is_hypothetical = 0
          AND i.type_desc <> ''HEAP''
    ) AS source
    ON (target.TableId = source.TableId AND target.IndexName = source.IndexName)
    WHEN MATCHED THEN
        UPDATE SET
            IsUnique = source.IsUnique,
            IsPrimaryKey = source.IsPrimaryKey
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (TableId, IndexName, IsUnique, IsPrimaryKey)
        VALUES (source.TableId, source.IndexName, source.IsUnique, source.IsPrimaryKey)
    WHEN NOT MATCHED BY SOURCE AND target.TableId IN (SELECT TableId FROM dbo.TablesMetadata WHERE ProjectId = @ProjectId) THEN
        DELETE;';

    EXEC sp_executesql @Sql, N'@ProjectId INT', @ProjectId;

    SET @Sql = N'
    MERGE dbo.IndexColumnsMetadata AS target
    USING (
        SELECT
            im.IndexId,
            cm.ColumnId,
            ic.key_ordinal AS ColumnOrder
        FROM ' + QUOTENAME(@DbName) + N'.sys.indexes i
        INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.tables t ON t.object_id = i.object_id
        INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.schemas s ON s.schema_id = t.schema_id
        INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
        INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        INNER JOIN dbo.TablesMetadata tm ON tm.ProjectId = @ProjectId AND tm.TableName = t.name AND tm.SchemaName = s.name
        INNER JOIN dbo.IndexMetadata im ON im.TableId = tm.TableId AND im.IndexName = i.name
        INNER JOIN dbo.ColumnsMetadata cm ON cm.TableId = tm.TableId AND cm.ColumnName = c.name
    ) AS source
    ON (target.IndexId = source.IndexId AND target.ColumnId = source.ColumnId)
    WHEN MATCHED THEN
        UPDATE SET ColumnOrder = source.ColumnOrder
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (IndexId, ColumnId, ColumnOrder)
        VALUES (source.IndexId, source.ColumnId, source.ColumnOrder)
    WHEN NOT MATCHED BY SOURCE AND target.IndexId IN (
        SELECT im2.IndexId 
        FROM dbo.IndexMetadata im2 
        INNER JOIN dbo.TablesMetadata tm2 ON tm2.TableId = im2.TableId 
        WHERE tm2.ProjectId = @ProjectId
    ) THEN
        DELETE;';

    EXEC sp_executesql @Sql, N'@ProjectId INT', @ProjectId;

    SET @Sql = N'
    MERGE dbo.ForeignKeyMetadata AS target
    USING (
        SELECT
            tm.TableId,
            cm.ColumnId,
            rtm.TableId AS ReferencedTableId,
            rcm.ColumnId AS ReferencedColumnId,
            fk.name AS ForeignKeyName,
            fk.delete_referential_action_desc AS OnDeleteAction,
            fk.update_referential_action_desc AS OnUpdateAction
        FROM ' + QUOTENAME(@DbName) + N'.sys.foreign_key_columns fkc
        INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.foreign_keys fk ON fk.object_id = fkc.constraint_object_id
        INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.tables t ON t.object_id = fkc.parent_object_id
        INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.schemas s ON s.schema_id = t.schema_id
        INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
        INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.tables rt ON rt.object_id = fkc.referenced_object_id
        INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.schemas rs ON rs.schema_id = rt.schema_id
        INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
        INNER JOIN dbo.TablesMetadata tm ON tm.ProjectId = @ProjectId AND tm.TableName = t.name AND tm.SchemaName = s.name
        INNER JOIN dbo.ColumnsMetadata cm ON cm.TableId = tm.TableId AND cm.ColumnName = c.name
        INNER JOIN dbo.TablesMetadata rtm ON rtm.ProjectId = @ProjectId AND rtm.TableName = rt.name AND rtm.SchemaName = rs.name
        INNER JOIN dbo.ColumnsMetadata rcm ON rcm.TableId = rtm.TableId AND rcm.ColumnName = rc.name
    ) AS source
    ON (target.TableId = source.TableId AND target.ColumnId = source.ColumnId AND target.ReferencedTableId = source.ReferencedTableId AND target.ReferencedColumnId = source.ReferencedColumnId)
    WHEN MATCHED THEN
        UPDATE SET
            ForeignKeyName = source.ForeignKeyName,
            OnDeleteAction = source.OnDeleteAction,
            OnUpdateAction = source.OnUpdateAction
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (TableId, ColumnId, ReferencedTableId, ReferencedColumnId, ForeignKeyName, OnDeleteAction, OnUpdateAction)
        VALUES (source.TableId, source.ColumnId, source.ReferencedTableId, source.ReferencedColumnId, source.ForeignKeyName, source.OnDeleteAction, source.OnUpdateAction)
    WHEN NOT MATCHED BY SOURCE AND target.TableId IN (
        SELECT tm3.TableId 
        FROM dbo.TablesMetadata tm3 
        WHERE tm3.ProjectId = @ProjectId
    ) THEN
        DELETE;';

    EXEC sp_executesql @Sql, N'@ProjectId INT', @ProjectId;

    DECLARE @DefaultClientId INT;
    SELECT @DefaultClientId = ClientId
    FROM dbo.Clients
    WHERE ClientName = 'Default Client';

    IF @DefaultClientId IS NULL
    BEGIN
        INSERT INTO dbo.Clients (ClientName, IsActive, CreatedAt, CreatedBy)
        VALUES ('Default Client', 1, GETUTCDATE(), @UserId);

        SET @DefaultClientId = SCOPE_IDENTITY();
    END;

    SET @Sql = N'
    MERGE dbo.SpMetadata AS target
    USING (
        SELECT
            @ProjectId AS ProjectId,
            @DefaultClientId AS ClientId,
            p.name AS ProcedureName,
            s.name AS SchemaName,
            mod.definition AS Definition,
            @UserId AS CreatedBy,
            GETUTCDATE() AS CreatedAt
        FROM ' + QUOTENAME(@DbName) + N'.sys.procedures p
        INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.schemas s ON s.schema_id = p.schema_id
        INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.sql_modules mod ON mod.object_id = p.object_id
        WHERE mod.definition IS NOT NULL
    ) AS source
    ON (target.ProjectId = source.ProjectId AND target.ProcedureName = source.ProcedureName AND target.SchemaName = source.SchemaName AND target.ClientId = source.ClientId)
    WHEN MATCHED AND (COALESCE(target.Definition, '''') <> COALESCE(source.Definition, '''') OR target.IsDeleted = 1) THEN
        UPDATE SET
            Definition = source.Definition,
            IsDeleted = 0
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (ProjectId, ClientId, ProcedureName, SchemaName, Definition, CreatedBy, CreatedAt, IsDeleted)
        VALUES (source.ProjectId, source.ClientId, source.ProcedureName, source.SchemaName, source.Definition, source.CreatedBy, source.CreatedAt, 0)
    WHEN NOT MATCHED BY SOURCE AND target.ProjectId = @ProjectId AND target.ClientId = @DefaultClientId THEN
        UPDATE SET IsDeleted = 1;';

    EXEC sp_executesql
        @Sql,
        N'@ProjectId INT, @DefaultClientId INT, @UserId INT',
        @ProjectId,
        @DefaultClientId,
        @UserId;
END;
GO
