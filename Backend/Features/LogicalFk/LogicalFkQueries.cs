namespace ActoEngine.WebApi.Features.LogicalFk;

/// <summary>
/// SQL queries for LogicalForeignKeys operations
/// </summary>
public static class LogicalFkQueries
{
    public const string GetByProject = @"
        SELECT 
            lfk.LogicalFkId, lfk.ProjectId,
            lfk.SourceTableId, st.TableName AS SourceTableName,
            lfk.SourceColumnIds,
            lfk.TargetTableId, tt.TableName AS TargetTableName,
            lfk.TargetColumnIds,
            lfk.DiscoveryMethod, lfk.ConfidenceScore, lfk.Status,
            lfk.ConfirmedBy, lfk.ConfirmedAt, lfk.Notes, lfk.CreatedAt
        FROM LogicalForeignKeys lfk
        INNER JOIN TablesMetadata st ON lfk.SourceTableId = st.TableId
        INNER JOIN TablesMetadata tt ON lfk.TargetTableId = tt.TableId
        WHERE lfk.ProjectId = @ProjectId
        ORDER BY lfk.CreatedAt DESC;";

    public const string GetByProjectFiltered = @"
        SELECT 
            lfk.LogicalFkId, lfk.ProjectId,
            lfk.SourceTableId, st.TableName AS SourceTableName,
            lfk.SourceColumnIds,
            lfk.TargetTableId, tt.TableName AS TargetTableName,
            lfk.TargetColumnIds,
            lfk.DiscoveryMethod, lfk.ConfidenceScore, lfk.Status,
            lfk.ConfirmedBy, lfk.ConfirmedAt, lfk.Notes, lfk.CreatedAt
        FROM LogicalForeignKeys lfk
        INNER JOIN TablesMetadata st ON lfk.SourceTableId = st.TableId
        INNER JOIN TablesMetadata tt ON lfk.TargetTableId = tt.TableId
        WHERE lfk.ProjectId = @ProjectId AND lfk.Status = @Status
        ORDER BY lfk.ConfidenceScore DESC;";

    public const string GetById = @"
        SELECT 
            lfk.LogicalFkId, lfk.ProjectId,
            lfk.SourceTableId, st.TableName AS SourceTableName,
            lfk.SourceColumnIds,
            lfk.TargetTableId, tt.TableName AS TargetTableName,
            lfk.TargetColumnIds,
            lfk.DiscoveryMethod, lfk.ConfidenceScore, lfk.Status,
            lfk.ConfirmedBy, lfk.ConfirmedAt, lfk.Notes, lfk.CreatedAt
        FROM LogicalForeignKeys lfk
        INNER JOIN TablesMetadata st ON lfk.SourceTableId = st.TableId
        INNER JOIN TablesMetadata tt ON lfk.TargetTableId = tt.TableId
        WHERE lfk.LogicalFkId = @LogicalFkId;";

    public const string GetByTable = @"
        SELECT 
            lfk.LogicalFkId, lfk.ProjectId,
            lfk.SourceTableId, st.TableName AS SourceTableName,
            lfk.SourceColumnIds,
            lfk.TargetTableId, tt.TableName AS TargetTableName,
            lfk.TargetColumnIds,
            lfk.DiscoveryMethod, lfk.ConfidenceScore, lfk.Status,
            lfk.ConfirmedBy, lfk.ConfirmedAt, lfk.Notes, lfk.CreatedAt
        FROM LogicalForeignKeys lfk
        INNER JOIN TablesMetadata st ON lfk.SourceTableId = st.TableId
        INNER JOIN TablesMetadata tt ON lfk.TargetTableId = tt.TableId
        WHERE lfk.ProjectId = @ProjectId
          AND (lfk.SourceTableId = @TableId OR lfk.TargetTableId = @TableId)
        ORDER BY lfk.CreatedAt DESC;";

