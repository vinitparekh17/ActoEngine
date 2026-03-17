using ActoEngine.WebApi.Features.ImpactAnalysis;
using ActoEngine.WebApi.Features.Projects;
using ActoEngine.WebApi.Features.Schema;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace ActoEngine.WebApi.Features.Patcher;

public interface IPatcherService
{
    Task<List<PagePatchStatus>> CheckPatchStatusAsync(PatchStatusRequest request, CancellationToken ct = default);
    Task<PatchGenerationResponse> GeneratePatchAsync(PatchGenerationRequest request, int? userId = null, CancellationToken ct = default);
}

public partial class PatcherService(
    IPatcherRepository patcherRepo,
    IPageMappingRepository pageMappingRepo,
    IProjectRepository projectRepo,
    IDependencyAnalysisService dependencyAnalysisService,
    ISchemaRepository schemaRepo,
    IPatchScriptRenderer scriptRenderer,
    ILogger<PatcherService> log) : IPatcherService
{
    private readonly IPatchScriptRenderer _scriptRenderer = scriptRenderer;

    public async Task<List<PagePatchStatus>> CheckPatchStatusAsync(PatchStatusRequest request, CancellationToken ct = default)
    {
        var config = await patcherRepo.GetPatchConfigAsync(request.ProjectId, ct)
            ?? throw new InvalidOperationException("Patch configuration not found for this project.");

        ValidateConfig(config);

        var statuses = new List<PagePatchStatus>();

        foreach (var mapping in request.PageMappings)
        {
            var status = new PagePatchStatus
            {
                PageName = mapping.PageName,
                DomainName = mapping.DomainName,
                NeedsRegeneration = true,
                Reason = "No existing patch found"
            };

            var approvedSps = await GetApprovedStoredProceduresAsync(
                request.ProjectId,
                mapping.DomainName,
                mapping.PageName,
                ct);
            if (!approvedSps.Any())
            {
                status.NeedsRegeneration = false;
                status.Reason = "No approved mappings found for this page";
                statuses.Add(status);
                continue;
            }

            var latestPatch = await patcherRepo.GetLatestPatchAsync(
                request.ProjectId, mapping.DomainName, mapping.PageName, ct);

            if (latestPatch != null)
            {
                status.LastPatchDate = latestPatch.GeneratedAt;

                var jsPath = BuildFilePath(config.ProjectRootPath!, config.ScriptDirPath!, mapping.DomainName, $"{mapping.PageName}.js");
                var cshtmlPath = BuildFilePath(config.ProjectRootPath!, config.ViewDirPath!, mapping.DomainName, $"{mapping.PageName}.cshtml");

                var lastModified = GetLatestModifiedDate(jsPath, cshtmlPath);
                status.FileLastModified = lastModified;

                if (lastModified.HasValue && lastModified.Value > latestPatch.GeneratedAt)
                {
                    status.NeedsRegeneration = true;
                    status.Reason = "Source files modified since last patch";
                }
                else
                {
                    status.NeedsRegeneration = false;
                    status.Reason = "Patch is up to date";
                }
            }

            statuses.Add(status);
        }

        return statuses;
    }

    public async Task<PatchGenerationResponse> GeneratePatchAsync(PatchGenerationRequest request, int? userId = null, CancellationToken ct = default)
    {
        _ = await projectRepo.GetByIdAsync(request.ProjectId)
            ?? throw new InvalidOperationException($"Project with ID {request.ProjectId} not found.");

        var config = await patcherRepo.GetPatchConfigAsync(request.ProjectId, ct)
            ?? throw new InvalidOperationException("Patch configuration not found for this project.");

        ValidateConfig(config);

        var manifest = await BuildManifestAsync(request, ct);
        if (manifest.BlockingIssues.Count != 0)
        {
            throw new InvalidOperationException(
                "Patch generation blocked because dependency metadata is incomplete or unsafe:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, manifest.BlockingIssues.Select(issue => $"- {issue}")));
        }

        var artifacts = _scriptRenderer.Render(manifest);
        var warnings = new List<string>(manifest.Warnings);
        var filesIncluded = new List<string>();

        if (request.PageMappings == null || request.PageMappings.Count == 0)
        {
            throw new InvalidOperationException("At least one page mapping is required.");
        }

        var firstMapping = request.PageMappings[0];
        var timestamp = DateTime.UtcNow.ToString("dd-MM-yyyy_HH-mm-ss");
        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var mapping in request.PageMappings)
            {
                var cshtmlPath = BuildFilePath(config.ProjectRootPath!, config.ViewDirPath!, mapping.DomainName, $"{mapping.PageName}.cshtml");
                if (File.Exists(cshtmlPath))
                {
                    var entryPath = Path.GetRelativePath(config.ProjectRootPath!, cshtmlPath).Replace('\\', '/');
                    await AddFileToArchiveAsync(archive, entryPath, cshtmlPath, ct);
                    filesIncluded.Add(entryPath);
                }
                else
                {
                    warnings.Add($"View file not found: {cshtmlPath}");
                }

                var jsPath = BuildFilePath(config.ProjectRootPath!, config.ScriptDirPath!, mapping.DomainName, $"{mapping.PageName}.js");
                if (File.Exists(jsPath))
                {
                    var entryPath = Path.GetRelativePath(config.ProjectRootPath!, jsPath).Replace('\\', '/');
                    await AddFileToArchiveAsync(archive, entryPath, jsPath, ct);
                    filesIncluded.Add(entryPath);
                }
                else
                {
                    warnings.Add($"Script file not found: {jsPath}");
                }

                if (mapping.IsNewPage)
                {
                    var menuSql = GenerateMenuPermissionSql(mapping.PageName, mapping.DomainName);
                    var menuEntry = $"sql/{mapping.DomainName}_{mapping.PageName}_{timestamp}/menu_permission.sql";
                    AddTextToArchive(archive, menuEntry, menuSql);
                    filesIncluded.Add(menuEntry);
                }
            }

            var sqlDir = $"sql/{firstMapping.DomainName}_{firstMapping.PageName}_{timestamp}";

            var compatibilityEntry = $"{sqlDir}/compatibility.sql";
            AddTextToArchive(archive, compatibilityEntry, artifacts.CompatibilitySql);
            filesIncluded.Add(compatibilityEntry);

            var updateEntry = $"{sqlDir}/update.sql";
            AddTextToArchive(archive, updateEntry, artifacts.UpdateSql);
            filesIncluded.Add(updateEntry);

            var rollbackEntry = $"{sqlDir}/rollback.sql";
            AddTextToArchive(archive, rollbackEntry, artifacts.RollbackSql);
            filesIncluded.Add(rollbackEntry);

            var manifestEntry = $"{sqlDir}/manifest.json";
            AddTextToArchive(archive, manifestEntry, artifacts.ManifestJson);
            filesIncluded.Add(manifestEntry);
        }

        var zipFileName = $"patch_{firstMapping.DomainName}_{firstMapping.PageName}_{timestamp}.zip";
        Directory.CreateDirectory(config.PatchDownloadPath!);
        var zipFilePath = Path.Combine(config.PatchDownloadPath!, zipFileName);

        zipStream.Position = 0;
        await using (var fileStream = new FileStream(zipFilePath, FileMode.Create))
        {
            await zipStream.CopyToAsync(fileStream, ct);
        }

        var spNamesList = manifest.Procedures.Select(s => s.ProcedureName).ToList();
        var pages = request.PageMappings.Select(m => new PatchPageEntry
        {
            DomainName = m.DomainName,
            PageName = m.PageName,
            IsNewPage = m.IsNewPage
        }).ToList();

        var patchId = await patcherRepo.SavePatchHistoryAsync(new PatchHistoryRecord
        {
            ProjectId = request.ProjectId,
            PageName = firstMapping.PageName,
            DomainName = firstMapping.DomainName,
            SpNames = System.Text.Json.JsonSerializer.Serialize(spNamesList),
            IsNewPage = firstMapping.IsNewPage,
            PatchFilePath = zipFilePath,
            GeneratedBy = userId,
            Status = "Generated"
        }, pages, ct);

        log.LogInformation(
            "Generated patch {PatchId} for {Domain}/{Page} with {SpCount} procedures at {Path}",
            patchId,
            firstMapping.DomainName,
            firstMapping.PageName,
            manifest.Procedures.Count,
            zipFilePath);

        return new PatchGenerationResponse
        {
            PatchId = patchId,
            DownloadPath = zipFilePath,
            FilesIncluded = filesIncluded,
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            GeneratedAt = DateTime.UtcNow
        };
    }

    private async Task<PatchManifest> BuildManifestAsync(PatchGenerationRequest request, CancellationToken ct)
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
        var tableColumnsCache = new Dictionary<int, List<ColumnMetadataDto>>();

        var procedureSnapshots = new Dictionary<int, PatchProcedureSnapshot>();
        var requiredColumnsByTable = new Dictionary<int, HashSet<string>>();
        var rootQueue = new Queue<(StoredProcedureListDto Sp, bool IsShared, string SourcePage)>();

        foreach (var mapping in request.PageMappings)
        {
            var approvedSps = await GetApprovedStoredProceduresAsync(
                request.ProjectId,
                mapping.DomainName,
                mapping.PageName,
                ct);

            if (!approvedSps.Any())
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
                    sp,
                    approved.MappingType.Equals(PageMappingConstants.MappingTypeShared, StringComparison.OrdinalIgnoreCase),
                    $"{mapping.DomainName}/{mapping.PageName}"));
            }
        }

        while (rootQueue.Count > 0)
        {
            var current = rootQueue.Dequeue();
            if (procedureSnapshots.TryGetValue(current.Sp.SpId, out var existing))
            {
                if (!current.IsShared && existing.IsShared)
                {
                    existing.IsShared = false;
                    foreach (var dependencyId in existing.DependencyProcedureIds)
                    {
                        if (storedProcedureById.TryGetValue(dependencyId, out var nested))
                        {
                            rootQueue.Enqueue((nested, false, current.SourcePage));
                        }
                    }
                }
                continue;
            }

            var spDetail = await schemaRepo.GetSpByIdAsync(current.Sp.SpId);
            if (spDetail == null)
            {
                blockers.Add($"Stored procedure '{NormalizeQualifiedName(current.Sp.SchemaName, current.Sp.ProcedureName)}' is missing its full definition in metadata. Re-sync the project.");
                continue;
            }

            var persistedProcedureDependencies = await patcherRepo.GetSpProcedureDependenciesAsync(request.ProjectId, current.Sp.SpId, ct);
            var persistedTableDependencies = await patcherRepo.GetSpOutboundDependenciesAsync(request.ProjectId, current.Sp.SpId, ct);
            var persistedColumnDependencies = await patcherRepo.GetSpColumnDependenciesAsync(request.ProjectId, current.Sp.SpId, ct);
            var liveDependencies = dependencyAnalysisService.ExtractDependencies(spDetail.Definition, current.Sp.SpId, "SP");
            var liveProcedureDependencies = ResolveLiveProcedureDependencies(liveDependencies, storedProcedureMap);
            var liveTableDependencies = ResolveLiveTableDependencies(liveDependencies, tableLookup);
            var liveColumnDependencies = await ResolveLiveColumnDependenciesAsync(
                liveDependencies,
                tableLookup,
                tableColumnsCache);
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
                IsShared = current.IsShared,
                HasDynamicSql = hasDynamicSql,
                DependencyProcedureIds = procedureDependencies.Select(d => d.SpId).Distinct().ToList(),
                TableIds = tableDependencies.Select(d => d.TableId).Distinct().ToList(),
                ColumnIds = columnDependencies.Select(d => d.ColumnId).Distinct().ToList()
            };

            procedureSnapshots[snapshot.SpId] = snapshot;

            foreach (var procedureDependency in procedureDependencies)
            {
                if (!TryResolveStoredProcedure(storedProcedureMap, NormalizeQualifiedName(procedureDependency.SchemaName, procedureDependency.ProcedureName), out var nestedProcedure))
                {
                    blockers.Add($"Nested stored procedure '{NormalizeQualifiedName(procedureDependency.SchemaName, procedureDependency.ProcedureName)}' was found in dependencies but is missing from synced metadata.");
                    continue;
                }

                rootQueue.Enqueue((nestedProcedure, snapshot.IsShared, current.SourcePage));
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

        var tableSnapshots = new List<PatchTableSnapshot>();
        foreach (var tableId in procedureSnapshots.Values.SelectMany(p => p.TableIds).Distinct())
        {
            var tableMetadata = await schemaRepo.GetTableByIdAsync(tableId);
            if (tableMetadata == null)
            {
                blockers.Add($"Table dependency '{tableId}' is missing from synced metadata. Re-sync the project before generating a patch.");
                continue;
            }

            var columns = await schemaRepo.GetStoredColumnsAsync(tableId);
            if (columns.Count == 0)
            {
                blockers.Add($"Table '{NormalizeQualifiedName(tableMetadata.SchemaName, tableMetadata.TableName)}' has no stored column metadata. Re-sync the project before generating a patch.");
                continue;
            }

            var requiredColumns = requiredColumnsByTable.TryGetValue(tableId, out var required)
                ? required.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList()
                : columns.Select(c => c.ColumnName).ToList();

            if (!requiredColumnsByTable.TryGetValue(tableId, out var requiredSet) || requiredSet.Count == 0)
            {
                warnings.Add($"No column-level dependencies were stored for table '{NormalizeQualifiedName(tableMetadata.SchemaName, tableMetadata.TableName)}'. Falling back to the full table snapshot.");
            }

            tableSnapshots.Add(new PatchTableSnapshot
            {
                TableId = tableMetadata.TableId,
                TableName = tableMetadata.TableName,
                SchemaName = tableMetadata.SchemaName ?? "dbo",
                Columns = columns.Select(c => new PatchColumnSnapshot
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
                }).ToList(),
                Indexes = (await schemaRepo.GetStoredIndexesAsync(tableId))
                    .Select(i => new PatchIndexSnapshot
                    {
                        IndexName = i.IndexName,
                        IsUnique = i.IsUnique,
                        IsPrimaryKey = i.IsPrimaryKey,
                        Columns = i.Columns.Select(c => new PatchIndexColumnSnapshot
                        {
                            ColumnId = c.ColumnId,
                            ColumnName = c.ColumnName,
                            ColumnOrder = c.ColumnOrder
                        }).ToList()
                    }).ToList(),
                ForeignKeys = (await schemaRepo.GetStoredForeignKeysAsync(tableId))
                    .Select(f => new PatchForeignKeySnapshot
                    {
                        ColumnName = f.ColumnName,
                        ReferencedTableName = f.ReferencedTableName,
                        ReferencedSchemaName = f.ReferencedSchemaName,
                        ReferencedColumnName = f.ReferencedColumnName,
                        ForeignKeyName = f.ForeignKeyName,
                        OnDeleteAction = f.OnDeleteAction,
                        OnUpdateAction = f.OnUpdateAction
                    }).ToList(),
                RequiredColumnNames = requiredColumns
            });
        }

        return new PatchManifest
        {
            ProjectId = request.ProjectId,
            Pages = pages,
            Procedures = OrderProceduresByDependencies(procedureSnapshots.Values.ToList()),
            Tables = tableSnapshots.OrderBy(t => t.SchemaName).ThenBy(t => t.TableName).ToList(),
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            BlockingIssues = blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private async Task<IEnumerable<ApprovedSpDto>> GetApprovedStoredProceduresAsync(
        int projectId,
        string domainName,
        string pageName,
        CancellationToken ct = default)
    {
        return await pageMappingRepo.GetApprovedStoredProceduresAsync(projectId, domainName, pageName, ct);
    }

    internal static string GenerateMenuPermissionSql(string pageName, string domainName)
    {
        if (!Regex.IsMatch(pageName, "^[A-Za-z0-9_]+$"))
        {
            throw new ArgumentException("PageName contains invalid characters.");
        }
        if (!Regex.IsMatch(domainName, "^[A-Za-z0-9_]+$"))
        {
            throw new ArgumentException("DomainName contains invalid characters.");
        }

        var safePageName = pageName.Replace("'", "''");
        var safeDomainName = domainName.Replace("'", "''");

        var sb = new StringBuilder();
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");

        sb.AppendLine("-- =============================================");
        sb.AppendLine("-- Menu & Permission Script (New Page)");
        sb.AppendLine($"-- Page: {safePageName} | Domain: {safeDomainName}");
        sb.AppendLine($"-- Generated by ActoEngine on {timestamp}");
        sb.AppendLine("-- =============================================");
        sb.AppendLine();
        sb.AppendLine("DECLARE @TableName SYSNAME = 'Common_MenuMas'");
        sb.AppendLine($"DECLARE @WhereClause NVARCHAR(MAX) = 'WHERE [Action] = ''{safePageName}'''");
        sb.AppendLine("DECLARE @Cols NVARCHAR(MAX), @ColsValues NVARCHAR(MAX), @SQL NVARCHAR(MAX)");
        sb.AppendLine();
        sb.AppendLine("-- Build column list (excludes IDENTITY columns)");
        sb.AppendLine("SELECT @Cols = STUFF((");
        sb.AppendLine("    SELECT ',' + QUOTENAME(COLUMN_NAME)");
        sb.AppendLine("    FROM INFORMATION_SCHEMA.COLUMNS");
        sb.AppendLine("    WHERE TABLE_NAME = @TableName");
        sb.AppendLine("      AND COLUMNPROPERTY(OBJECT_ID(TABLE_SCHEMA + '.' + TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 0");
        sb.AppendLine("    ORDER BY ORDINAL_POSITION");
        sb.AppendLine("    FOR XML PATH(''), TYPE");
        sb.AppendLine(").value('.', 'NVARCHAR(MAX)'),1,1,'')");
        sb.AppendLine();
        sb.AppendLine("-- Build value expressions");
        sb.AppendLine("SELECT @ColsValues = STUFF((");
        sb.AppendLine("    SELECT ' + '','' + ISNULL('''''''' + ");
        sb.AppendLine("           REPLACE(CAST(' + QUOTENAME(COLUMN_NAME) + ' AS NVARCHAR(MAX)),'''''''','''''''''''') + ");
        sb.AppendLine("           '''''''',''NULL'')'");
        sb.AppendLine("    FROM INFORMATION_SCHEMA.COLUMNS");
        sb.AppendLine("    WHERE TABLE_NAME = @TableName");
        sb.AppendLine("      AND COLUMNPROPERTY(OBJECT_ID(TABLE_SCHEMA + '.' + TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 0");
        sb.AppendLine("    ORDER BY ORDINAL_POSITION");
        sb.AppendLine("    FOR XML PATH(''), TYPE");
        sb.AppendLine(").value('.', 'NVARCHAR(MAX)'),1,9,'')");
        sb.AppendLine();
        sb.AppendLine("-- Generate conditional INSERT with auto-permission");
        sb.AppendLine("SET @SQL = '");
        sb.AppendLine("SELECT ''IF NOT EXISTS (SELECT 1 FROM ' + @TableName + ' ' + ");
        sb.AppendLine("    REPLACE(@WhereClause, '''', '''''') + ') ' + ");
        sb.AppendLine("    'BEGIN ' + ");
        sb.AppendLine("        'INSERT INTO ' + @TableName + '(' + @Cols + ') VALUES ('' + ' + @ColsValues + ' + ''); ' +");
        sb.AppendLine("        'INSERT INTO Security_UserPermission ");
        sb.AppendLine("            (UserGroupId, MenuId, IsView, IsAdd, IsUpdate, IsDelete, IsDownload, IsDisableIpLock) ' +");
        sb.AppendLine("        'SELECT UserGroupId, SCOPE_IDENTITY(), 1, 1, 1, 1, 1, 1 ' + ");
        sb.AppendLine("        'FROM Security_UserAccessGroups WHERE UserGroupName = ''''Administration''''; ' +");
        sb.AppendLine("    'END''");
        sb.AppendLine("FROM ' + @TableName + ' ' + @WhereClause");
        sb.AppendLine();
        sb.AppendLine("EXEC(@SQL)");

        return sb.ToString();
    }

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

    private static string NormalizeQualifiedName(string? schemaName, string objectName)
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
        return liveDependencies
            .Where(d => d.TargetType == "SP")
            .Select(d => TryResolveStoredProcedure(storedProcedureMap, d.TargetName, out var sp) ? new SpProcedureDependencyRow
            {
                SpId = sp.SpId,
                ProcedureName = sp.ProcedureName,
                SchemaName = sp.SchemaName
            } : null)
            .Where(d => d != null)
            .GroupBy(d => d!.SpId)
            .Select(g => g.First()!)
            .ToList();
    }

    private static List<SpTableDependencyRow> ResolveLiveTableDependencies(
        IEnumerable<Dependency> liveDependencies,
        IReadOnlyDictionary<string, TableMetadataDto> tableLookup)
    {
        return liveDependencies
            .Where(d => d.TargetType == "TABLE")
            .Select(d => TryResolveTable(tableLookup, d.TargetName, out var table) ? new SpTableDependencyRow
            {
                TableId = table.TableId,
                TableName = table.TableName,
                SchemaName = table.SchemaName
            } : null)
            .Where(d => d != null)
            .GroupBy(d => d!.TableId)
            .Select(g => g.First()!)
            .ToList();
    }

    private async Task<List<SpColumnDependencyRow>> ResolveLiveColumnDependenciesAsync(
        IEnumerable<Dependency> liveDependencies,
        IReadOnlyDictionary<string, TableMetadataDto> tableLookup,
        Dictionary<int, List<ColumnMetadataDto>> tableColumnsCache)
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

            if (!tableColumnsCache.TryGetValue(table.TableId, out var columns))
            {
                columns = await schemaRepo.GetStoredColumnsAsync(table.TableId);
                tableColumnsCache[table.TableId] = columns;
            }

            var column = columns.FirstOrDefault(c => c.ColumnName.Equals(parsed.Value.ColumnName, StringComparison.OrdinalIgnoreCase));
            if (column == null)
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

        return resolved
            .GroupBy(c => c.ColumnId)
            .Select(g => g.First())
            .ToList();
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

        return resolved
            .GroupBy(t => t.TableId)
            .Select(g => g.First())
            .ToList();
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

    private static List<SpProcedureDependencyRow> MergeProcedureDependencies(
        IEnumerable<SpProcedureDependencyRow> first,
        IEnumerable<SpProcedureDependencyRow> second)
    {
        return first.Concat(second)
            .GroupBy(d => d.SpId)
            .Select(g => g.First())
            .ToList();
    }

    private static List<SpTableDependencyRow> MergeTableDependencies(
        IEnumerable<SpTableDependencyRow> first,
        IEnumerable<SpTableDependencyRow> second)
    {
        return first.Concat(second)
            .GroupBy(d => d.TableId)
            .Select(g => g.First())
            .ToList();
    }

    private static List<SpColumnDependencyRow> MergeColumnDependencies(
        IEnumerable<SpColumnDependencyRow> first,
        IEnumerable<SpColumnDependencyRow> second)
    {
        return first.Concat(second)
            .GroupBy(d => d.ColumnId)
            .Select(g => g.First())
            .ToList();
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

    private static void ValidateConfig(ProjectPatchConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ProjectRootPath))
            throw new InvalidOperationException("ProjectRootPath is not configured for this project.");
        if (string.IsNullOrWhiteSpace(config.ViewDirPath))
            throw new InvalidOperationException("ViewDirPath is not configured for this project.");
        if (string.IsNullOrWhiteSpace(config.ScriptDirPath))
            throw new InvalidOperationException("ScriptDirPath is not configured for this project.");
        if (string.IsNullOrWhiteSpace(config.PatchDownloadPath))
            throw new InvalidOperationException("PatchDownloadPath is not configured for this project.");
        if (!Directory.Exists(config.ProjectRootPath))
            throw new InvalidOperationException($"ProjectRootPath does not exist: {config.ProjectRootPath}");
    }

    private static string BuildFilePath(string rootPath, string relativeDir, string domain, string fileName)
    {
        return Path.Combine(rootPath, relativeDir, domain, fileName);
    }

    private static DateTime? GetLatestModifiedDate(params string[] filePaths)
    {
        DateTime? latest = null;
        foreach (var path in filePaths)
        {
            if (!File.Exists(path)) continue;
            var mod = File.GetLastWriteTimeUtc(path);
            if (latest == null || mod > latest.Value)
                latest = mod;
        }
        return latest;
    }

    private static async Task AddFileToArchiveAsync(ZipArchive archive, string entryName, string filePath, CancellationToken ct)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        await fileStream.CopyToAsync(entryStream, ct);
        await entryStream.FlushAsync(ct);
    }

    private static void AddTextToArchive(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, Encoding.UTF8);
        writer.Write(content);
    }

    [GeneratedRegex(@"\bsp_executesql\b|\bexec(?:ute)?\s*\(|\bexec(?:ute)?\s+@\w+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DynamicSqlRegex();
    [GeneratedRegex(@"\b(?:from|join|update|into|delete\s+from)\s+((?:\[[^\]]+\]|\w+)(?:\.(?:\[[^\]]+\]|\w+))?)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DynamicSqlTableRegex();
}
