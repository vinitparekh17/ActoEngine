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
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DbName SYSNAME;
    DECLARE @Sql NVARCHAR(MAX);

    SELECT @DbName = DatabaseName
    FROM dbo.Projects
    WHERE ProjectId = @ProjectId;

    IF @DbName IS NULL
    BEGIN
        THROW 50001, 'Project database name is not configured.', 1;
    END;

    SET @Sql = N'
    INSERT INTO dbo.TablesMetadata (ProjectId, TableName, SchemaName, CreatedAt)
    SELECT @ProjectId, t.name, s.name, GETUTCDATE()
    FROM ' + QUOTENAME(@DbName) + N'.sys.tables t
    INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.schemas s ON s.schema_id = t.schema_id
    WHERE NOT EXISTS (
        SELECT 1
        FROM dbo.TablesMetadata tm
        WHERE tm.ProjectId = @ProjectId
          AND tm.TableName = t.name
          AND tm.SchemaName = s.name
    );';

    EXEC sp_executesql @Sql, N'@ProjectId INT', @ProjectId;

    SET @Sql = N'
    INSERT INTO dbo.ColumnsMetadata
        (TableId, ColumnName, DataType, MaxLength, Precision, Scale, IsNullable, IsPrimaryKey, IsForeignKey, IsIdentity, DefaultValue, ColumnOrder)
    SELECT
        tm.TableId,
        c.name,
        typ.name,
        c.max_length,
        c.precision,
        c.scale,
        c.is_nullable,
        CASE WHEN pk.column_id IS NULL THEN 0 ELSE 1 END,
        CASE WHEN fkc.parent_column_id IS NULL THEN 0 ELSE 1 END,
        c.is_identity,
        dc.definition,
        c.column_id
    FROM ' + QUOTENAME(@DbName) + N'.sys.columns c
    INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.tables t ON t.object_id = c.object_id
    INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.schemas s ON s.schema_id = t.schema_id
    INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.types typ ON typ.user_type_id = c.user_type_id
    INNER JOIN dbo.TablesMetadata tm
        ON tm.ProjectId = @ProjectId
       AND tm.TableName = t.name
       AND tm.SchemaName = s.name
    LEFT JOIN ' + QUOTENAME(@DbName) + N'.sys.default_constraints dc
        ON dc.parent_object_id = c.object_id
       AND dc.parent_column_id = c.column_id
    LEFT JOIN (
        SELECT ic.object_id, ic.column_id
        FROM ' + QUOTENAME(@DbName) + N'.sys.indexes i
        INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.index_columns ic
            ON ic.object_id = i.object_id
           AND ic.index_id = i.index_id
        WHERE i.is_primary_key = 1
          AND ic.is_included_column = 0
    ) pk ON pk.object_id = c.object_id AND pk.column_id = c.column_id
    LEFT JOIN ' + QUOTENAME(@DbName) + N'.sys.foreign_key_columns fkc
        ON fkc.parent_object_id = c.object_id
       AND fkc.parent_column_id = c.column_id
    WHERE NOT EXISTS (
        SELECT 1
        FROM dbo.ColumnsMetadata cm
        WHERE cm.TableId = tm.TableId
          AND cm.ColumnName = c.name
    );';

    EXEC sp_executesql @Sql, N'@ProjectId INT', @ProjectId;

    SET @Sql = N'
    INSERT INTO dbo.IndexMetadata (TableId, IndexName, IsUnique, IsPrimaryKey)
    SELECT DISTINCT
        tm.TableId,
        i.name,
        i.is_unique,
        i.is_primary_key
    FROM ' + QUOTENAME(@DbName) + N'.sys.indexes i
    INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.tables t ON t.object_id = i.object_id
    INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.schemas s ON s.schema_id = t.schema_id
    INNER JOIN dbo.TablesMetadata tm
        ON tm.ProjectId = @ProjectId
       AND tm.TableName = t.name
       AND tm.SchemaName = s.name
    WHERE i.name IS NOT NULL
      AND i.is_hypothetical = 0
      AND i.type_desc <> ''HEAP''
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.IndexMetadata im
          WHERE im.TableId = tm.TableId
            AND im.IndexName = i.name
      );';

    EXEC sp_executesql @Sql, N'@ProjectId INT', @ProjectId;

    SET @Sql = N'
    INSERT INTO dbo.IndexColumnsMetadata (IndexId, ColumnId, ColumnOrder)
    SELECT
        im.IndexId,
        cm.ColumnId,
        ic.key_ordinal
    FROM ' + QUOTENAME(@DbName) + N'.sys.indexes i
    INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.tables t ON t.object_id = i.object_id
    INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.schemas s ON s.schema_id = t.schema_id
    INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.index_columns ic
        ON ic.object_id = i.object_id
       AND ic.index_id = i.index_id
       AND ic.is_included_column = 0
    INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.columns c
        ON c.object_id = ic.object_id
       AND c.column_id = ic.column_id
    INNER JOIN dbo.TablesMetadata tm
        ON tm.ProjectId = @ProjectId
       AND tm.TableName = t.name
       AND tm.SchemaName = s.name
    INNER JOIN dbo.IndexMetadata im
        ON im.TableId = tm.TableId
       AND im.IndexName = i.name
    INNER JOIN dbo.ColumnsMetadata cm
        ON cm.TableId = tm.TableId
       AND cm.ColumnName = c.name
    WHERE NOT EXISTS (
        SELECT 1
        FROM dbo.IndexColumnsMetadata icm
        WHERE icm.IndexId = im.IndexId
          AND icm.ColumnId = cm.ColumnId
    );';

    EXEC sp_executesql @Sql, N'@ProjectId INT', @ProjectId;

    SET @Sql = N'
    INSERT INTO dbo.ForeignKeyMetadata
        (TableId, ColumnId, ReferencedTableId, ReferencedColumnId, ForeignKeyName, OnDeleteAction, OnUpdateAction)
    SELECT
        tm.TableId,
        cm.ColumnId,
        rtm.TableId,
        rcm.ColumnId,
        fk.name,
        fk.delete_referential_action_desc,
        fk.update_referential_action_desc
    FROM ' + QUOTENAME(@DbName) + N'.sys.foreign_key_columns fkc
    INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.foreign_keys fk ON fk.object_id = fkc.constraint_object_id
    INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.tables t ON t.object_id = fkc.parent_object_id
    INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.schemas s ON s.schema_id = t.schema_id
    INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.columns c
        ON c.object_id = fkc.parent_object_id
       AND c.column_id = fkc.parent_column_id
    INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.tables rt ON rt.object_id = fkc.referenced_object_id
    INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.schemas rs ON rs.schema_id = rt.schema_id
    INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.columns rc
        ON rc.object_id = fkc.referenced_object_id
       AND rc.column_id = fkc.referenced_column_id
    INNER JOIN dbo.TablesMetadata tm
        ON tm.ProjectId = @ProjectId
       AND tm.TableName = t.name
       AND tm.SchemaName = s.name
    INNER JOIN dbo.ColumnsMetadata cm
        ON cm.TableId = tm.TableId
       AND cm.ColumnName = c.name
    INNER JOIN dbo.TablesMetadata rtm
        ON rtm.ProjectId = @ProjectId
       AND rtm.TableName = rt.name
       AND rtm.SchemaName = rs.name
    INNER JOIN dbo.ColumnsMetadata rcm
        ON rcm.TableId = rtm.TableId
       AND rcm.ColumnName = rc.name
    WHERE NOT EXISTS (
        SELECT 1
        FROM dbo.ForeignKeyMetadata existingFk
        WHERE existingFk.TableId = tm.TableId
          AND existingFk.ColumnId = cm.ColumnId
          AND existingFk.ReferencedTableId = rtm.TableId
          AND existingFk.ReferencedColumnId = rcm.ColumnId
    );';

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
    INSERT INTO dbo.SpMetadata
        (ProjectId, ClientId, ProcedureName, SchemaName, Definition, CreatedBy, CreatedAt)
    SELECT
        @ProjectId,
        @DefaultClientId,
        p.name,
        s.name,
        OBJECT_DEFINITION(p.object_id),
        @UserId,
        GETUTCDATE()
    FROM ' + QUOTENAME(@DbName) + N'.sys.procedures p
    INNER JOIN ' + QUOTENAME(@DbName) + N'.sys.schemas s ON s.schema_id = p.schema_id
    WHERE OBJECT_DEFINITION(p.object_id) IS NOT NULL
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.SpMetadata sm
          WHERE sm.ProjectId = @ProjectId
            AND sm.ClientId = @DefaultClientId
            AND sm.ProcedureName = p.name
            AND sm.SchemaName = s.name
      );';

    EXEC sp_executesql
        @Sql,
        N'@ProjectId INT, @DefaultClientId INT, @UserId INT',
        @ProjectId,
        @DefaultClientId,
        @UserId;
END;
GO