    public const string Insert = @"
        INSERT INTO LogicalForeignKeys
            (ProjectId, SourceTableId, SourceColumnIds, TargetTableId, TargetColumnIds,
             DiscoveryMethod, ConfidenceScore, Status, Notes, CreatedBy, ConfirmedBy, ConfirmedAt)
        OUTPUT INSERTED.LogicalFkId
        VALUES
            (@ProjectId, @SourceTableId, @SourceColumnIds, @TargetTableId, @TargetColumnIds,
             @DiscoveryMethod, @ConfidenceScore, @Status, @Notes, @CreatedBy, @ConfirmedBy, @ConfirmedAt);";

    public const string Confirm = @"
        UPDATE LogicalForeignKeys
        SET Status = 'CONFIRMED',
            ConfirmedBy = @UserId,
            ConfirmedAt = GETUTCDATE(),
            Notes = COALESCE(@Notes, Notes)
        WHERE LogicalFkId = @LogicalFkId
          AND ProjectId = @ProjectId;";

    public const string Reject = @"
        UPDATE LogicalForeignKeys
        SET Status = 'REJECTED',
            ConfirmedBy = @UserId,
            ConfirmedAt = GETUTCDATE(),
            Notes = COALESCE(@Notes, Notes)
        WHERE LogicalFkId = @LogicalFkId
          AND ProjectId = @ProjectId;";

    public const string Delete = @"
        DELETE FROM LogicalForeignKeys
        WHERE LogicalFkId = @LogicalFkId
          AND ProjectId = @ProjectId;";

    /// <summary>
    /// Check if a logical FK already exists for the same source→target column mapping
    /// </summary>
    public const string Exists = @"
        SELECT COUNT(1)
        FROM LogicalForeignKeys
        WHERE ProjectId = @ProjectId
          AND SourceTableId = @SourceTableId
          AND SourceColumnIds = @SourceColumnIds
          AND TargetTableId = @TargetTableId
          AND TargetColumnIds = @TargetColumnIds;";

    /// <summary>
    /// Also exclude mappings that already exist as physical FKs
    /// </summary>
    public const string ExistsAsPhysicalFk = @"
        SELECT COUNT(1)
        FROM ForeignKeyMetadata fk
        WHERE fk.TableId = @SourceTableId
          AND fk.ColumnId = @SourceColumnId
          AND fk.ReferencedTableId = @TargetTableId
          AND fk.ReferencedColumnId = @TargetColumnId;";

    /// <summary>
    /// Get all columns for a project (used by detection engine)
    /// </summary>
    public const string GetColumnsForDetection = @"
        SELECT 
            cm.ColumnId, cm.TableId, cm.ColumnName, cm.DataType,
            cm.IsPrimaryKey, cm.IsForeignKey,
            tm.TableName
        FROM ColumnsMetadata cm
        INNER JOIN TablesMetadata tm ON cm.TableId = tm.TableId
        WHERE tm.ProjectId = @ProjectId
        ORDER BY tm.TableName, cm.ColumnOrder;";

    public const string GetPhysicalFksByTable = @"
        SELECT 
            fk.ForeignKeyName,
            st.TableName AS SourceTableName,
            sc.ColumnName AS SourceColumnName,
            tt.TableName AS TargetTableName,
            tc.ColumnName AS TargetColumnName,
            fk.OnDeleteAction,
            fk.OnUpdateAction
        FROM ForeignKeyMetadata fk
        INNER JOIN TablesMetadata st ON fk.TableId = st.TableId
        INNER JOIN ColumnsMetadata sc ON fk.ColumnId = sc.ColumnId
        INNER JOIN TablesMetadata tt ON fk.ReferencedTableId = tt.TableId
        INNER JOIN ColumnsMetadata tc ON fk.ReferencedColumnId = tc.ColumnId
        WHERE st.ProjectId = @ProjectId
          AND (fk.TableId = @TableId OR fk.ReferencedTableId = @TableId)
        ORDER BY st.TableName, sc.ColumnOrder;";

    public const string GetColumnNamesQuery = "SELECT ColumnId, ColumnName FROM ColumnsMetadata WHERE ColumnId IN @ColumnIds";
}