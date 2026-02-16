
namespace ActoEngine.WebApi.Features.ErDiagram;

internal static class ErDiagramQueries
{
    public const string GetTableInfo = "SELECT TableId, TableName, SchemaName FROM TablesMetadata WHERE TableId = @TableId AND ProjectId = @ProjectId";

    /// <summary>
    /// Get all physical FK edges for a project.
    /// Returns source table+column → target table+column via ForeignKeyMetadata join.
    /// </summary>
    public const string GetPhysicalFks = @"
        SELECT
            fk.TableId AS SourceTableId,
            fk.ColumnId AS SourceColumnId,
            sc.ColumnName AS SourceColumnName,
            fk.ReferencedTableId AS TargetTableId,
            fk.ReferencedColumnId AS TargetColumnId,
            tc.ColumnName AS TargetColumnName
        FROM ForeignKeyMetadata fk
        INNER JOIN TablesMetadata st ON fk.TableId = st.TableId
        INNER JOIN ColumnsMetadata sc ON fk.ColumnId = sc.ColumnId
        INNER JOIN ColumnsMetadata tc ON fk.ReferencedColumnId = tc.ColumnId
        WHERE st.ProjectId = @ProjectId";

    /// <summary>
    /// Get logical FK edges for a project (only SUGGESTED + CONFIRMED, single-column only for now).
    /// JSON arrays are parsed here — first element of each array.
    /// </summary>
    public const string GetLogicalFks = @"
        SELECT
            lfk.LogicalFkId AS LogicalForeignKeyId,
            lfk.SourceTableId,
            TRY_CAST(JSON_VALUE(lfk.SourceColumnIds, '$[0]') AS INT) AS SourceColumnId,
            sc.ColumnName AS SourceColumnName,
            lfk.TargetTableId,
            TRY_CAST(JSON_VALUE(lfk.TargetColumnIds, '$[0]') AS INT) AS TargetColumnId,
            tc.ColumnName AS TargetColumnName,
            lfk.Status,
            lfk.ConfidenceScore,
            lfk.DiscoveryMethod,
            lfk.ConfirmedAt,
            lfk.ConfirmedBy,
            lfk.CreatedAt
        FROM LogicalForeignKeys lfk
        INNER JOIN ColumnsMetadata sc ON sc.ColumnId = TRY_CAST(JSON_VALUE(lfk.SourceColumnIds, '$[0]') AS INT)
        INNER JOIN ColumnsMetadata tc ON tc.ColumnId = TRY_CAST(JSON_VALUE(lfk.TargetColumnIds, '$[0]') AS INT)
        WHERE lfk.ProjectId = @ProjectId
          AND lfk.Status IN ('SUGGESTED', 'CONFIRMED')
          AND JSON_VALUE(lfk.SourceColumnIds, '$[0]') IS NOT NULL
          AND JSON_VALUE(lfk.TargetColumnIds, '$[0]') IS NOT NULL";

    public const string GetTablesByIds = @"
        SELECT TableId, TableName, SchemaName
        FROM TablesMetadata
        WHERE TableId IN @TableIds";

    public const string GetColumnsByTableIds = @"
        SELECT TableId, ColumnId, ColumnName, DataType, IsPrimaryKey, IsForeignKey, IsNullable
        FROM ColumnsMetadata
        WHERE TableId IN @TableIds
        ORDER BY ColumnOrder";
}
