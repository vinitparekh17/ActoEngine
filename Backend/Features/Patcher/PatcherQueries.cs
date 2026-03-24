namespace ActoEngine.WebApi.Features.Patcher;

public static class PatcherQueries
{
    public const string GetPatchConfig = @"
        SELECT ProjectRootPath, ViewDirPath, ScriptDirPath, PatchDownloadPath
        FROM Projects
        WHERE ProjectId = @ProjectId";

    public const string UpdatePatchConfig = @"
        UPDATE Projects
        SET ProjectRootPath = @ProjectRootPath,
            ViewDirPath = @ViewDirPath,
            ScriptDirPath = @ScriptDirPath,
            PatchDownloadPath = @PatchDownloadPath
        WHERE ProjectId = @ProjectId";

    public const string GetLatestPatch = @"
        SELECT TOP 1 ph.PatchId, ph.ProjectId, ph.PageName, ph.DomainName, ph.SpNames, ph.PatchName,
                       ph.IsNewPage, ph.PatchFilePath, ph.PatchSignature, ph.GeneratedAt, ph.GeneratedBy, ph.Status
        FROM PatchHistory ph
        WHERE ph.ProjectId = @ProjectId
          AND EXISTS (
              SELECT 1 
              FROM PatchHistoryPages php 
              WHERE php.PatchId = ph.PatchId 
                AND php.DomainName = @DomainName
                AND php.PageName = @PageName
          )
        ORDER BY ph.GeneratedAt DESC, ph.PatchId DESC";

    public const string GetPatchHistory = @"
        SELECT PatchId, ProjectId, PageName, DomainName, SpNames, PatchName,
                 IsNewPage, PatchFilePath, PatchSignature, GeneratedAt, GeneratedBy, Status
        FROM PatchHistory
        WHERE ProjectId = @ProjectId
        ORDER BY GeneratedAt DESC, PatchId DESC";

    public const string GetPatchHistoryPaged = @"
        SELECT PatchId, ProjectId, PageName, DomainName, SpNames, PatchName,
                 IsNewPage, PatchFilePath, PatchSignature, GeneratedAt, GeneratedBy, Status
        FROM PatchHistory
        WHERE ProjectId = @ProjectId
        ORDER BY GeneratedAt DESC, PatchId DESC
        OFFSET @Offset ROWS
        FETCH NEXT @Limit ROWS ONLY";

    public const string GetPatchHistoryCount = @"
        SELECT COUNT(1)
        FROM PatchHistory
        WHERE ProjectId = @ProjectId";

    public const string GetPatchHistoryPagesByPatchIds = @"
        SELECT PatchId, DomainName, PageName, IsNewPage
        FROM PatchHistoryPages
        WHERE PatchId IN @PatchIds
        ORDER BY PatchId, DomainName, PageName";

    public const string InsertPatchHistory = @"
        INSERT INTO PatchHistory
            (ProjectId, PageName, DomainName, SpNames, PatchName, IsNewPage, PatchFilePath, PatchSignature, GeneratedAt, GeneratedBy, Status)
        VALUES
            (@ProjectId, @PageName, @DomainName, @SpNames, @PatchName, @IsNewPage, @PatchFilePath, @PatchSignature, GETUTCDATE(), @GeneratedBy, @Status);
        SELECT SCOPE_IDENTITY();";

    public const string InsertPatchHistoryPages = @"
        INSERT INTO PatchHistoryPages (PatchId, DomainName, PageName, IsNewPage)
        VALUES (@PatchId, @DomainName, @PageName, @IsNewPage)";

    public const string GetPatchById = @"
        SELECT ph.PatchId, ph.ProjectId, ph.PageName, ph.DomainName, ph.SpNames, ph.PatchName,
                 ph.IsNewPage, ph.PatchFilePath, ph.PatchSignature, ph.GeneratedAt, ph.GeneratedBy, ph.Status
        FROM PatchHistory ph
        JOIN ProjectMembers pm ON ph.ProjectId = pm.ProjectId
        WHERE ph.PatchId = @PatchId AND pm.UserId = @UserId";

    public const string GetSpOutboundDependencies = @"
        SELECT d.TargetId AS TableId, t.TableName, t.SchemaName
        FROM Dependencies d
        JOIN SpMetadata src ON d.SourceId = src.SpId AND src.ProjectId = d.ProjectId AND src.IsDeleted = 0
        JOIN TablesMetadata t ON d.TargetId = t.TableId AND t.ProjectId = d.ProjectId AND t.IsDeleted = 0
        WHERE d.ProjectId = @ProjectId
          AND d.SourceType = 'SP'
          AND d.SourceId = @SpId
          AND d.TargetType = 'TABLE'";

