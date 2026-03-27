using ActoEngine.WebApi.Features.Patcher.Engine;
using ActoEngine.WebApi.Features.Projects;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ActoEngine.WebApi.Features.Patcher;

public interface IPatcherService
{
    Task<List<PagePatchStatus>> CheckPatchStatusAsync(PatchStatusRequest request, CancellationToken ct = default);
    Task<PatchGenerationResponse> GeneratePatchAsync(PatchGenerationRequest request, int? userId = null, CancellationToken ct = default);

    string BuildPatchScript(string patchPath);
}

/// <summary>
/// Orchestrates patch status checks and patch generation.
/// Delegates heavy lifting to <see cref="PatchManifestBuilder"/> and <see cref="PatchArchiver"/>.
/// </summary>
public sealed partial class PatcherService(
    IPatcherRepository patcherRepo,
    IPageMappingRepository pageMappingRepo,
    IProjectRepository projectRepo,
    PatchManifestBuilder manifestBuilder,
    IPatchScriptRenderer scriptRenderer,
    PatchArchiver archiver,
    IPatchApplyScriptBuilder patchApplyScriptBuilder,
    ILogger<PatcherService> log) : IPatcherService
{
    [GeneratedRegex(@"^[A-Za-z0-9_\-]+$")]
    private static partial Regex SafePathSegmentRegex();
    private static readonly JsonSerializerOptions s_signatureJsonOptions = new() { WriteIndented = false };
    private readonly IPatchScriptRenderer _scriptRenderer = scriptRenderer;


    public async Task<List<PagePatchStatus>> CheckPatchStatusAsync(PatchStatusRequest request, CancellationToken ct = default)
    {
        if (request.PageMappings == null || request.PageMappings.Count == 0)
        {
            throw new InvalidOperationException("At least one page mapping is required.");
        }

        var config = await patcherRepo.GetPatchConfigAsync(request.ProjectId, ct)
            ?? throw new InvalidOperationException("Patch configuration not found for this project.");
        var resolvedConfig = ResolveAndValidateConfig(config);

        var uniqueMappings = NormalizeMappings(request.PageMappings);
        var pageEntries = uniqueMappings.Select(mapping => new PatchPageEntry
        {
            DomainName = mapping.DomainName,
            PageName = mapping.PageName,
            IsNewPage = mapping.IsNewPage
        }).ToList();

        var approvedRows = await pageMappingRepo.GetApprovedStoredProceduresByPagesAsync(request.ProjectId, pageEntries, ct);
        var approvedByPage = approvedRows.GroupBy(
                row => PatcherRepository.BuildPageKey(row.DomainName, row.PageName),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var latestByPage = await patcherRepo.GetLatestPatchesAsync(request.ProjectId, pageEntries, ct);
        var lastModifiedCache = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);

        var statuses = new List<PagePatchStatus>(uniqueMappings.Count);
        foreach (var mapping in uniqueMappings)
        {
            var pageKey = PatcherRepository.BuildPageKey(mapping.DomainName, mapping.PageName);
            var status = new PagePatchStatus
            {
                PageName = mapping.PageName,
                DomainName = mapping.DomainName,
                NeedsRegeneration = true,
                Reason = "No existing patch found"
            };

            if (!approvedByPage.TryGetValue(pageKey, out var approvedCount) || approvedCount == 0)
            {
                status.NeedsRegeneration = false;
                status.Reason = "No approved mappings found for this page";
                statuses.Add(status);
                continue;
            }

            if (latestByPage.TryGetValue(pageKey, out var latestPatch))
            {
                status.LastPatchDate = latestPatch.GeneratedAt;

                var jsPath = BuildPageFilePath(resolvedConfig.ScriptRootPath, mapping.DomainName, $"{mapping.PageName}.js");
                var cshtmlPath = BuildPageFilePath(resolvedConfig.ViewRootPath, mapping.DomainName, $"{mapping.PageName}.cshtml");

                var lastModified = GetLatestModifiedDate(lastModifiedCache, jsPath, cshtmlPath);
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
        if (request.PageMappings == null || request.PageMappings.Count == 0)
        {
            throw new InvalidOperationException("At least one page mapping is required.");
        }

        // 1. Validate project & config
        _ = await projectRepo.GetByIdAsync(request.ProjectId, ct)
            ?? throw new InvalidOperationException($"Project with ID {request.ProjectId} not found.");

        ProjectPatchConfig config = await patcherRepo.GetPatchConfigAsync(request.ProjectId, ct)
            ?? throw new InvalidOperationException("Patch configuration not found for this project.");
        var resolvedConfig = ResolveAndValidateConfig(config);

        var uniqueMappings = NormalizeMappings(request.PageMappings);
        var normalizedRequest = new PatchGenerationRequest
        {
            ProjectId = request.ProjectId,
            PatchName = request.PatchName,
            PageMappings = uniqueMappings
        };

        // 2. Build manifest (dependency walk, snapshot collection)
        var manifest = await manifestBuilder.BuildAsync(normalizedRequest, ct);
        if (manifest.BlockingIssues.Count != 0)
        {
            throw new InvalidOperationException(
                "Patch generation blocked because dependency metadata is incomplete or unsafe:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, manifest.BlockingIssues.Select(issue => $"- {issue}")));
        }

        if (manifest.Procedures.Count == 0 && manifest.Tables.Count == 0)
        {
            return new PatchGenerationResponse
            {
                PatchId = 0,
                DownloadPath = string.Empty,
                ScriptDownloadPath = null,
                FilesIncluded = [],
                Warnings = [.. manifest.Warnings.Append("No procedures or tables found in the manifest. Nothing to generate.").Distinct(StringComparer.OrdinalIgnoreCase)],
                GeneratedAt = DateTime.UtcNow
            };
        }

        var patchSignature = ComputePatchSignature(manifest, uniqueMappings, resolvedConfig);
        await GuardStalenessBySignatureAsync(request.ProjectId, uniqueMappings, patchSignature, ct);

        // 3. Render SQL scripts
        var artifacts = _scriptRenderer.Render(manifest);
        var warnings = new List<string>(manifest.Warnings);

        // 4. Archive
        var firstMapping = uniqueMappings[0];
        var generatedAt = DateTime.UtcNow;
        var timestamp = generatedAt.ToString("dd-MM-yyyy_HH-mm-ss");
        var tempName = string.IsNullOrWhiteSpace(request.PatchName)
            ? string.Empty
            : new string([.. request.PatchName.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')]);
        var safePatchName = string.IsNullOrWhiteSpace(request.PatchName)
            ? $"{firstMapping.DomainName}_{firstMapping.PageName}"
            : (string.IsNullOrWhiteSpace(tempName) ? $"{firstMapping.DomainName}_{firstMapping.PageName}" : tempName);

        var (zipFilePath, filesIncluded, archiveWarnings) =
            await archiver.CreateArchiveAsync(normalizedRequest, config, artifacts, safePatchName, timestamp, ct);

        warnings.AddRange(archiveWarnings);

        // 5. Persist history
        var spNamesList = manifest.Procedures.Select(s => s.ProcedureName).ToList();
        var pages = uniqueMappings.Select(m => new PatchPageEntry
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
            PatchName = request.PatchName,
            IsNewPage = firstMapping.IsNewPage,
            PatchFilePath = zipFilePath,
            PatchSignature = patchSignature,
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

        // 6. Return response
        return new PatchGenerationResponse
        {
            PatchId = patchId,
            DownloadPath = zipFilePath,
            ScriptDownloadPath = $"/api/patcher/download-script/{patchId}",
            FilesIncluded = filesIncluded,
            Warnings = [.. warnings.Distinct(StringComparer.OrdinalIgnoreCase)],
            GeneratedAt = generatedAt
        };
    }

    public string BuildPatchScript(string patchPath)
    {
        return patchApplyScriptBuilder.Build(patchPath);
    }
    // ──────────────────────────────────────────────
    //  Private helpers
    // ──────────────────────────────────────────────

    private async Task GuardStalenessBySignatureAsync(
        int projectId,
        IReadOnlyCollection<PageSpMapping> mappings,
        string currentSignature,
        CancellationToken ct)
    {
        var pages = mappings.Select(mapping => new PatchPageEntry
        {
            DomainName = mapping.DomainName,
            PageName = mapping.PageName,
            IsNewPage = mapping.IsNewPage
        }).ToList();

        var latestByPage = await patcherRepo.GetLatestPatchesAsync(projectId, pages, ct);
        foreach (var page in pages)
        {
            var key = PatcherRepository.BuildPageKey(page.DomainName, page.PageName);
            if (!latestByPage.TryGetValue(key, out var latestPatch))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(latestPatch.PatchSignature))
            {
                return;
            }

            if (!string.Equals(latestPatch.PatchSignature, currentSignature, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        throw new InvalidOperationException(
            "Patch is already up to date. Manifest signature and source snapshot match the latest generated patch.");
    }

    private static ResolvedPatchConfig ResolveAndValidateConfig(ProjectPatchConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ProjectRootPath))
            throw new InvalidOperationException("ProjectRootPath is not configured for this project.");
        if (string.IsNullOrWhiteSpace(config.ViewDirPath))
            throw new InvalidOperationException("ViewDirPath is not configured for this project.");
        if (string.IsNullOrWhiteSpace(config.ScriptDirPath))
            throw new InvalidOperationException("ScriptDirPath is not configured for this project.");
        if (string.IsNullOrWhiteSpace(config.PatchDownloadPath))
            throw new InvalidOperationException("PatchDownloadPath is not configured for this project.");

        var projectRootPath = PatchPathSafety.NormalizePath(config.ProjectRootPath, nameof(config.ProjectRootPath));
        if (!Directory.Exists(projectRootPath))
            throw new InvalidOperationException($"ProjectRootPath does not exist: {projectRootPath}");

        var viewRootPath = PatchPathSafety.ResolveRelativePathUnderRoot(projectRootPath, config.ViewDirPath, nameof(config.ViewDirPath));
        var scriptRootPath = PatchPathSafety.ResolveRelativePathUnderRoot(projectRootPath, config.ScriptDirPath, nameof(config.ScriptDirPath));
        var patchDownloadRootPath = PatchPathSafety.ResolvePath(projectRootPath, config.PatchDownloadPath, nameof(config.PatchDownloadPath));

        return new ResolvedPatchConfig
        {
            ProjectRootPath = projectRootPath,
            ViewRootPath = viewRootPath,
            ScriptRootPath = scriptRootPath,
            PatchDownloadRootPath = patchDownloadRootPath
        };
    }

    private string ComputePatchSignature(
        PatchManifest manifest,
        IReadOnlyCollection<PageSpMapping> mappings,
        ResolvedPatchConfig resolvedConfig)
    {
        var fileSnapshot = BuildSourceFileSnapshot(mappings, resolvedConfig);

        var signaturePayload = new
        {
            pages = mappings
                .Select(mapping => new
                {
                    domainName = mapping.DomainName,
                    pageName = mapping.PageName,
                    isNewPage = mapping.IsNewPage
                })
                .OrderBy(page => page.domainName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(page => page.pageName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            manifest = new
            {
                projectId = manifest.ProjectId,
                procedures = manifest.Procedures
                    .OrderBy(procedure => procedure.IsShared)
                    .ThenBy(procedure => procedure.SchemaName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(procedure => procedure.ProcedureName, StringComparer.OrdinalIgnoreCase)
                    .Select(procedure => new
                    {
                        procedure.SpId,
                        procedure.ProcedureName,
                        procedure.SchemaName,
                        procedure.IsShared,
                        procedure.HasDynamicSql,
                        definitionHash = ComputeSha256(procedure.Definition),
                        dependencyProcedureIds = procedure.DependencyProcedureIds.OrderBy(id => id).ToList(),
                        tableIds = procedure.TableIds.OrderBy(id => id).ToList(),
                        columnIds = procedure.ColumnIds.OrderBy(id => id).ToList()
                    })
                    .ToList(),
                tables = manifest.Tables
                    .OrderBy(table => table.SchemaName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(table => table.TableName, StringComparer.OrdinalIgnoreCase)
                    .Select(table => new
                    {
                        table.TableId,
                        table.TableName,
                        table.SchemaName,
                        requiredColumns = table.RequiredColumnNames
                            .OrderBy(column => column, StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                        columns = table.Columns
                            .OrderBy(column => column.ColumnName, StringComparer.OrdinalIgnoreCase)
                            .Select(column => new
                            {
                                column.ColumnName,
                                column.DataType,
                                column.MaxLength,
                                column.Precision,
                                column.Scale,
                                column.IsNullable,
                                column.IsPrimaryKey,
                                column.IsIdentity,
                                column.DefaultValue
                            })
                            .ToList(),
                        indexes = table.Indexes
                            .OrderBy(index => index.IndexName, StringComparer.OrdinalIgnoreCase)
                            .Select(index => new
                            {
                                index.IndexName,
                                index.IsUnique,
                                index.IsPrimaryKey,
                                columns = index.Columns
                                    .OrderBy(column => column.ColumnOrder)
                                    .Select(column => new { column.ColumnName, column.ColumnOrder })
                                    .ToList()
                            })
                            .ToList(),
                        foreignKeys = table.ForeignKeys
                            .OrderBy(foreignKey => foreignKey.ForeignKeyName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(foreignKey => foreignKey.ColumnName, StringComparer.OrdinalIgnoreCase)
                            .Select(foreignKey => new
                            {
                                foreignKey.ColumnName,
                                foreignKey.ReferencedSchemaName,
                                foreignKey.ReferencedTableName,
                                foreignKey.ReferencedColumnName,
                                foreignKey.ForeignKeyName,
                                foreignKey.OnDeleteAction,
                                foreignKey.OnUpdateAction
                            })
                            .ToList()
                    })
                    .ToList(),
                warnings = manifest.Warnings.OrderBy(warning => warning, StringComparer.OrdinalIgnoreCase).ToList(),
                blockers = manifest.BlockingIssues.OrderBy(blocker => blocker, StringComparer.OrdinalIgnoreCase).ToList()
            },
            sourceFiles = fileSnapshot
        };

        var payloadJson = JsonSerializer.Serialize(signaturePayload, s_signatureJsonOptions);
        return ComputeSha256(payloadJson);
    }

    private static List<SourceFileSignatureRecord> BuildSourceFileSnapshot(
        IReadOnlyCollection<PageSpMapping> mappings,
        ResolvedPatchConfig resolvedConfig)
    {
        var snapshot = new List<SourceFileSignatureRecord>();
        foreach (var mapping in mappings)
        {
            var jsPath = BuildPageFilePath(resolvedConfig.ScriptRootPath, mapping.DomainName, $"{mapping.PageName}.js");
            var cshtmlPath = BuildPageFilePath(resolvedConfig.ViewRootPath, mapping.DomainName, $"{mapping.PageName}.cshtml");

            snapshot.Add(BuildSingleFileSnapshot("js", mapping.DomainName, mapping.PageName, jsPath, resolvedConfig.ProjectRootPath));
            snapshot.Add(BuildSingleFileSnapshot("cshtml", mapping.DomainName, mapping.PageName, cshtmlPath, resolvedConfig.ProjectRootPath));
        }

        return [.. snapshot
            .OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.FileType, StringComparer.OrdinalIgnoreCase)];
    }

    private static SourceFileSignatureRecord BuildSingleFileSnapshot(
        string fileType,
        string domainName,
        string pageName,
        string fullPath,
        string projectRootPath)
    {
        var exists = File.Exists(fullPath);
        long? length = null;
        long? lastWriteTicks = null;

        if (exists)
        {
            var info = new FileInfo(fullPath);
            length = info.Length;
            lastWriteTicks = info.LastWriteTimeUtc.Ticks;
        }

        return new SourceFileSignatureRecord
        {
            FileType = fileType,
            DomainName = domainName,
            PageName = pageName,
            Path = Path.GetRelativePath(projectRootPath, fullPath).Replace('\\', '/'),
            Exists = exists,
            Length = length,
            LastWriteTicks = lastWriteTicks
        };
    }

    private static string ComputeSha256(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static List<PageSpMapping> NormalizeMappings(IReadOnlyCollection<PageSpMapping> mappings)
    {
        return [.. mappings
            .Select(mapping =>
            {
                ValidateSafePathSegment(mapping.DomainName, "DomainName");
                ValidateSafePathSegment(mapping.PageName, "PageName");
                return new PageSpMapping
                {
                    DomainName = mapping.DomainName.Trim(),
                    PageName = mapping.PageName.Trim(),
                    ServiceNames = mapping.ServiceNames ?? [],
                    FiltersRaw = mapping.FiltersRaw,
                    IsNewPage = mapping.IsNewPage
                };
            })
            .GroupBy(mapping => PatcherRepository.BuildPageKey(mapping.DomainName, mapping.PageName), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                first.IsNewPage = group.Any(mapping => mapping.IsNewPage);
                return first;
            })
            .ToList()];
    }

    private static void ValidateSafePathSegment(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{parameterName} must not be null or empty.");

        if (value.Contains("..") || value.Contains('/') || value.Contains('\\'))
            throw new InvalidOperationException($"{parameterName} contains invalid characters (path traversal attempt).");

        if (!SafePathSegmentRegex().IsMatch(value))
            throw new InvalidOperationException($"{parameterName} contains invalid characters.");
    }

    private static string BuildPageFilePath(string basePath, string domain, string fileName)
    {
        var fullPath = Path.GetFullPath(Path.Combine(basePath, domain, fileName));
        PatchPathSafety.EnsurePathIsUnderRoot(fullPath, basePath, nameof(basePath));
        return fullPath;
    }

    private static DateTime? GetLatestModifiedDate(
        IDictionary<string, DateTime?> cache,
        params string[] filePaths)
    {
        DateTime? latest = null;
        foreach (var path in filePaths)
        {
            if (!cache.TryGetValue(path, out var modified))
            {
                modified = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;
                cache[path] = modified;
            }

            if (modified == null)
            {
                continue;
            }

            if (latest == null || modified.Value > latest.Value)
            {
                latest = modified.Value;
            }
        }

        return latest;
    }

    private sealed class ResolvedPatchConfig
    {
        public required string ProjectRootPath { get; init; }
        public required string ViewRootPath { get; init; }
        public required string ScriptRootPath { get; init; }
        public required string PatchDownloadRootPath { get; init; }
    }

    private sealed class SourceFileSignatureRecord
    {
        public required string FileType { get; init; }
        public required string DomainName { get; init; }
        public required string PageName { get; init; }
        public required string Path { get; init; }
        public required bool Exists { get; init; }
        public required long? Length { get; init; }
        public required long? LastWriteTicks { get; init; }
    }
}
