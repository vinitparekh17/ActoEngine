USE ActoEngine
GO;
CREATE OR ALTER PROCEDURE SyncSchemaMetadata
    @ProjectId INT,
    @ConnectionString NVARCHAR(500),
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Sql NVARCHAR(MAX);
    DECLARE @DbName NVARCHAR(100);

    SELECT @DbName = DatabaseName
    FROM Projects
    WHERE ProjectId = @ProjectId;

    -- 1. Sync Tables
    SET @Sql = '
    INSERT INTO ActoEngine.dbo.TablesMetadata (ProjectId, TableName)
    SELECT @ProjectId, t.name
    FROM ' + QUOTENAME(@DbName) + '.sys.tables t
    WHERE NOT EXISTS (
        SELECT 1 FROM ActoEngine.dbo.TablesMetadata tm
        WHERE tm.ProjectId = @ProjectId AND tm.TableName = t.name
    )';

    EXEC sp_executesql @Sql, N'@ProjectId INT', @ProjectId;

    -- 2. Sync Columns
    SET @Sql = '
    INSERT INTO ActoEngine.dbo.ColumnsMetadata
        (TableId, ColumnName, DataType, IsNullable, IsPrimaryKey, IsForeignKey, ColumnOrder)
    SELECT
        tm.TableId,
        c.name,
        typ.name,
        c.is_nullable,
        CASE WHEN kc.column_id IS NOT NULL THEN 1 ELSE 0 END,
        CASE WHEN fkc.parent_column_id IS NOT NULL THEN 1 ELSE 0 END,
        c.column_id
    FROM ' + QUOTENAME(@DbName) + '.sys.columns c
    INNER JOIN ' + QUOTENAME(@DbName) + '.sys.tables t2 ON c.object_id = t2.object_id
    INNER JOIN ActoEngine.dbo.TablesMetadata tm
        ON tm.TableName = t2.name AND tm.ProjectId = @ProjectId
    INNER JOIN ' + QUOTENAME(@DbName) + '.sys.types typ
        ON c.user_type_id = typ.user_type_id
    LEFT JOIN (
        SELECT ic.object_id, ic.column_id
        FROM ' + QUOTENAME(@DbName) + '.sys.indexes i
        INNER JOIN ' + QUOTENAME(@DbName) + '.sys.index_columns ic
            ON i.object_id = ic.object_id AND i.index_id = ic.index_id
        WHERE i.is_primary_key = 1
    ) kc ON kc.object_id = c.object_id AND kc.column_id = c.column_id
    LEFT JOIN ' + QUOTENAME(@DbName) + '.sys.foreign_key_columns fkc
        ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
    WHERE NOT EXISTS (
        SELECT 1 FROM ActoEngine.dbo.ColumnsMetadata cm
        WHERE cm.TableId = tm.TableId AND cm.ColumnName = c.name
    )';

    EXEC sp_executesql @Sql, N'@ProjectId INT', @ProjectId;

    -- 3. Sync Foreign Keys
    SET @Sql = '
    INSERT INTO ActoEngine.dbo.ForeignKeyMetadata
        (TableId, ColumnId, ReferencedTableId, ReferencedColumnId, OnDeleteAction, OnUpdateAction)
    SELECT
        tm.TableId,
        cm.ColumnId,
        rtm.TableId,
        rcm.ColumnId,
        fk.delete_referential_action_desc,
        fk.update_referential_action_desc
    FROM ' + QUOTENAME(@DbName) + '.sys.foreign_key_columns fkc
    INNER JOIN ' + QUOTENAME(@DbName) + '.sys.foreign_keys fk
        ON fkc.constraint_object_id = fk.object_id
    INNER JOIN ' + QUOTENAME(@DbName) + '.sys.tables t
        ON fkc.parent_object_id = t.object_id
    INNER JOIN ' + QUOTENAME(@DbName) + '.sys.columns c
        ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
    INNER JOIN ' + QUOTENAME(@DbName) + '.sys.tables rt
        ON fkc.referenced_object_id = rt.object_id
    INNER JOIN ' + QUOTENAME(@DbName) + '.sys.columns rc
        ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
    INNER JOIN ActoEngine.dbo.TablesMetadata tm
        ON tm.TableName = t.name AND tm.ProjectId = @ProjectId
    INNER JOIN ActoEngine.dbo.ColumnsMetadata cm
        ON cm.TableId = tm.TableId AND cm.ColumnName = c.name
    INNER JOIN ActoEngine.dbo.TablesMetadata rtm
        ON rtm.TableName = rt.name AND rtm.ProjectId = @ProjectId
    INNER JOIN ActoEngine.dbo.ColumnsMetadata rcm
        ON rcm.TableId = rtm.TableId AND rcm.ColumnName = rc.name
    WHERE NOT EXISTS (
        SELECT 1 FROM ActoEngine.dbo.ForeignKeyMetadata fkm
        WHERE fkm.TableId = tm.TableId AND fkm.ColumnId = cm.ColumnId
    )';

    EXEC sp_executesql @Sql, N'@ProjectId INT', @ProjectId;

    -- 4. Update IsForeignKey flag in ColumnsMetadata
    UPDATE cm
    SET cm.IsForeignKey = 1
    FROM ActoEngine.dbo.ColumnsMetadata cm
    INNER JOIN ActoEngine.dbo.ForeignKeyMetadata fkm
        ON cm.ColumnId = fkm.ColumnId
    WHERE cm.IsForeignKey = 0;

    -- 5. Sync Stored Procedures
    SET @Sql = '
    INSERT INTO ActoEngine.dbo.SpMetadata
        (ProjectId, ClientId, ProcedureName, Definition, CreatedBy)
    SELECT
        @ProjectId,
        NULL,
        p.name,
        OBJECT_DEFINITION(p.object_id),
        @UserId
    FROM ' + QUOTENAME(@DbName) + '.sys.procedures p
    WHERE NOT EXISTS (
        SELECT 1 FROM ActoEngine.dbo.SpMetadata sm
        WHERE sm.ProjectId = @ProjectId AND sm.ProcedureName = p.name AND sm.ClientId IS NULL
    )';

    EXEC sp_executesql @Sql, N'@ProjectId INT, @UserId INT', @ProjectId, @UserId;
END;
