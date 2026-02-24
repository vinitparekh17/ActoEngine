using ActoEngine.WebApi.Features.ImpactAnalysis;
using ActoEngine.WebApi.Features.Schema;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ActoEngine.WebApi.Features.LogicalFk;

/// <summary>
/// Service contract for Logical FK operations
/// </summary>
public interface ILogicalFkService
{
    Task<List<LogicalFkDto>> GetByProjectAsync(int projectId, string? statusFilter = null, CancellationToken cancellationToken = default);
    Task<LogicalFkDto?> GetByIdAsync(int projectId, int logicalFkId, CancellationToken cancellationToken = default);
    Task<List<LogicalFkDto>> GetByTableAsync(int projectId, int tableId, CancellationToken cancellationToken = default);
    Task<LogicalFkDto> CreateManualAsync(int projectId, CreateLogicalFkRequest request, int userId, CancellationToken cancellationToken = default);
    Task<LogicalFkDto> ConfirmAsync(int projectId, int logicalFkId, int userId, string? notes = null, CancellationToken cancellationToken = default);
    Task<LogicalFkDto> RejectAsync(int projectId, int logicalFkId, int userId, string? notes = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(int projectId, int logicalFkId, CancellationToken cancellationToken = default);
    Task<List<LogicalFkCandidate>> DetectCandidatesAsync(int projectId, CancellationToken cancellationToken = default);
    Task<List<PhysicalFkDto>> GetPhysicalFksByTableAsync(int projectId, int tableId, CancellationToken cancellationToken = default);
    Task<int> DetectAndPersistCandidatesAsync(int projectId, CancellationToken cancellationToken = default);
    Task<bool> IsDetectionStaleAsync(int projectId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Multi-strategy logical FK detection engine.
/// Strategy 1: SP JOIN Analysis — Parse SP definitions for JOIN ON conditions
/// Strategy 2: Naming Convention — Match *_id / *Id columns to PK columns
/// Strategy 3: Corroboration — Boost when both strategies agree
/// </summary>
public partial class LogicalFkService(
    ILogicalFkRepository logicalFkRepository,
    IDependencyRepository dependencyRepository,
    ISchemaRepository schemaRepository,
    IDependencyAnalysisService analysisService,
    ConfidenceCalculator confidenceCalculator,
    ILogger<LogicalFkService> logger) : ILogicalFkService
{
    // Name convention patterns: orders.customer_id → customers.id
    [GeneratedRegex(@"^(.+?)(?:_?[Ii][Dd])$")]
    private static partial Regex FkColumnPattern();

    /// <summary>
    /// Bump this when detection logic, confidence thresholds, or scoring rules change.
    /// The staleness guard compares this against the stored version to invalidate cache.
    /// </summary>
    public const string AlgorithmVersion = "1.0";

    public async Task<List<LogicalFkDto>> GetByProjectAsync(int projectId, string? statusFilter = null, CancellationToken cancellationToken = default)
        => await logicalFkRepository.GetByProjectAsync(projectId, statusFilter, cancellationToken);

    public async Task<LogicalFkDto?> GetByIdAsync(int projectId, int logicalFkId, CancellationToken cancellationToken = default)
        => await logicalFkRepository.GetByIdAsync(projectId, logicalFkId, cancellationToken);

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

        return await logicalFkRepository.GetByIdAsync(projectId, id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve created logical FK.");
    }

    public async Task<LogicalFkDto> ConfirmAsync(int projectId, int logicalFkId, int userId, string? notes = null, CancellationToken cancellationToken = default)
    {
        var existing = await logicalFkRepository.GetByIdAsync(projectId, logicalFkId, cancellationToken)
            ?? throw new KeyNotFoundException($"Logical FK {logicalFkId} not found.");

        if (existing.ProjectId != projectId)
        {
            throw new KeyNotFoundException($"Logical FK {logicalFkId} does not belong to project {projectId}.");
        }

        await logicalFkRepository.ConfirmAsync(projectId, logicalFkId, userId, notes, cancellationToken);

        // Feed confirmed FK into Dependencies table
        await FeedIntoDependenciesAsync(
            existing.ProjectId, existing.SourceTableId, existing.TargetTableId,
            existing.ConfidenceScore, cancellationToken);

        return await logicalFkRepository.GetByIdAsync(projectId, logicalFkId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve confirmed logical FK.");
    }

    public async Task<LogicalFkDto> RejectAsync(int projectId, int logicalFkId, int userId, string? notes = null, CancellationToken cancellationToken = default)
    {
        var existing = await logicalFkRepository.GetByIdAsync(projectId, logicalFkId, cancellationToken)
            ?? throw new KeyNotFoundException($"Logical FK {logicalFkId} not found.");

        if (existing.ProjectId != projectId)
        {
            throw new KeyNotFoundException($"Logical FK {logicalFkId} does not belong to project {projectId}.");
        }

        await logicalFkRepository.RejectAsync(projectId, logicalFkId, userId, notes, cancellationToken);

        return await logicalFkRepository.GetByIdAsync(projectId, logicalFkId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to retrieve rejected logical FK.");
    }

    public async Task DeleteAsync(int projectId, int logicalFkId, CancellationToken cancellationToken = default)
    {
        // Check existence and ownership
        var existing = await logicalFkRepository.GetByIdAsync(projectId, logicalFkId, cancellationToken)
             ?? throw new KeyNotFoundException($"Logical FK {logicalFkId} not found.");

        if (existing.ProjectId != projectId)
        {
            // Treating mismatches as Not Found to avoid leaking existence
            throw new KeyNotFoundException($"Logical FK {logicalFkId} not found in project {projectId}.");
        }

        await logicalFkRepository.DeleteAsync(projectId, logicalFkId, cancellationToken);
    }

    /// <summary>
    /// Multi-strategy logical FK detection engine.
    /// 1. SP JOIN Analysis — Parse SP definitions for JOIN ON conditions (high confidence)
    /// 2. Naming Convention — Match *_id / *Id columns to PK columns (medium confidence)
    /// 3. Corroboration — When both strategies agree, boost confidence
    /// </summary>
    public async Task<List<LogicalFkCandidate>> DetectCandidatesAsync(int projectId, CancellationToken cancellationToken = default)
    {
        // ── Load shared metadata ──────────────────────────────────
        var columns = await logicalFkRepository.GetColumnsForDetectionAsync(projectId, cancellationToken);

        // Group by table for fast lookup
        var columnsByTable = columns.GroupBy(c => c.TableId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Table name → table ID (case-insensitive)
        var tableNameToId = columns
            .Select(c => new { c.TableId, c.TableName })
            .DistinctBy(t => t.TableId)
            .ToDictionary(t => t.TableName, t => t.TableId, StringComparer.OrdinalIgnoreCase);

        // Column lookup: (tableId, columnName) → columnInfo
        var columnLookup = columns
            .GroupBy(c => c.TableId)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(c => c.ColumnName, c => c, StringComparer.OrdinalIgnoreCase));

        // ── Batch-load exclusion sets (avoid N+1) ─────────────────
        var physicalFkKeys = await logicalFkRepository.GetAllPhysicalFkPairsAsync(projectId, cancellationToken);
        var logicalFkKeys = await logicalFkRepository.GetAllLogicalFkCanonicalKeysAsync(projectId, cancellationToken);

        // ── Strategy 1: SP JOIN Analysis ──────────────────────────
        // Keyed by canonical key → (candidate info, list of SP names)
        var spJoinCandidates = new Dictionary<string, SpJoinEvidence>();

        try
        {
            var procedures = await schemaRepository.GetStoredStoredProceduresAsync(projectId);

            foreach (var sp in procedures)
            {
                if (string.IsNullOrWhiteSpace(sp.Definition)) continue;

                try
                {
                    var joinConditions = analysisService.ExtractJoinConditions(sp.Definition);

                    foreach (var jc in joinConditions)
                    {
                        // Resolve table names to IDs
                        int? leftTableId = ResolveTableName(jc.LeftTable, tableNameToId);
                        int? rightTableId = ResolveTableName(jc.RightTable, tableNameToId);

                        if (leftTableId == null || rightTableId == null) continue;
                        if (leftTableId == rightTableId) continue; // Self-join

                        // Resolve column names to IDs
                        var leftCol = ResolveColumn(leftTableId.Value, jc.LeftColumn, columnLookup);
                        var rightCol = ResolveColumn(rightTableId.Value, jc.RightColumn, columnLookup);

                        if (leftCol == null || rightCol == null) continue;

                        // Determine direction: the FK side is typically the non-PK column
                        // If right is PK → left references right (left is FK side)
                        // If left is PK → right references left (right is FK side)
                        DetectionColumnInfo sourceCol, targetCol;

                        if (rightCol.IsPrimaryKey && !leftCol.IsPrimaryKey)
                        {
                            sourceCol = leftCol;
                            targetCol = rightCol;
                        }
                        else if (leftCol.IsPrimaryKey && !rightCol.IsPrimaryKey)
                        {
                            sourceCol = rightCol;
                            targetCol = leftCol;
                        }
                        else
                        {
                            // Both PK or neither PK — take the one with *Id suffix as source
                            if (FkColumnPattern().IsMatch(leftCol.ColumnName))
                            {
                                sourceCol = leftCol;
                                targetCol = rightCol;
                            }
                            else
                            {
                                sourceCol = rightCol;
                                targetCol = leftCol;
                            }
                        }

                        var canonicalKey = $"{sourceCol.TableId}:{sourceCol.ColumnId}\u2192{targetCol.TableId}:{targetCol.ColumnId}";

                        if (!spJoinCandidates.TryGetValue(canonicalKey, out var evidence))
                        {
                            evidence = new SpJoinEvidence
                            {
                                SourceCol = sourceCol,
                                TargetCol = targetCol,
                                SpNames = []
                            };
                            spJoinCandidates[canonicalKey] = evidence;
                        }

                        if (!evidence.SpNames.Contains(sp.ProcedureName))
                        {
                            evidence.SpNames.Add(sp.ProcedureName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to extract JOIN conditions from SP {SpName} (ID: {SpId})",
                        sp.ProcedureName, sp.SpId);
                }
            }

            logger.LogInformation(
                "SP JOIN analysis found {Count} candidate relationships from {SpCount} procedures for project {ProjectId}",
                spJoinCandidates.Count, procedures.Count, projectId);
        }
        catch (Exception ex)
        {
            // SP analysis failure is non-fatal — fall through to naming convention
            logger.LogError(ex, "SP JOIN analysis failed for project {ProjectId}. Falling back to naming convention only.", projectId);
        }

        // ── Strategy 2: Naming Convention ─────────────────────────
        var namingCandidates = new Dictionary<string, NamingEvidence>();
        var namingAmbiguityGroups = new Dictionary<string, List<string>>();

        foreach (var col in columns)
        {
            // Skip PK and already-FK columns
            if (col.IsPrimaryKey || col.IsForeignKey) continue;

            var match = FkColumnPattern().Match(col.ColumnName);
            if (!match.Success) continue;

            var prefix = match.Groups[1].Value;

            // Try to find a matching table by prefix
            int? targetTableId = TryResolveTable(prefix, tableNameToId);
            if (targetTableId == null) continue;

            // Don't match a table to itself
            if (targetTableId == col.TableId) continue;

            if (!columnsByTable.TryGetValue(targetTableId.Value, out var targetColumns) || targetColumns.Count == 0)
            {
                continue;
            }

            // Tiering: PRIMARY KEY always outranks UNIQUE.
            var targetCandidates = targetColumns
                .Where(c => c.IsPrimaryKey || c.IsUnique)
                .Select(c => new
                {
                    Column = c,
                    StructuralTier = c.IsPrimaryKey ? 2 : 1,
                    NamingScore = GetNamingScore(prefix, c.ColumnName)
                })
                .ToList();

            if (targetCandidates.Count == 0)
            {
                continue;
            }

            var maxTier = targetCandidates.Max(c => c.StructuralTier);
            var tierWinners = targetCandidates.Where(c => c.StructuralTier == maxTier).ToList();

            var maxNamingScore = tierWinners.Max(c => c.NamingScore);
            if (maxNamingScore == 0)
                continue;
            var winners = tierWinners.Where(c => c.NamingScore == maxNamingScore).ToList();

            var ambiguityGroupKey = $"{col.TableId}:{col.ColumnId}\u2192{targetTableId.Value}";
            var isAmbiguous = winners.Count > 1;

            foreach (var winner in winners)
            {
                var canonicalKey = $"{col.TableId}:{col.ColumnId}\u2192{targetTableId.Value}:{winner.Column.ColumnId}";

                namingCandidates[canonicalKey] = new NamingEvidence
                {
                    SourceCol = col,
                    TargetCol = winner.Column,
                    IsAmbiguous = isAmbiguous
                };

                if (!isAmbiguous)
                {
                    continue;
                }

                if (!namingAmbiguityGroups.TryGetValue(ambiguityGroupKey, out var keys))
                {
                    keys = [];
                    namingAmbiguityGroups[ambiguityGroupKey] = keys;
                }

                keys.Add(canonicalKey);
            }
        }

        // Corroboration can disambiguate ties when exactly one candidate in a tied naming group
        // also appears in SP JOIN evidence.
        foreach (var tiedKeys in namingAmbiguityGroups.Values)
        {
            var corroborated = tiedKeys.Where(k => spJoinCandidates.ContainsKey(k)).ToList();
            if (corroborated.Count != 1)
            {
                continue;
            }

            var selected = corroborated[0];
            foreach (var key in tiedKeys)
            {
                if (!namingCandidates.TryGetValue(key, out var evidence))
                {
                    continue;
                }

                if (key == selected)
                {
                    evidence.IsAmbiguous = false;
                }
                else
                {
                    namingCandidates.Remove(key);
                }
            }
        }
        logger.LogInformation(
            "Naming convention analysis found {Count} candidate relationships for project {ProjectId}",
            namingCandidates.Count, projectId);

        // ── Strategy 3: Merge + Corroborate + Score ───────────────
        var allCanonicalKeys = new HashSet<string>(spJoinCandidates.Keys);
        allCanonicalKeys.UnionWith(namingCandidates.Keys);

        var candidates = new List<LogicalFkCandidate>();

        foreach (var key in allCanonicalKeys)
        {
            // Skip if already a physical FK
            if (physicalFkKeys.Contains(key)) continue;

            // Skip if already an existing logical FK
            if (logicalFkKeys.Contains(key)) continue;

            var hasSpJoin = spJoinCandidates.TryGetValue(key, out var spEvidence);
            var hasNaming = namingCandidates.TryGetValue(key, out var namingEvidence);

            // Get source/target info from whichever strategy provided it
            var sourceCol = hasSpJoin ? spEvidence!.SourceCol : namingEvidence!.SourceCol;
            var targetCol = hasSpJoin ? spEvidence!.TargetCol : namingEvidence!.TargetCol;

            // Build detection signals
            var signals = new DetectionSignals
            {
                SpJoinDetected = hasSpJoin,
                NamingDetected = hasNaming,
                TypeMatch = DataTypesCompatible(sourceCol.DataType, targetCol.DataType),
                HasIdSuffix = FkColumnPattern().IsMatch(sourceCol.ColumnName),
                SpCount = hasSpJoin ? spEvidence!.SpNames.Count : 0
            };

            // Compute confidence with layered caps
            var result = confidenceCalculator.ComputeConfidence(signals);

            // Build discovery methods list
            var discoveryMethods = new List<string>();
            if (hasSpJoin) discoveryMethods.Add("SP_JOIN");
            if (hasNaming) discoveryMethods.Add("NAME_CONVENTION");

            // Build human-readable reason
            var reason = BuildReason(sourceCol, targetCol, signals, spEvidence, result);

            candidates.Add(new LogicalFkCandidate
            {
                SourceTableId = sourceCol.TableId,
                SourceTableName = sourceCol.TableName,
                SourceColumnId = sourceCol.ColumnId,
                SourceColumnName = sourceCol.ColumnName,
                SourceDataType = sourceCol.DataType,
                TargetTableId = targetCol.TableId,
                TargetTableName = targetCol.TableName,
                TargetColumnId = targetCol.ColumnId,
                TargetColumnName = targetCol.ColumnName,
                TargetDataType = targetCol.DataType,
                ConfidenceScore = result.FinalConfidence,
                ConfidenceBand = ConfidenceBandClassifier.Classify(result.FinalConfidence),
                Reason = reason,
                IsAmbiguous = hasNaming && namingEvidence!.IsAmbiguous,
                DiscoveryMethods = discoveryMethods,
                SpEvidence = hasSpJoin ? spEvidence!.SpNames : [],
                MatchCount = hasSpJoin ? spEvidence!.SpNames.Count : 0
            });
        }

        // Sort by confidence descending
        candidates.Sort((a, b) => b.ConfidenceScore.CompareTo(a.ConfidenceScore));

        logger.LogInformation(
            "Detected {Total} logical FK candidates for project {ProjectId} " +
            "(SP JOIN: {SpCount}, Naming: {NamingCount}, Corroborated: {CorrCount})",
            candidates.Count, projectId,
            candidates.Count(c => c.DiscoveryMethods.Contains("SP_JOIN") && !c.DiscoveryMethods.Contains("NAME_CONVENTION")),
            candidates.Count(c => c.DiscoveryMethods.Contains("NAME_CONVENTION") && !c.DiscoveryMethods.Contains("SP_JOIN")),
            candidates.Count(c => c.DiscoveryMethods.Count > 1));

        return candidates;
    }

    public async Task<List<PhysicalFkDto>> GetPhysicalFksByTableAsync(int projectId, int tableId, CancellationToken cancellationToken = default)
    {
        return await logicalFkRepository.GetPhysicalFksByTableAsync(projectId, tableId, cancellationToken);
    }

    /// <summary>
    /// Detect candidates and persist them as SUGGESTED rows in LogicalForeignKeys.
    /// Uses INSERT + UPDATE (not MERGE) for safe concurrency.
    /// </summary>
    public async Task<int> DetectAndPersistCandidatesAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var candidates = await DetectCandidatesAsync(projectId, cancellationToken);

        if (candidates.Count == 0)
        {
            logger.LogInformation("No candidates to persist for project {ProjectId}", projectId);
            await logicalFkRepository.UpdateDetectionMetadataAsync(projectId, AlgorithmVersion, cancellationToken);
            return 0;
        }

        var affected = await logicalFkRepository.BulkUpsertSuggestedAsync(projectId, candidates, cancellationToken);
        await logicalFkRepository.UpdateDetectionMetadataAsync(projectId, AlgorithmVersion, cancellationToken);

        logger.LogInformation(
            "Persisted {Affected} candidates (of {Total} detected) for project {ProjectId} [AlgoVersion={Version}]",
            affected, candidates.Count, projectId, AlgorithmVersion);

        return affected;
    }

    /// <summary>
    /// Check if detection results are stale (need re-run).
    /// Stale when: algo version differs OR sync happened after last detection.
    /// </summary>
    public async Task<bool> IsDetectionStaleAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var metadata = await logicalFkRepository.GetDetectionMetadataAsync(projectId, cancellationToken);

        if (metadata == null) return true;
        if (metadata.LastDetectionRunAt == null) return true;
        if (metadata.DetectionAlgorithmVersion != AlgorithmVersion) return true;
        if (metadata.LastSyncAttempt > metadata.LastDetectionRunAt) return true;

        return false;
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

            // Use append-only method to avoid wiping other dependencies
            await dependencyRepository.AddDependenciesAsync(
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

    private static int? TryResolveTable(string prefix, Dictionary<string, int> tableNames)
        => TableNameResolver.TryResolveTable(prefix, tableNames);

    private static int? ResolveTableName(string name, Dictionary<string, int> tableNameToId)
        => TableNameResolver.ResolveTableName(name, tableNameToId);

    /// <summary>
    /// Resolve a column name within a specific table to its DetectionColumnInfo.
    /// </summary>
    private static DetectionColumnInfo? ResolveColumn(
        int tableId,
        string columnName,
        Dictionary<int, Dictionary<string, DetectionColumnInfo>> columnLookup)
    {
        if (!columnLookup.TryGetValue(tableId, out var tableCols))
            return null;

        if (tableCols.TryGetValue(columnName, out var col))
            return col;

        // Strip brackets
        var cleaned = columnName.Replace("[", "").Replace("]", "");
        if (tableCols.TryGetValue(cleaned, out col))
            return col;

        return null;
    }

    /// <summary>
    /// Build a human-readable reason string explaining why this candidate was detected.
    /// </summary>
    private static string BuildReason(
        DetectionColumnInfo sourceCol,
        DetectionColumnInfo targetCol,
        DetectionSignals signals,
        SpJoinEvidence? spEvidence,
        ConfidenceResult result)
    {
        var parts = new List<string>();

        if (signals.Corroborated)
        {
            parts.Add($"Corroborated: Column '{sourceCol.ColumnName}' matches naming convention " +
                       $"AND is joined to '{targetCol.TableName}.{targetCol.ColumnName}' " +
                       $"in {spEvidence!.SpNames.Count} SP(s): {string.Join(", ", spEvidence.SpNames)}");
        }
        else if (signals.SpJoinDetected)
        {
            parts.Add($"SP JOIN: '{sourceCol.TableName}.{sourceCol.ColumnName}' joined to " +
                       $"'{targetCol.TableName}.{targetCol.ColumnName}' " +
                       $"in {spEvidence!.SpNames.Count} SP(s): {string.Join(", ", spEvidence.SpNames)}");
        }
        else if (signals.NamingDetected)
        {
            parts.Add($"Naming convention: Column '{sourceCol.ColumnName}' matches " +
                       $"table '{targetCol.TableName}' key column '{targetCol.ColumnName}'");
        }

        if (!signals.TypeMatch)
        {
            parts.Add($"Type mismatch: {sourceCol.DataType} → {targetCol.DataType}");
        }

        if (result.CapsApplied.Length > 0)
        {
            parts.Add($"Caps applied: {string.Join(", ", result.CapsApplied)}");
        }

        return string.Join(". ", parts);
    }

    /// <summary>
    /// Score how well a target key column name matches the source FK naming prefix.
    /// Higher score wins within the same structural tier.
    /// </summary>
    private static int GetNamingScore(string sourcePrefix, string targetColumnName)
    {
        // Score 3: Canonical PK name "id" — strongest signal (e.g., Customers.id)
        if (targetColumnName.Equals("id", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        // Score 2: Table-prefixed FK pattern — target matches sourcePrefix+"id" or sourcePrefix+"_id"
        // e.g., source column "customer_id" with prefix "customer" matches target "customer_id" or "customerid"
        if (targetColumnName.Equals(sourcePrefix + "id", StringComparison.OrdinalIgnoreCase) ||
            targetColumnName.Equals(sourcePrefix + "_id", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        // Score 1: Generic *Id column — ends with "id" but no prefix match; weaker signal
        if (targetColumnName.EndsWith("id", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        // Score 0: No naming evidence — column name doesn't suggest an FK relationship
        return 0;
    }

    private static bool DataTypesCompatible(string sourceType, string targetType)
        => DataTypeCompatibility.AreCompatible(sourceType, targetType);

    #endregion

    #region Internal Models

    /// <summary>Evidence from SP JOIN analysis for a single candidate</summary>
    private sealed class SpJoinEvidence
    {
        public required DetectionColumnInfo SourceCol { get; set; }
        public required DetectionColumnInfo TargetCol { get; set; }
        public required List<string> SpNames { get; set; }
    }

    /// <summary>Evidence from naming convention analysis for a single candidate</summary>
    private sealed class NamingEvidence
    {
        public required DetectionColumnInfo SourceCol { get; set; }
        public required DetectionColumnInfo TargetCol { get; set; }
        public bool IsAmbiguous { get; set; }
    }

    #endregion
}