    public const string GetSpOutboundDependenciesBatch = @"
        SELECT d.SourceId AS SourceSpId, d.TargetId AS TableId, t.TableName, t.SchemaName
        FROM Dependencies d
        JOIN SpMetadata src ON d.SourceId = src.SpId AND src.ProjectId = d.ProjectId AND src.IsDeleted = 0
        JOIN TablesMetadata t ON d.TargetId = t.TableId AND t.ProjectId = d.ProjectId AND t.IsDeleted = 0
        WHERE d.ProjectId = @ProjectId
          AND d.SourceType = 'SP'
          AND d.SourceId IN @SpIds
          AND d.TargetType = 'TABLE'";

    public const string GetSpProcedureDependencies = @"
        SELECT d.TargetId AS SpId, s.ProcedureName, s.SchemaName
        FROM Dependencies d
        JOIN SpMetadata src ON d.SourceId = src.SpId AND src.ProjectId = d.ProjectId AND src.IsDeleted = 0
        JOIN SpMetadata s ON d.TargetId = s.SpId AND s.ProjectId = d.ProjectId AND s.IsDeleted = 0
        WHERE d.ProjectId = @ProjectId
          AND d.SourceType = 'SP'
          AND d.SourceId = @SpId
          AND d.TargetType = 'SP'";

    public const string GetSpProcedureDependenciesBatch = @"
        SELECT d.SourceId AS SourceSpId, d.TargetId AS SpId, s.ProcedureName, s.SchemaName
        FROM Dependencies d
        JOIN SpMetadata src ON d.SourceId = src.SpId AND src.ProjectId = d.ProjectId AND src.IsDeleted = 0
        JOIN SpMetadata s ON d.TargetId = s.SpId AND s.ProjectId = d.ProjectId AND s.IsDeleted = 0
        WHERE d.ProjectId = @ProjectId
          AND d.SourceType = 'SP'
          AND d.SourceId IN @SpIds
          AND d.TargetType = 'SP'";

    public const string GetSpColumnDependencies = @"
        SELECT
                d.TargetId AS ColumnId,
                c.ColumnName,
                t.TableId,
                t.TableName,
                t.SchemaName
        FROM Dependencies d
        JOIN SpMetadata src ON d.SourceId = src.SpId AND src.ProjectId = d.ProjectId AND src.IsDeleted = 0
        JOIN ColumnsMetadata c ON d.TargetId = c.ColumnId
        JOIN TablesMetadata t ON c.TableId = t.TableId AND t.ProjectId = d.ProjectId AND t.IsDeleted = 0
        WHERE d.ProjectId = @ProjectId
          AND d.SourceType = 'SP'
          AND d.SourceId = @SpId
          AND d.TargetType = 'COLUMN'";

    public const string GetSpColumnDependenciesBatch = @"
        SELECT
                d.SourceId AS SourceSpId,
                d.TargetId AS ColumnId,
                c.ColumnName,
                t.TableId,
                t.TableName,
                t.SchemaName
        FROM Dependencies d
        JOIN SpMetadata src ON d.SourceId = src.SpId AND src.ProjectId = d.ProjectId AND src.IsDeleted = 0
        JOIN ColumnsMetadata c ON d.TargetId = c.ColumnId
        JOIN TablesMetadata t ON c.TableId = t.TableId AND t.ProjectId = d.ProjectId AND t.IsDeleted = 0
        WHERE d.ProjectId = @ProjectId
          AND d.SourceType = 'SP'
          AND d.SourceId IN @SpIds
          AND d.TargetType = 'COLUMN'";

    public const string GetStoredProceduresByIds = @"
        SELECT SpId, ProjectId, ClientId, SchemaName, ProcedureName, Definition, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
        FROM SpMetadata
        WHERE SpId IN @SpIds
          AND IsDeleted = 0";

    public const string GetTablesByIds = @"
        SELECT TableId, ProjectId, TableName, SchemaName, CreatedAt
        FROM TablesMetadata
        WHERE TableId IN @TableIds
          AND IsDeleted = 0";

    public const string GetColumnsByTableIds = @"
        SELECT ColumnId, TableId, ColumnName, DataType, MaxLength,
               Precision, Scale, IsNullable, IsPrimaryKey, IsForeignKey, IsIdentity,
               DefaultValue, ColumnOrder
        FROM ColumnsMetadata
        WHERE TableId IN @TableIds
        ORDER BY TableId, ColumnOrder";

