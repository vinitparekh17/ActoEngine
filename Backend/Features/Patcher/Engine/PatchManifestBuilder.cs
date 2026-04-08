using ActoEngine.WebApi.Features.ImpactAnalysis;
using ActoEngine.WebApi.Features.Schema;
using System.Text.RegularExpressions;

namespace ActoEngine.WebApi.Features.Patcher.Engine;

/// <summary>
/// Builds a <see cref="PatchManifest"/> by walking approved stored-procedure
/// mappings, resolving persisted + live dependencies, and collecting table/column
/// snapshots.  Pure data-assembly — no I/O besides repository calls.
/// </summary>
public sealed partial class PatchManifestBuilder(
    IPatcherRepository patcherRepo,
    IPageMappingRepository pageMappingRepo,
    ISchemaRepository schemaRepo,
    IDependencyAnalysisService dependencyAnalysisService)
{
    private static readonly HashSet<string> s_allowedReferentialActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "NO ACTION",
        "CASCADE",
        "SET NULL",
        "SET DEFAULT"
    };

    // ──────────────────────────────────────────────
    //  Public entry point
    // ──────────────────────────────────────────────

    public async Task<PatchManifest> BuildAsync(PatchGenerationRequest request, CancellationToken ct)
    {
        var warnings = new List<string>();
        var blockers = new List<string>();
        var pages = request.PageMappings.Select(m => new PatchManifestPage
        {
            DomainName = m.DomainName,
            PageName = m.PageName,
            IsNewPage = m.IsNewPage
        }).ToList();

        var allStoredProcedures = await schemaRepo.GetStoredProceduresListAsync(request.ProjectId);
        var storedProcedureMap = allStoredProcedures.ToDictionary(
            sp => NormalizeQualifiedName(sp.SchemaName, sp.ProcedureName),
            sp => sp,
            StringComparer.OrdinalIgnoreCase);
        var storedProcedureById = allStoredProcedures.ToDictionary(sp => sp.SpId);
        var allTables = await schemaRepo.GetStoredTablesAsync(request.ProjectId) ?? [];
        var tableLookup = BuildTableLookup(allTables);
        var tableColumnsListCache = new Dictionary<int, List<ColumnMetadataDto>>();
        var tableColumnsLookupCache = new Dictionary<int, Dictionary<string, ColumnMetadataDto>>();

        var procedureSnapshots = new Dictionary<int, PatchProcedureSnapshot>();
        var requiredColumnsByTable = new Dictionary<int, HashSet<string>>();
        var rootQueue = new Queue<(int SpId, bool IsShared, string SourcePage)>();

        var requestedPages = pages.Select(page => new PatchPageEntry
        {
            DomainName = page.DomainName,
            PageName = page.PageName,
            IsNewPage = page.IsNewPage
        }).ToList();
        var approvedRows = await pageMappingRepo.GetApprovedStoredProceduresByPagesAsync(request.ProjectId, requestedPages, ct);
        var approvedByPage = approvedRows
            .GroupBy(row => PatcherRepository.BuildPageKey(row.DomainName, row.PageName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in request.PageMappings)
        {
            var pageKey = PatcherRepository.BuildPageKey(mapping.DomainName, mapping.PageName);
            if (!approvedByPage.TryGetValue(pageKey, out var approvedSps) || approvedSps.Count == 0)
            {
                warnings.Add($"No approved mappings for page '{mapping.DomainName}/{mapping.PageName}'.");
                continue;
            }

            foreach (var approved in approvedSps)
            {
                if (!TryResolveStoredProcedure(storedProcedureMap, approved.StoredProcedure, out var sp))
                {
                    blockers.Add($"Approved stored procedure '{approved.StoredProcedure}' was not found in synced metadata. Re-sync the project before generating a patch.");
                    continue;
                }

                rootQueue.Enqueue((
                    sp.SpId,
                    approved.MappingType.Equals(PageMappingConstants.MappingTypeShared, StringComparison.OrdinalIgnoreCase),
                    $"{mapping.DomainName}/{mapping.PageName}"));
            }
        }

        while (rootQueue.Count > 0)
        {
            var batch = new List<(int SpId, bool IsShared, string SourcePage)>(rootQueue.Count);
            while (rootQueue.Count > 0)
            {
                batch.Add(rootQueue.Dequeue());
            }

            var pending = new List<(int SpId, bool IsShared, string SourcePage)>();
            foreach (var candidate in batch)
            {
                if (procedureSnapshots.TryGetValue(candidate.SpId, out var existingSnapshot))
                {
                    if (!candidate.IsShared && existingSnapshot.IsShared)
                    {
                        existingSnapshot.IsShared = false;
                        foreach (var dependencyId in existingSnapshot.DependencyProcedureIds)
                        {
                            rootQueue.Enqueue((dependencyId, false, candidate.SourcePage));
                        }
                    }

                    continue;
                }

                pending.Add(candidate);
            }

            if (pending.Count == 0)
            {
                continue;
            }

            var pendingSpIds = pending.Select(item => item.SpId).Distinct().ToList();
            var spDetailsById = await patcherRepo.GetStoredProceduresByIdsAsync(pendingSpIds, ct);
            var persistedProcedureDependenciesBySp = await patcherRepo.GetSpProcedureDependenciesAsync(request.ProjectId, pendingSpIds, ct);
            var persistedTableDependenciesBySp = await patcherRepo.GetSpOutboundDependenciesAsync(request.ProjectId, pendingSpIds, ct);
            var persistedColumnDependenciesBySp = await patcherRepo.GetSpColumnDependenciesAsync(request.ProjectId, pendingSpIds, ct);

            foreach (var pendingItem in pending)
            {
                if (procedureSnapshots.TryGetValue(pendingItem.SpId, out var existingSnapshot))
                {
                    if (!pendingItem.IsShared && existingSnapshot.IsShared)
                    {
                        existingSnapshot.IsShared = false;
                        foreach (var dependencyId in existingSnapshot.DependencyProcedureIds)
                        {
                            rootQueue.Enqueue((dependencyId, false, pendingItem.SourcePage));
                        }
                    }

                    continue;
                }

                if (!storedProcedureById.TryGetValue(pendingItem.SpId, out var storedProcedure))
                {
                    blockers.Add($"Stored procedure id '{pendingItem.SpId}' is missing from synced metadata. Re-sync the project before generating a patch.");
                    continue;
                }

                if (!spDetailsById.TryGetValue(pendingItem.SpId, out var spDetail))
                {
                    blockers.Add($"Stored procedure '{NormalizeQualifiedName(storedProcedure.SchemaName, storedProcedure.ProcedureName)}' is missing its full definition in metadata. Re-sync the project.");
                    continue;
                }

                var persistedProcedureDependencies = persistedProcedureDependenciesBySp.TryGetValue(pendingItem.SpId, out var procedureDeps)
                    ? procedureDeps
                    : [];
                var persistedTableDependencies = persistedTableDependenciesBySp.TryGetValue(pendingItem.SpId, out var tableDeps)
                    ? tableDeps
                    : [];
                var persistedColumnDependencies = persistedColumnDependenciesBySp.TryGetValue(pendingItem.SpId, out var columnDeps)
                    ? columnDeps
                    : [];

                var liveDependencies = dependencyAnalysisService.ExtractDependencies(spDetail.Definition, pendingItem.SpId, "SP");
                var liveProcedureDependencies = ResolveLiveProcedureDependencies(liveDependencies, storedProcedureMap);
                var liveTableDependencies = ResolveLiveTableDependencies(liveDependencies, tableLookup);
                var liveColumnDependencies = await ResolveLiveColumnDependenciesAsync(
                    liveDependencies,
                    tableLookup,
                    tableColumnsLookupCache,
                    tableColumnsListCache);
                var hasDynamicSql = HasDynamicSql(spDetail.Definition);

                if (persistedProcedureDependencies.Count == 0 && liveProcedureDependencies.Count != 0)
                {
                    warnings.Add($"Stored procedure '{NormalizeQualifiedName(spDetail.SchemaName, spDetail.ProcedureName)}' is using live SP dependency parsing because no persisted SP dependency metadata was found.");
                }

                if (persistedTableDependencies.Count == 0 && liveTableDependencies.Count != 0)
                {
                    warnings.Add($"Stored procedure '{NormalizeQualifiedName(spDetail.SchemaName, spDetail.ProcedureName)}' is using live table dependency parsing because no persisted table dependency metadata was found.");
                }

                if (persistedColumnDependencies.Count == 0 && liveColumnDependencies.Count != 0)
                {
                    warnings.Add($"Stored procedure '{NormalizeQualifiedName(spDetail.SchemaName, spDetail.ProcedureName)}' is using live column dependency parsing because no persisted column dependency metadata was found.");
                }

                var procedureDependencies = MergeProcedureDependencies(persistedProcedureDependencies, liveProcedureDependencies);
                var tableDependencies = MergeTableDependencies(persistedTableDependencies, liveTableDependencies);
                var columnDependencies = MergeColumnDependencies(persistedColumnDependencies, liveColumnDependencies);

                if (hasDynamicSql)
                {
                    var procedureName = NormalizeQualifiedName(spDetail.SchemaName, spDetail.ProcedureName);
                    var dynamicTables = ResolveDynamicSqlTableDependencies(spDetail.Definition, tableLookup);
                    if (dynamicTables.Count != 0)
                    {
                        tableDependencies = MergeTableDependencies(tableDependencies, dynamicTables);
                    }

                    warnings.Add($"Stored procedure '{procedureName}' contains dynamic SQL. Patch generation is continuing with best-effort dependency metadata; validate compatibility.sql manually before deployment.");

                    if (procedureDependencies.Count == 0 && tableDependencies.Count == 0 && columnDependencies.Count == 0)
                    {
                        warnings.Add($"Stored procedure '{procedureName}' contains dynamic SQL and no resolved dependencies were found in metadata. Runtime objects referenced inside the dynamic statement will not be validated automatically.");
                    }
                }

                var snapshot = new PatchProcedureSnapshot
                {
                    SpId = spDetail.SpId,
                    ProcedureName = spDetail.ProcedureName,
                    SchemaName = spDetail.SchemaName ?? "dbo",
                    Definition = spDetail.Definition,
                    IsShared = pendingItem.IsShared,
                    HasDynamicSql = hasDynamicSql,
                    DependencyProcedureIds = [.. procedureDependencies.Select(d => d.SpId).Distinct()],
                    TableIds = [.. tableDependencies.Select(d => d.TableId).Distinct()],
                    ColumnIds = [.. columnDependencies.Select(d => d.ColumnId).Distinct()]
                };

                procedureSnapshots[snapshot.SpId] = snapshot;

                foreach (var procedureDependency in procedureDependencies)
                {
                    StoredProcedureListDto? nestedProcedure = null;
                    if (storedProcedureById.TryGetValue(procedureDependency.SpId, out var nestedById))
                    {
                        nestedProcedure = nestedById;
                    }
                    else if (TryResolveStoredProcedure(
                        storedProcedureMap,
                        NormalizeQualifiedName(procedureDependency.SchemaName, procedureDependency.ProcedureName),
                        out var nestedByName))
                    {
                        nestedProcedure = nestedByName;
                    }

                    if (nestedProcedure == null)
                    {
                        blockers.Add($"Nested stored procedure '{NormalizeQualifiedName(procedureDependency.SchemaName, procedureDependency.ProcedureName)}' was found in dependencies but is missing from synced metadata.");
                        continue;
                    }

                    rootQueue.Enqueue((nestedProcedure.SpId, snapshot.IsShared, pendingItem.SourcePage));
                }

                foreach (var tableDependency in tableDependencies)
                {
                    if (!requiredColumnsByTable.ContainsKey(tableDependency.TableId))
                    {
                        requiredColumnsByTable[tableDependency.TableId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }
                }

                foreach (var columnDependency in columnDependencies)
                {
                    if (!requiredColumnsByTable.TryGetValue(columnDependency.TableId, out var columns))
                    {
                        columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        requiredColumnsByTable[columnDependency.TableId] = columns;
                    }

                    columns.Add(columnDependency.ColumnName);
                }
            }
        }

        var tableSnapshots = new List<PatchTableSnapshot>();
        var allTableIds = procedureSnapshots.Values.SelectMany(p => p.TableIds)
            .Union(requiredColumnsByTable.Keys)
            .Distinct();
        var orderedTableIds = allTableIds.ToList();
        var tablesById = await patcherRepo.GetTablesByIdsAsync(orderedTableIds, ct);
        var columnsByTableId = await patcherRepo.GetColumnsByTableIdsAsync(orderedTableIds, ct);
        var indexesByTableId = await patcherRepo.GetIndexesByTableIdsAsync(orderedTableIds, ct);
        var foreignKeysByTableId = await patcherRepo.GetForeignKeysByTableIdsAsync(orderedTableIds, ct);

        foreach (var tableId in orderedTableIds)
        {
            if (!tablesById.TryGetValue(tableId, out var tableMetadata))
            {
                blockers.Add($"Table dependency '{tableId}' is missing from synced metadata. Re-sync the project before generating a patch.");
                continue;
            }

            var columns = columnsByTableId.TryGetValue(tableId, out var tableColumns)
                ? tableColumns
                : [];
            if (columns.Count == 0)
            {
                blockers.Add($"Table '{NormalizeQualifiedName(tableMetadata.SchemaName, tableMetadata.TableName)}' has no stored column metadata. Re-sync the project before generating a patch.");
                continue;
            }

            // Always treat every synced column as required.
            // Partial column-dependency tracking (persisted + live analysis) misses unqualified
            // column references in SP bodies (e.g. "SET IsReturn = @v" with no table alias),
            // so if we used only the tracked subset we would silently exclude columns like
            // IsReturn from missing-column detection and repair.  Using the full column list
            // is conservative but correct: CanRepairMissingColumn still guards which missing
            // columns can be auto-added vs flagged as blockers.
            List<string> requiredColumns = [.. columns.Select(c => c.ColumnName)];

            tableSnapshots.Add(new PatchTableSnapshot
            {
                TableId = tableMetadata.TableId,
                TableName = tableMetadata.TableName,
                SchemaName = tableMetadata.SchemaName ?? "dbo",
                Columns = [.. columns.Select(c => new PatchColumnSnapshot
                {
                    ColumnId = c.ColumnId,
                    ColumnName = c.ColumnName,
                    DataType = c.DataType,
                    MaxLength = c.MaxLength,
                    Precision = c.Precision,
                    Scale = c.Scale,
                    IsNullable = c.IsNullable,
                    IsPrimaryKey = c.IsPrimaryKey,
                    IsIdentity = c.IsIdentity,
                    DefaultValue = c.DefaultValue
                })],
                Indexes = [.. (indexesByTableId.TryGetValue(tableId, out var indexRows) ? indexRows : [])
                    .Select(i => new PatchIndexSnapshot
                    {
                        IndexName = i.IndexName,
                        IsUnique = i.IsUnique,
                        IsPrimaryKey = i.IsPrimaryKey,
                        Columns = [.. i.Columns.Select(c => new PatchIndexColumnSnapshot
                        {
                            ColumnId = c.ColumnId,
                            ColumnName = c.ColumnName,
                            ColumnOrder = c.ColumnOrder
                        })]
                    })],
                ForeignKeys = [.. (foreignKeysByTableId.TryGetValue(tableId, out var foreignKeyRows) ? foreignKeyRows : [])
                    .Select(f => new PatchForeignKeySnapshot
                    {
                        ColumnName = f.ColumnName,
                        ReferencedTableName = f.ReferencedTableName,
                        ReferencedSchemaName = f.ReferencedSchemaName,
                        ReferencedColumnName = f.ReferencedColumnName,
                        ForeignKeyName = f.ForeignKeyName,
                        OnDeleteAction = NormalizeReferentialAction(f.OnDeleteAction),
                        OnUpdateAction = NormalizeReferentialAction(f.OnUpdateAction)
                    })],
                RequiredColumnNames = requiredColumns
            });
        }

        return new PatchManifest
        {
            ProjectId = request.ProjectId,
            Pages = pages,
            Procedures = OrderProceduresByDependencies([.. procedureSnapshots.Values]),
            Tables = [.. tableSnapshots.OrderBy(t => t.SchemaName).ThenBy(t => t.TableName)],
            Warnings = [.. warnings.Distinct(StringComparer.OrdinalIgnoreCase)],
            BlockingIssues = [.. blockers.Distinct(StringComparer.OrdinalIgnoreCase)],
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    // ──────────────────────────────────────────────
    //  Procedure ordering (topological sort)
    // ──────────────────────────────────────────────

    private static List<PatchProcedureSnapshot> OrderProceduresByDependencies(List<PatchProcedureSnapshot> procedures)
    {
        var procedureMap = procedures.ToDictionary(p => p.SpId);
        var ordered = new List<PatchProcedureSnapshot>();
        var visiting = new HashSet<int>();
        var visited = new HashSet<int>();

        foreach (var procedure in procedures.OrderBy(p => p.IsShared).ThenBy(p => p.SchemaName).ThenBy(p => p.ProcedureName))
        {
            VisitProcedure(procedure, procedureMap, ordered, visiting, visited);
        }

        return ordered;
    }

    private static void VisitProcedure(
        PatchProcedureSnapshot procedure,
        IReadOnlyDictionary<int, PatchProcedureSnapshot> procedureMap,
        List<PatchProcedureSnapshot> ordered,
        HashSet<int> visiting,
        HashSet<int> visited)
    {
        if (visited.Contains(procedure.SpId))
        {
            return;
        }

        if (!visiting.Add(procedure.SpId))
        {
            return;
        }

        foreach (var dependencyId in procedure.DependencyProcedureIds)
        {
            if (procedureMap.TryGetValue(dependencyId, out var dependency))
            {
                VisitProcedure(dependency, procedureMap, ordered, visiting, visited);
            }
        }

        visiting.Remove(procedure.SpId);
        visited.Add(procedure.SpId);
        ordered.Add(procedure);
    }

    // ──────────────────────────────────────────────
    //  Dependency resolution helpers
    // ──────────────────────────────────────────────

    private static bool TryResolveStoredProcedure(
        IReadOnlyDictionary<string, StoredProcedureListDto> storedProcedureMap,
        string serviceName,
        out StoredProcedureListDto storedProcedure)
    {
        if (storedProcedureMap.TryGetValue(serviceName, out storedProcedure!))
        {
            return true;
        }

        var cleaned = NormalizeQualifiedName(null, serviceName);
        if (storedProcedureMap.TryGetValue(cleaned, out storedProcedure!))
        {
            return true;
        }

        return false;
    }

    internal static string NormalizeQualifiedName(string? schemaName, string objectName)
    {
        var cleanedObject = objectName.Replace("[", "").Replace("]", "");
        if (cleanedObject.Contains('.'))
        {
            var parts = cleanedObject.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return $"{parts[^2]}.{parts[^1]}";
            }
        }

        return $"{(string.IsNullOrWhiteSpace(schemaName) ? "dbo" : schemaName)}.{cleanedObject}";
    }

    /// <summary>
    /// Detects dynamic SQL patterns (sp_executesql, EXEC(@var), EXEC(...)).
    /// NOTE: May produce false positives for EXEC('literal string') or EXEC(N'...')
    /// where the argument is a string literal rather than a variable.
    /// </summary>
    private static bool HasDynamicSql(string definition)
    {
        return DynamicSqlRegex().IsMatch(definition);
    }

    private static Dictionary<string, TableMetadataDto> BuildTableLookup(IEnumerable<TableMetadataDto> tables)
    {
        tables ??= [];
        var lookup = new Dictionary<string, TableMetadataDto>(StringComparer.OrdinalIgnoreCase);
        var groupedByName = tables.GroupBy(t => t.TableName, StringComparer.OrdinalIgnoreCase);

        foreach (var table in tables)
        {
            lookup[NormalizeQualifiedName(table.SchemaName, table.TableName)] = table;
        }

        foreach (var group in groupedByName)
        {
            if (group.Count() == 1)
            {
                lookup[group.Key] = group.First();
                continue;
            }

            var dboTable = group.FirstOrDefault(t => string.Equals(t.SchemaName, "dbo", StringComparison.OrdinalIgnoreCase));
            if (dboTable != null)
            {
                lookup[group.Key] = dboTable;
            }
        }

        return lookup;
    }

    private static List<SpProcedureDependencyRow> ResolveLiveProcedureDependencies(
        IEnumerable<Dependency> liveDependencies,
        IReadOnlyDictionary<string, StoredProcedureListDto> storedProcedureMap)
    {
        return [.. liveDependencies
            .Where(d => d.TargetType == "SP")
            .Select(d => TryResolveStoredProcedure(storedProcedureMap, d.TargetName, out var sp) ? new SpProcedureDependencyRow
            {
                SpId = sp.SpId,
                ProcedureName = sp.ProcedureName,
                SchemaName = sp.SchemaName
            } : null)
            .Where(d => d != null)
            .GroupBy(d => d!.SpId)
            .Select(g => g.First()!)];
    }

    private static List<SpTableDependencyRow> ResolveLiveTableDependencies(
        IEnumerable<Dependency> liveDependencies,
        IReadOnlyDictionary<string, TableMetadataDto> tableLookup)
    {
        return [.. liveDependencies
            .Where(d => d.TargetType == "TABLE")
            .Select(d => TryResolveTable(tableLookup, d.TargetName, out var table) ? new SpTableDependencyRow
            {
                TableId = table.TableId,
                TableName = table.TableName,
                SchemaName = table.SchemaName
            } : null)
            .Where(d => d != null)
            .GroupBy(d => d!.TableId)
            .Select(g => g.First()!)];
    }

    private async Task<List<SpColumnDependencyRow>> ResolveLiveColumnDependenciesAsync(
        IEnumerable<Dependency> liveDependencies,
        IReadOnlyDictionary<string, TableMetadataDto> tableLookup,
        Dictionary<int, Dictionary<string, ColumnMetadataDto>> tableColumnsLookupCache,
        Dictionary<int, List<ColumnMetadataDto>> tableColumnsListCache)
    {
        var resolved = new List<SpColumnDependencyRow>();

        foreach (var dependency in liveDependencies.Where(d => d.TargetType == "COLUMN"))
        {
            var parsed = TrySplitColumnReference(dependency.TargetName);
            if (parsed == null)
            {
                continue;
            }

            if (!TryResolveTable(tableLookup, parsed.Value.TableReference, out var table))
            {
                continue;
            }

            if (!tableColumnsLookupCache.TryGetValue(table.TableId, out var columnLookup))
            {
                if (!tableColumnsListCache.TryGetValue(table.TableId, out var columns))
                {
                    columns = await schemaRepo.GetStoredColumnsAsync(table.TableId);
                    tableColumnsListCache[table.TableId] = columns;
                }

                columnLookup = columns.ToDictionary(column => column.ColumnName, StringComparer.OrdinalIgnoreCase);
                tableColumnsLookupCache[table.TableId] = columnLookup;
            }

            if (!columnLookup.TryGetValue(parsed.Value.ColumnName, out var column))
            {
                continue;
            }

            resolved.Add(new SpColumnDependencyRow
            {
                ColumnId = column.ColumnId,
                ColumnName = column.ColumnName,
                TableId = table.TableId,
                TableName = table.TableName,
                SchemaName = table.SchemaName
            });
        }

        return [.. resolved
            .GroupBy(c => c.ColumnId)
            .Select(g => g.First())];
    }

    private static List<SpTableDependencyRow> ResolveDynamicSqlTableDependencies(
        string definition,
        IReadOnlyDictionary<string, TableMetadataDto> tableLookup)
    {
        var resolved = new List<SpTableDependencyRow>();
        foreach (var tableName in ExtractDynamicSqlTableReferences(definition))
        {
            if (!TryResolveTable(tableLookup, tableName, out var table))
            {
                continue;
            }

            resolved.Add(new SpTableDependencyRow
            {
                TableId = table.TableId,
                TableName = table.TableName,
                SchemaName = table.SchemaName
            });
        }

        return [.. resolved
            .GroupBy(t => t.TableId)
            .Select(g => g.First())];
    }

    private static IEnumerable<string> ExtractDynamicSqlTableReferences(string definition)
    {
        var normalized = definition.Replace("''", "'").Replace("[", string.Empty).Replace("]", string.Empty);
        foreach (Match tableMatch in DynamicSqlTableRegex().Matches(normalized))
        {
            if (tableMatch.Groups.Count > 1)
            {
                yield return tableMatch.Groups[1].Value;
            }
        }
    }

    // ──────────────────────────────────────────────
    //  Merge helpers
    // ──────────────────────────────────────────────

    private static List<SpProcedureDependencyRow> MergeProcedureDependencies(
        IEnumerable<SpProcedureDependencyRow> first,
        IEnumerable<SpProcedureDependencyRow> second)
    {
        return [.. first.Concat(second)
            .GroupBy(d => d.SpId)
            .Select(g => g.First())];
    }

    private static List<SpTableDependencyRow> MergeTableDependencies(
        IEnumerable<SpTableDependencyRow> first,
        IEnumerable<SpTableDependencyRow> second)
    {
        return [.. first.Concat(second)
            .GroupBy(d => d.TableId)
            .Select(g => g.First())];
    }

    private static List<SpColumnDependencyRow> MergeColumnDependencies(
        IEnumerable<SpColumnDependencyRow> first,
        IEnumerable<SpColumnDependencyRow> second)
    {
        return [.. first.Concat(second)
            .GroupBy(d => d.ColumnId)
            .Select(g => g.First())];
    }

    private static bool TryResolveTable(
        IReadOnlyDictionary<string, TableMetadataDto> tableLookup,
        string tableReference,
        out TableMetadataDto table)
    {
        if (tableLookup.TryGetValue(tableReference.Replace("[", "").Replace("]", ""), out table!))
        {
            return true;
        }

        var normalized = NormalizeQualifiedName(null, tableReference);
        return tableLookup.TryGetValue(normalized, out table!);
    }

    private static (string TableReference, string ColumnName)? TrySplitColumnReference(string reference)
    {
        var cleaned = reference.Replace("[", "").Replace("]", "");
        var parts = cleaned.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        var columnName = parts[^1];
        var tableReference = string.Join('.', parts.Take(parts.Length - 1));
        return (tableReference, columnName);
    }

    private static string NormalizeReferentialAction(string action)
    {
        var normalized = action?.Trim().ToUpperInvariant() ?? "NO ACTION";
        return s_allowedReferentialActions.Contains(normalized) ? normalized : "NO ACTION";
    }

    // ──────────────────────────────────────────────
    //  Regex patterns
    // ──────────────────────────────────────────────

    [GeneratedRegex(@"\bsp_executesql\b|\bexec(?:ute)?\s*\(|\bexec(?:ute)?\s+@\w+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DynamicSqlRegex();
    [GeneratedRegex(@"\b(?:from|join|update|into|delete\s+from)\s+((?:\[[^\]]+\]|\w+)(?:\.(?:\[[^\]]+\]|\w+))?)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DynamicSqlTableRegex();
}
