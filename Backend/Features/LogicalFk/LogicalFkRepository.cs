using ActoEngine.WebApi.Infrastructure.Database;
using ActoEngine.WebApi.Shared;
using Dapper;
using System.Text.Json;

namespace ActoEngine.WebApi.Features.LogicalFk;

/// <summary>
/// Repository contract for LogicalForeignKey operations
/// </summary>
public interface ILogicalFkRepository
{
    Task<List<LogicalFkDto>> GetByProjectAsync(int projectId, string? statusFilter = null, CancellationToken cancellationToken = default);
    Task<LogicalFkDto?> GetByIdAsync(int projectId, int logicalFkId, CancellationToken cancellationToken = default);
    Task<List<LogicalFkDto>> GetByTableAsync(int projectId, int tableId, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(LogicalForeignKey logicalForeignKey, CancellationToken cancellationToken = default);
    Task ConfirmAsync(int projectId, int logicalFkId, int userId, string? notes = null, CancellationToken cancellationToken = default);
    Task RejectAsync(int projectId, int logicalFkId, int userId, string? notes = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(int projectId, int logicalFkId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(int projectId, int sourceTableId, List<int> sourceColumnIds, int targetTableId, List<int> targetColumnIds, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsPhysicalFkAsync(int sourceTableId, int sourceColumnId, int targetTableId, int targetColumnId, CancellationToken cancellationToken = default);
    Task<List<DetectionColumnInfo>> GetColumnsForDetectionAsync(int projectId, CancellationToken cancellationToken = default);
    Task<List<PhysicalFkDto>> GetPhysicalFksByTableAsync(int projectId, int tableId, CancellationToken cancellationToken = default);

    /// <summary>Bulk-load all physical FK column pairs for batch exclusion</summary>
    Task<HashSet<string>> GetAllPhysicalFkPairsAsync(int projectId, CancellationToken cancellationToken = default);

    /// <summary>Bulk-load canonical keys of all existing logical FKs for batch dedup</summary>
    Task<HashSet<string>> GetAllLogicalFkCanonicalKeysAsync(int projectId, CancellationToken cancellationToken = default);

    /// <summary>Bulk-upsert detected candidates as SUGGESTED rows (INSERT + UPDATE, no MERGE)</summary>
    Task<int> BulkUpsertSuggestedAsync(int projectId, List<LogicalFkCandidate> candidates, CancellationToken cancellationToken = default);

    Task<List<LogicalFkCandidate>> GetPersistedSuggestedCandidatesAsync(int projectId, CancellationToken cancellationToken = default);

    /// <summary>Read detection staleness metadata from Projects</summary>
    Task<DetectionMetadata?> GetDetectionMetadataAsync(int projectId, CancellationToken cancellationToken = default);

    /// <summary>Update detection metadata after a successful run</summary>
    Task UpdateDetectionMetadataAsync(int projectId, string algorithmVersion, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for LogicalForeignKey operations
/// </summary>
public class LogicalFkRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<LogicalFkRepository> logger)
    : BaseRepository(connectionFactory, logger), ILogicalFkRepository
{
    public async Task<List<LogicalFkDto>> GetByProjectAsync(int projectId, string? statusFilter = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<LogicalFkRawRow> rows;

        if (statusFilter != null)
        {
            rows = await QueryAsync<LogicalFkRawRow>(
                LogicalFkQueries.GetByProjectFiltered,
                new { ProjectId = projectId, Status = statusFilter },
                cancellationToken);
        }
        else
        {
            rows = await QueryAsync<LogicalFkRawRow>(
                LogicalFkQueries.GetByProject,
                new { ProjectId = projectId },
                cancellationToken);
        }

        var dtos = rows.Select(MapToDto).ToList();
        return await EnrichDtosWithColumnNames(dtos, projectId, cancellationToken);
    }

    public async Task<LogicalFkDto?> GetByIdAsync(int projectId, int logicalFkId, CancellationToken cancellationToken = default)
    {
        var row = await QueryFirstOrDefaultAsync<LogicalFkRawRow>(
            LogicalFkQueries.GetById,
            new { ProjectId = projectId, LogicalFkId = logicalFkId },
            cancellationToken);

        if (row == null) return null;

        var dto = MapToDto(row);
        var enriched = await EnrichDtosWithColumnNames([dto], projectId, cancellationToken);
        return enriched.FirstOrDefault();
    }

    public async Task<List<LogicalFkDto>> GetByTableAsync(int projectId, int tableId, CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync<LogicalFkRawRow>(
            LogicalFkQueries.GetByTable,
            new { ProjectId = projectId, TableId = tableId },
            cancellationToken);

        var dtos = rows.Select(MapToDto).ToList();
        return await EnrichDtosWithColumnNames(dtos, projectId, cancellationToken);
    }

    public async Task<int> CreateAsync(
        LogicalForeignKey logicalForeignKey,
        CancellationToken cancellationToken = default)
    {
        var id = await ExecuteScalarAsync<int>(
            LogicalFkQueries.Insert,
            new
            {
                logicalForeignKey.ProjectId,
                logicalForeignKey.SourceTableId,
                logicalForeignKey.SourceColumnIds,
                logicalForeignKey.TargetTableId,
                logicalForeignKey.TargetColumnIds,
                logicalForeignKey.DiscoveryMethod,
                logicalForeignKey.ConfidenceScore,
                logicalForeignKey.Status,
                logicalForeignKey.Notes,
                logicalForeignKey.CreatedBy,
                logicalForeignKey.ConfirmedBy,
                logicalForeignKey.ConfirmedAt
            },
            cancellationToken);

        _logger.LogInformation(
            "Created logical FK {LogicalFkId} for project {ProjectId}: Table {SourceTableId} → Table {TargetTableId}",
            id, logicalForeignKey.ProjectId, logicalForeignKey.SourceTableId, logicalForeignKey.TargetTableId);

        return id;
    }

    public async Task ConfirmAsync(int projectId, int logicalFkId, int userId, string? notes = null, CancellationToken cancellationToken = default)
    {
        var rows = await ExecuteAsync(
            LogicalFkQueries.Confirm,
            new { LogicalFkId = logicalFkId, ProjectId = projectId, UserId = userId, Notes = notes },
            cancellationToken);

        if (rows == 0)
        {
            _logger.LogWarning("No logical FK found with ID {LogicalFkId} (Project {ProjectId}) to confirm", logicalFkId, projectId);
        }
    }

    public async Task RejectAsync(int projectId, int logicalFkId, int userId, string? notes = null, CancellationToken cancellationToken = default)
    {
        var rows = await ExecuteAsync(
            LogicalFkQueries.Reject,
            new { LogicalFkId = logicalFkId, ProjectId = projectId, UserId = userId, Notes = notes },
            cancellationToken);

        if (rows == 0)
        {
            _logger.LogWarning("No logical FK found with ID {LogicalFkId} (Project {ProjectId}) to reject", logicalFkId, projectId);
        }
    }

    public async Task DeleteAsync(int projectId, int logicalFkId, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(
            LogicalFkQueries.Delete,
            new { LogicalFkId = logicalFkId, ProjectId = projectId },
            cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        int projectId,
        int sourceTableId,
        List<int> sourceColumnIds,
        int targetTableId,
        List<int> targetColumnIds,
        CancellationToken cancellationToken = default)
    {
        var count = await ExecuteScalarAsync<int>(
            LogicalFkQueries.Exists,
            new
            {
                ProjectId = projectId,
                SourceTableId = sourceTableId,
                SourceColumnIds = JsonSerializer.Serialize(sourceColumnIds),
                TargetTableId = targetTableId,
                TargetColumnIds = JsonSerializer.Serialize(targetColumnIds)
            },
            cancellationToken);

        return count > 0;
    }

    public async Task<bool> ExistsAsPhysicalFkAsync(
        int sourceTableId,
        int sourceColumnId,
        int targetTableId,
        int targetColumnId,
        CancellationToken cancellationToken = default)
    {
        var count = await ExecuteScalarAsync<int>(
            LogicalFkQueries.ExistsAsPhysicalFk,
            new
            {
                SourceTableId = sourceTableId,
                SourceColumnId = sourceColumnId,
                TargetTableId = targetTableId,
                TargetColumnId = targetColumnId
            },
            cancellationToken);

        return count > 0;
    }

    public async Task<List<DetectionColumnInfo>> GetColumnsForDetectionAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync<DetectionColumnInfo>(
            LogicalFkQueries.GetColumnsForDetectionWithUniques,
            new { ProjectId = projectId },
            cancellationToken);

        return [.. rows];
    }

    public async Task<List<PhysicalFkDto>> GetPhysicalFksByTableAsync(int projectId, int tableId, CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync<PhysicalFkDto>(
            LogicalFkQueries.GetPhysicalFksByTable,
            new { ProjectId = projectId, TableId = tableId },
            cancellationToken);

        return [.. rows];
    }

    public async Task<HashSet<string>> GetAllPhysicalFkPairsAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync<dynamic>(
            LogicalFkQueries.GetAllPhysicalFkPairs,
            new { ProjectId = projectId },
            cancellationToken);

        var set = new HashSet<string>();
        foreach (var row in rows)
        {
            set.Add($"{row.SourceTableId}:{row.SourceColumnId}\u2192{row.TargetTableId}:{row.TargetColumnId}");
        }
        return set;
    }

    public async Task<HashSet<string>> GetAllLogicalFkCanonicalKeysAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync<dynamic>(
            LogicalFkQueries.GetAllLogicalFkCanonicalKeys,
            new { ProjectId = projectId },
            cancellationToken);

        var set = new HashSet<string>();
        foreach (var row in rows)
        {
            set.Add($"{row.SourceTableId}:{row.SourceColumnId}\u2192{row.TargetTableId}:{row.TargetColumnId}");
        }
        return set;
    }

    #region Mapping Helpers

    private async Task<List<LogicalFkDto>> EnrichDtosWithColumnNames(List<LogicalFkDto> dtos, int projectId, CancellationToken cancellationToken)
    {
        if (dtos.Count == 0) return dtos;

        var allColumnIds = dtos.SelectMany(d => d.SourceColumnIds)
            .Concat(dtos.SelectMany(d => d.TargetColumnIds))
            .Distinct()
            .ToList();

        if (allColumnIds.Count == 0) return dtos;



        var columnNames = await QueryAsync<dynamic>(
            LogicalFkQueries.GetColumnNamesQuery,
            new { ProjectId = projectId, ColumnIds = allColumnIds },
            cancellationToken);

        var nameMap = columnNames.ToDictionary(r => (int)r.ColumnId, r => (string)r.ColumnName);

        foreach (var dto in dtos)
        {
            dto.SourceColumnNames = dto.SourceColumnIds
                .Select(id => nameMap.TryGetValue(id, out var name) ? name : $"Col_{id}")
                .ToList();

            dto.TargetColumnNames = dto.TargetColumnIds
                .Select(id => nameMap.TryGetValue(id, out var name) ? name : $"Col_{id}")
                .ToList();
        }

        return dtos;
    }

    private static LogicalFkDto MapToDto(LogicalFkRawRow row)
    {
        return new LogicalFkDto
        {
            LogicalFkId = row.LogicalFkId,
            ProjectId = row.ProjectId,
            SourceTableId = row.SourceTableId,
            SourceTableName = row.SourceTableName,
            SourceColumnIds = ParseJsonIntArray(row.SourceColumnIds),
            TargetTableId = row.TargetTableId,
            TargetTableName = row.TargetTableName,
            TargetColumnIds = ParseJsonIntArray(row.TargetColumnIds),
            DiscoveryMethod = row.DiscoveryMethod,
            ConfidenceScore = row.ConfidenceScore,
            Status = row.Status,
            ConfirmedBy = row.ConfirmedBy,
            ConfirmedAt = row.ConfirmedAt,
            Notes = row.Notes,
            CreatedAt = row.CreatedAt
        };
    }

    private static List<int> ParseJsonIntArray(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<int>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    #endregion

    #region Detection Persistence

    public async Task<int> BulkUpsertSuggestedAsync(
        int projectId,
        List<LogicalFkCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var affected = 0;

            foreach (var c in candidates)
            {
                var sourceColumnIds = JsonSerializer.Serialize(new[] { c.SourceColumnId });
                var targetColumnIds = JsonSerializer.Serialize(new[] { c.TargetColumnId });
                var discoveryMethod = c.DiscoveryMethods.Count > 1 ? "CORROBORATED"
                    : c.DiscoveryMethods.FirstOrDefault() ?? "NAME_CONVENTION";
                var discoveryMethodsJson = JsonSerializer.Serialize(c.DiscoveryMethods);

                var parameters = new
                {
                    ProjectId = projectId,
                    c.SourceTableId,
                    SourceColumnIds = sourceColumnIds,
                    c.TargetTableId,
                    TargetColumnIds = targetColumnIds,
                    DiscoveryMethod = discoveryMethod,
                    c.ConfidenceScore,
                    DetectionReason = c.Reason,
                    DiscoveryMethods = discoveryMethodsJson
                };

                // Step 1: Try INSERT (new candidates)
                var inserted = await connection.ExecuteAsync(
                    LogicalFkQueries.InsertSuggestedCandidate, parameters, transaction);

                if (inserted > 0)
                {
                    affected += inserted;
                    continue;
                }

                // Step 2: Try re-surface REJECTED (score improved)
                var resurfaced = await connection.ExecuteAsync(
                    LogicalFkQueries.UpdateResurfacedCandidate, parameters, transaction);

                if (resurfaced > 0)
                {
                    affected += resurfaced;
                    _logger.LogInformation(
                        "Re-surfaced rejected FK: {Source} → {Target} (new score: {Score})",
                        c.SourceTableName, c.TargetTableName, c.ConfidenceScore);
                    continue;
                }

                // Step 3: Refresh existing SUGGESTED with updated score/reason
                affected += await connection.ExecuteAsync(
                    LogicalFkQueries.UpdateExistingSuggested, parameters, transaction);
            }

            return affected;
        }, cancellationToken);
    }

    public async Task<List<LogicalFkCandidate>> GetPersistedSuggestedCandidatesAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var rawRows = await QueryAsync<dynamic>(
            LogicalFkQueries.GetSuggestedCandidates,
            new { ProjectId = projectId },
            cancellationToken);

        var candidates = new List<LogicalFkCandidate>();
        foreach (var row in rawRows)
        {
            string? srcRaw = row.SourceColumnId?.ToString();
            string? tgtRaw = row.TargetColumnId?.ToString();
            int sourceTableId = (int)row.SourceTableId;
            string sourceTableName = (string)row.SourceTableName;
            int targetTableId = (int)row.TargetTableId;
            string targetTableName = (string)row.TargetTableName;

            if (!int.TryParse(srcRaw, out int srcId))
            {
                _logger.LogWarning(
                    "Failed to parse SourceColumnId '{RawValue}' for suggested FK in table {TableId} ({TableName})",
                    srcRaw, sourceTableId, sourceTableName);
                srcId = -1;
            }

            if (!int.TryParse(tgtRaw, out int tgtId))
            {
                _logger.LogWarning(
                    "Failed to parse TargetColumnId '{RawValue}' for suggested FK in table {TableId} ({TableName})",
                    tgtRaw, targetTableId, targetTableName);
                tgtId = -1;
            }

            candidates.Add(new LogicalFkCandidate
            {
                SourceTableId = row.SourceTableId,
                SourceTableName = row.SourceTableName,
                SourceColumnId = srcId,
                SourceColumnName = row.SourceColumnName ?? "",
                SourceDataType = row.SourceDataType ?? "",
                TargetTableId = row.TargetTableId,
                TargetTableName = row.TargetTableName,
                TargetColumnId = tgtId,
                TargetColumnName = row.TargetColumnName ?? "",
                TargetDataType = row.TargetDataType ?? "",
                ConfidenceScore = row.ConfidenceScore,
                ConfidenceBand = ConfidenceBandClassifier.Classify((decimal)row.ConfidenceScore),
                Reason = row.Reason ?? "",
                IsAmbiguous = false,
                DiscoveryMethods = string.IsNullOrWhiteSpace((string)row.DiscoveryMethods) 
                    ? new List<string>() 
                    : JsonSerializer.Deserialize<List<string>>((string)row.DiscoveryMethods) ?? new List<string>(),
                MatchCount = 0
            });
        }
        return candidates;
    }

    public async Task<DetectionMetadata?> GetDetectionMetadataAsync(
        int projectId,
        CancellationToken cancellationToken = default)
    {
        return await QueryFirstOrDefaultAsync<DetectionMetadata>(
            LogicalFkQueries.GetDetectionMetadata,
            new { ProjectId = projectId },
            cancellationToken);
    }

    public async Task UpdateDetectionMetadataAsync(
        int projectId,
        string algorithmVersion,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(
            LogicalFkQueries.UpdateDetectionMetadata,
            new { ProjectId = projectId, AlgorithmVersion = algorithmVersion },
            cancellationToken);
    }

    #endregion
}

/// <summary>
/// Raw row from the joined query (before JSON parsing)
/// </summary>
internal class LogicalFkRawRow
{
    public int LogicalFkId { get; set; }
    public int ProjectId { get; set; }
    public int SourceTableId { get; set; }
    public string SourceTableName { get; set; } = string.Empty;
    public string SourceColumnIds { get; set; } = "[]";
    public int TargetTableId { get; set; }
    public string TargetTableName { get; set; } = string.Empty;
    public string TargetColumnIds { get; set; } = "[]";
    public string DiscoveryMethod { get; set; } = "MANUAL";
    public decimal ConfidenceScore { get; set; }
    public string Status { get; set; } = "SUGGESTED";
    public int? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Column info used by the detection engine
/// </summary>
public class DetectionColumnInfo
{
    public int ColumnId { get; set; }
    public int TableId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public bool IsUnique { get; set; }
    public string TableName { get; set; } = string.Empty;
}