    public const string GetIndexesByTableIds = @"
        SELECT
            i.IndexId,
            i.TableId,
            i.IndexName,
            i.IsUnique,
            i.IsPrimaryKey,
            ic.ColumnId,
            c.ColumnName,
            ic.ColumnOrder,
            CAST(0 AS bit) AS IsIncludedColumn
        FROM IndexMetadata i
        LEFT JOIN IndexColumnsMetadata ic ON ic.IndexId = i.IndexId
        LEFT JOIN ColumnsMetadata c ON c.ColumnId = ic.ColumnId
        WHERE i.TableId IN @TableIds
        ORDER BY i.TableId, i.IndexName, ic.ColumnOrder, c.ColumnName;";

    public const string GetForeignKeysByTableIds = @"
        SELECT
            fk.ForeignKeyId,
            fk.TableId,
            fk.ColumnId,
            c.ColumnName,
            fk.ReferencedTableId,
            rt.TableName AS ReferencedTableName,
            rt.SchemaName AS ReferencedSchemaName,
            fk.ReferencedColumnId,
            rc.ColumnName AS ReferencedColumnName,
            fk.ForeignKeyName,
            fk.OnDeleteAction,
            fk.OnUpdateAction
        FROM ForeignKeyMetadata fk
        INNER JOIN ColumnsMetadata c ON c.ColumnId = fk.ColumnId
        INNER JOIN TablesMetadata rt ON rt.TableId = fk.ReferencedTableId
        INNER JOIN ColumnsMetadata rc ON rc.ColumnId = fk.ReferencedColumnId
        WHERE fk.TableId IN @TableIds
        ORDER BY fk.TableId, fk.ForeignKeyName, c.ColumnName;";

    public const string MenuPermissionScriptTemplate = @"-- =============================================
-- Menu & Permission Script (New Page)
-- Page: {SafePageName} | Domain: {SafeDomainName}
-- Generated by ActoEngine on {Timestamp}
-- =============================================

DECLARE @TableName SYSNAME = 'Common_MenuMas'
DECLARE @WhereClause NVARCHAR(MAX) = 'WHERE [Action] = ''{SafePageName}'''
DECLARE @Cols NVARCHAR(MAX), @ColsValues NVARCHAR(MAX), @SQL NVARCHAR(MAX)

-- Build column list (excludes IDENTITY columns)
SELECT @Cols = STUFF((
    SELECT ',' + QUOTENAME(COLUMN_NAME)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = @TableName
      AND COLUMNPROPERTY(OBJECT_ID(TABLE_SCHEMA + '.' + TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 0
    ORDER BY ORDINAL_POSITION
    FOR XML PATH(''), TYPE
).value('.', 'NVARCHAR(MAX)'),1,1,'')

-- Build value expressions
SELECT @ColsValues = STUFF((
    SELECT ' + '','' + ISNULL('''''''' + 
           REPLACE(CAST(' + QUOTENAME(COLUMN_NAME) + ' AS NVARCHAR(MAX)),'''''''','''''''''''') + 
           '''''''',''NULL'')'
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = @TableName
      AND COLUMNPROPERTY(OBJECT_ID(TABLE_SCHEMA + '.' + TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 0
    ORDER BY ORDINAL_POSITION
    FOR XML PATH(''), TYPE
).value('.', 'NVARCHAR(MAX)'),1,9,'')

-- Generate conditional INSERT with auto-permission
SET @SQL = '
SELECT ''IF NOT EXISTS (SELECT 1 FROM ' + @TableName + ' ' + 
    REPLACE(@WhereClause, '''', '''''') + ') ' + 
    'BEGIN ' + 
        'INSERT INTO ' + @TableName + '(' + @Cols + ') VALUES ('' + ' + @ColsValues + ' + ''); ' +
        'INSERT INTO Security_UserPermission 
            (UserGroupId, MenuId, IsView, IsAdd, IsUpdate, IsDelete, IsDownload, IsDisableIpLock) ' +
        'SELECT UserGroupId, SCOPE_IDENTITY(), 1, 1, 1, 1, 1, 1 ' + 
        'FROM Security_UserAccessGroups WHERE UserGroupName = ''''Administration''''; ' +
    'END''
FROM (SELECT 1 AS X) AS _Src

EXEC(@SQL)";
}
