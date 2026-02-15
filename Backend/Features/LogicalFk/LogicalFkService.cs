using ActoEngine.WebApi.Features.ImpactAnalysis;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace ActoEngine.WebApi.Features.LogicalFk;

/// <summary>
/// Service contract for Logical FK operations
/// </summary>
public interface ILogicalFkService
{
    Task<List<LogicalFkDto>> GetByProjectAsync(int projectId, string? statusFilter = null, CancellationToken cancellationToken = default);
    Task<LogicalFkDto?> GetByIdAsync(int logicalFkId, CancellationToken cancellationToken = default);
    Task<List<LogicalFkDto>> GetByTableAsync(int projectId, int tableId, CancellationToken cancellationToken = default);
    Task<LogicalFkDto> CreateManualAsync(int projectId, CreateLogicalFkRequest request, int userId, CancellationToken cancellationToken = default);
    Task<LogicalFkDto> ConfirmAsync(int logicalFkId, int userId, string? notes = null, CancellationToken cancellationToken = default);
    Task<LogicalFkDto> RejectAsync(int logicalFkId, int userId, string? notes = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(int logicalFkId, CancellationToken cancellationToken = default);
    Task<List<LogicalFkCandidate>> DetectCandidatesAsync(int projectId, CancellationToken cancellationToken = default);
    Task<List<PhysicalFkDto>> GetPhysicalFksByTableAsync(int projectId, int tableId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for Logical FK business logic including name convention detection
/// </summary>
public partial class LogicalFkService(
    ILogicalFkRepository logicalFkRepository,
    IDependencyRepository dependencyRepository,
    ILogger<LogicalFkService> logger) : ILogicalFkService
{
    // Name convention patterns: orders.customer_id → customers.id
    [GeneratedRegex(@"^(.+?)(?:_?[Ii][Dd])$")]
    private static partial Regex FkColumnPattern();

    public async Task<List<LogicalFkDto>> GetByProjectAsync(int projectId, string? statusFilter = null, CancellationToken cancellationToken = default)
        => await logicalFkRepository.GetByProjectAsync(projectId, statusFilter, cancellationToken);

    public async Task<LogicalFkDto?> GetByIdAsync(int logicalFkId, CancellationToken cancellationToken = default)
        => await logicalFkRepository.GetByIdAsync(logicalFkId, cancellationToken);

    public async Task<List<LogicalFkDto>> GetByTableAsync(int projectId, int tableId, CancellationToken cancellationToken = default)
        => await logicalFkRepository.GetByTableAsync(projectId, tableId, cancellationToken);

    public async Task<LogicalFkDto> CreateManualAsync(int projectId, CreateLogicalFkRequest request, int userId, CancellationToken cancellationToken = default)
    {
        // Validate column counts match
        if (request.SourceColumnIds.Count != request.TargetColumnIds.Count)
        {
            throw new ArgumentException("Source and target column counts must match for composite foreign keys.");
        }

        // Check if already exists
        var exists = await logicalFkRepository.ExistsAsync(
            projectId, request.SourceTableId, request.SourceColumnIds,
            request.TargetTableId, request.TargetColumnIds, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("A logical foreign key with this mapping already exists.");
        }

        var entity = new LogicalForeignKey
        {
            ProjectId = projectId,
            SourceTableId = request.SourceTableId,
            SourceColumnIds = JsonSerializer.Serialize(request.SourceColumnIds),
            TargetTableId = request.TargetTableId,
            TargetColumnIds = JsonSerializer.Serialize(request.TargetColumnIds),
            DiscoveryMethod = "MANUAL",
            ConfidenceScore = 1.0m,
            Status = "CONFIRMED",
            Notes = request.Notes,
            CreatedBy = userId,
            ConfirmedBy = userId,
            ConfirmedAt = DateTime.UtcNow
        };

        var id = await logicalFkRepository.CreateAsync(entity, cancellationToken);

        // Manual FKs are auto-confirmed → feed into Dependencies immediately
        await FeedIntoDependenciesAsync(projectId, request.SourceTableId, request.TargetTableId, 1.0m, cancellationToken);

        return await logicalFkRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve created logical FK.");
    }

    public async Task<LogicalFkDto> ConfirmAsync(int logicalFkId, int userId, string? notes = null, CancellationToken cancellationToken = default)
    {
        var existing = await logicalFkRepository.GetByIdAsync(logicalFkId, cancellationToken)
            ?? throw new KeyNotFoundException($"Logical FK {logicalFkId} not found.");

        await logicalFkRepository.ConfirmAsync(logicalFkId, userId, notes, cancellationToken);

        // Feed confirmed FK into Dependencies table
        await FeedIntoDependenciesAsync(
            existing.ProjectId, existing.SourceTableId, existing.TargetTableId,
            existing.ConfidenceScore, cancellationToken);

        return await logicalFkRepository.GetByIdAsync(logicalFkId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve confirmed logical FK.");
    }

    public async Task<LogicalFkDto> RejectAsync(int logicalFkId, int userId, string? notes = null, CancellationToken cancellationToken = default)
    {
        _ = await logicalFkRepository.GetByIdAsync(logicalFkId, cancellationToken)
            ?? throw new KeyNotFoundException($"Logical FK {logicalFkId} not found.");

        await logicalFkRepository.RejectAsync(logicalFkId, userId, notes, cancellationToken);

        return await logicalFkRepository.GetByIdAsync(logicalFkId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve rejected logical FK.");
    }

    public async Task DeleteAsync(int logicalFkId, CancellationToken cancellationToken = default)
    {
        await logicalFkRepository.DeleteAsync(logicalFkId, cancellationToken);
    }

    /// <summary>
    /// Detect logical FK candidates using name convention + data type matching.
    /// Matches columns named "&lt;table&gt;_id" or "&lt;table&gt;Id" to PK columns of matching tables.
    /// </summary>
    public async Task<List<LogicalFkCandidate>> DetectCandidatesAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var columns = await logicalFkRepository.GetColumnsForDetectionAsync(projectId, cancellationToken);
        var candidates = new List<LogicalFkCandidate>();

        // Group by table for fast lookup
        var columnsByTable = columns.GroupBy(c => c.TableId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Build table name → table info lookup (case-insensitive)
        var tableNames = columns
            .Select(c => new { c.TableId, c.TableName })
            .DistinctBy(t => t.TableId)
            .ToDictionary(t => t.TableName, t => t.TableId, StringComparer.OrdinalIgnoreCase);

        // Find PK columns per table
        var pksByTable = columns
            .Where(c => c.IsPrimaryKey)
            .GroupBy(c => c.TableId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var col in columns)
        {
            // Skip PK and already-FK columns
            if (col.IsPrimaryKey || col.IsForeignKey) continue;

            var match = FkColumnPattern().Match(col.ColumnName);
            if (!match.Success) continue;

            var prefix = match.Groups[1].Value;

            // Try to find a matching table by prefix
            // e.g., "customer" → "customers", "Customer", "Customers"
            int? targetTableId = TryResolveTable(prefix, tableNames);
            if (targetTableId == null) continue;

            // Don't match a table to itself
            if (targetTableId == col.TableId) continue;

            // Check that the target table has a PK we can reference
            if (!pksByTable.TryGetValue(targetTableId.Value, out var targetPks) || targetPks.Count == 0)
                continue;

            // Use the first PK column (single-column PK for name convention detection)
            var targetPk = targetPks[0];

            // Data type must match
            if (!DataTypesCompatible(col.DataType, targetPk.DataType))
                continue;

            // Skip if this is already a physical FK
            var isPhysical = await logicalFkRepository.ExistsAsPhysicalFkAsync(
                col.TableId, col.ColumnId, targetTableId.Value, targetPk.ColumnId, cancellationToken);
            if (isPhysical) continue;

            // Skip if already exists as a logical FK
            var alreadyExists = await logicalFkRepository.ExistsAsync(
                projectId, col.TableId, [col.ColumnId], targetTableId.Value, [targetPk.ColumnId], cancellationToken);
            if (alreadyExists) continue;

            candidates.Add(new LogicalFkCandidate
            {
                SourceTableId = col.TableId,
                SourceTableName = col.TableName,
                SourceColumnId = col.ColumnId,
                SourceColumnName = col.ColumnName,
                SourceDataType = col.DataType,
                TargetTableId = targetTableId.Value,
                TargetTableName = targetPks[0].TableName,
                TargetColumnId = targetPk.ColumnId,
                TargetColumnName = targetPk.ColumnName,
                TargetDataType = targetPk.DataType,
                ConfidenceScore = 0.70m,
                Reason = $"Column '{col.ColumnName}' follows naming convention matching table '{targetPks[0].TableName}' PK '{targetPk.ColumnName}' with compatible data types."
            });
        }

        logger.LogInformation(
            "Detected {Count} logical FK candidates for project {ProjectId}",
            candidates.Count, projectId);

        return candidates;
    }

    public async Task<List<PhysicalFkDto>> GetPhysicalFksByTableAsync(int projectId, int tableId, CancellationToken cancellationToken = default)
    {
        return await logicalFkRepository.GetPhysicalFksByTableAsync(projectId, tableId, cancellationToken);
    }

    #region Private Helpers

    /// <summary>
    /// Feed a confirmed logical FK into the Dependencies table for impact analysis
    /// </summary>
    private async Task FeedIntoDependenciesAsync(
        int projectId, int sourceTableId, int targetTableId,
        decimal confidenceScore, CancellationToken cancellationToken = default)
    {
        try
        {
            var dependency = new ResolvedDependency
            {
                ProjectId = projectId,
                SourceType = "TABLE",
                SourceId = sourceTableId,
                TargetType = "TABLE",
                TargetId = targetTableId,
                DependencyType = "LOGICAL_FK",
                ConfidenceScore = confidenceScore
            };

            await dependencyRepository.SaveDependenciesForSourcesAsync(
                projectId, [dependency], cancellationToken);

            logger.LogInformation(
                "Fed logical FK into Dependencies: Table {SourceTableId} → Table {TargetTableId} (confidence: {Confidence})",
                sourceTableId, targetTableId, confidenceScore);
        }
        catch (Exception ex)
        {
            // Don't fail the confirm operation if dependency feed fails
            logger.LogError(ex,
                "Failed to feed logical FK into Dependencies for project {ProjectId}", projectId);
        }
    }

    /// <summary>
    /// Try to resolve a column name prefix to a table name.
    /// Handles: "customer" → "Customers", "customers", "Customer"
    /// </summary>
    private static int? TryResolveTable(string prefix, Dictionary<string, int> tableNames)
    {
        // Direct match
        if (tableNames.TryGetValue(prefix, out int tableId))
            return tableId;

        // Pluralized: "customer" → "customers"
        if (tableNames.TryGetValue(prefix + "s", out tableId))
            return tableId;

        // Singular from plural: "customers" → "customer"
        if (prefix.EndsWith('s') && tableNames.TryGetValue(prefix[..^1], out tableId))
            return tableId;

        // Snake_case to table: "order_item" → "order_items"
        if (tableNames.TryGetValue(prefix + "s", out tableId))
            return tableId;

        return null;
    }

    /// <summary>
    /// Check if two SQL Server data types are compatible for FK matching
    /// </summary>
    private static bool DataTypesCompatible(string sourceType, string targetType)
    {
        var source = NormalizeDataType(sourceType);
        var target = NormalizeDataType(targetType);
        return source == target;
    }

    private static string NormalizeDataType(string dataType)
    {
        var lower = dataType.Trim().ToLowerInvariant();
        // Treat int/bigint/smallint as grouped families
        return lower switch
        {
            "int" or "bigint" or "smallint" or "tinyint" => "integer_family",
            "uniqueidentifier" => "guid",
            "nvarchar" or "varchar" or "char" or "nchar" => "string_family",
            _ => lower
        };
    }

    #endregion
}
