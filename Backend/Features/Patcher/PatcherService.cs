using ActoEngine.WebApi.Features.Patcher.Engine;
using ActoEngine.WebApi.Features.Projects;
using System.Text.RegularExpressions;

namespace ActoEngine.WebApi.Features.Patcher;

public interface IPatcherService
{
    Task<List<PagePatchStatus>> CheckPatchStatusAsync(PatchStatusRequest request, CancellationToken ct = default);
    Task<PatchGenerationResponse> GeneratePatchAsync(PatchGenerationRequest request, int? userId = null, CancellationToken ct = default);
}

/// <summary>
/// Orchestrates patch status checks and patch generation.
/// Delegates heavy lifting to <see cref="PatchManifestBuilder"/> and <see cref="PatchArchiver"/>.
/// </summary>
public sealed class PatcherService(
    IPatcherRepository patcherRepo,
    IPageMappingRepository pageMappingRepo,
    IProjectRepository projectRepo,
    PatchManifestBuilder manifestBuilder,
    IPatchScriptRenderer scriptRenderer,
    PatchArchiver archiver,
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
            ValidateSafePathSegment(mapping.DomainName, "DomainName");
            ValidateSafePathSegment(mapping.PageName, "PageName");

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
        // 1. Validate project & config
        _ = await projectRepo.GetByIdAsync(request.ProjectId)
            ?? throw new InvalidOperationException($"Project with ID {request.ProjectId} not found.");

        ProjectPatchConfig config = await patcherRepo.GetPatchConfigAsync(request.ProjectId, ct)
            ?? throw new InvalidOperationException("Patch configuration not found for this project.");

        ValidateConfig(config);

        if (request.PageMappings == null || request.PageMappings.Count == 0)
        {
            throw new InvalidOperationException("At least one page mapping is required.");
        }

        foreach (var mapping in request.PageMappings)
        {
            ValidateSafePathSegment(mapping.DomainName, "DomainName");
            ValidateSafePathSegment(mapping.PageName, "PageName");
        }

        // 2. Guard: skip generation when no target file has changed since last patch
        await GuardStalenessAsync(request, config);

        // 3. Build manifest (dependency walk, snapshot collection)
        var manifest = await manifestBuilder.BuildAsync(request, ct);
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
                FilesIncluded = [],
                Warnings = manifest.Warnings.Append("No procedures or tables found in the manifest. Nothing to generate.").Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                GeneratedAt = DateTime.UtcNow
            };
        }

        // 4. Render SQL scripts
        var artifacts = _scriptRenderer.Render(manifest);
        var warnings = new List<string>(manifest.Warnings);

        // 5. Archive
        var firstMapping = request.PageMappings[0];
        var timestamp = DateTime.UtcNow.ToString("dd-MM-yyyy_HH-mm-ss");
        var safePatchName = string.IsNullOrWhiteSpace(request.PatchName)
            ? $"{firstMapping.DomainName}_{firstMapping.PageName}"
            : new string(request.PatchName.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());

        var (zipFilePath, filesIncluded, archiveWarnings) =
            await archiver.CreateArchiveAsync(request, config, artifacts, safePatchName, timestamp, ct);

        warnings.AddRange(archiveWarnings);

        // 6. Persist history
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
            PatchName = request.PatchName,
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

        // 7. Return response
        return new PatchGenerationResponse
        {
            PatchId = patchId,
            DownloadPath = zipFilePath,
            FilesIncluded = filesIncluded,
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            GeneratedAt = DateTime.UtcNow
        };
    }

    // ──────────────────────────────────────────────
    //  Private helpers
    // ──────────────────────────────────────────────

    private async Task GuardStalenessAsync(PatchGenerationRequest request, ProjectPatchConfig config)
    {
        bool anyPageChanged = false;
        foreach (var mapping in request.PageMappings)
        {
            var latestPatch = await patcherRepo.GetLatestPatchAsync(
                request.ProjectId, mapping.DomainName, mapping.PageName);

            if (latestPatch == null)
            {
                anyPageChanged = true;
                break;
            }

            var jsPath = BuildFilePath(config.ProjectRootPath!, config.ScriptDirPath!, mapping.DomainName, $"{mapping.PageName}.js");
            var cshtmlPath = BuildFilePath(config.ProjectRootPath!, config.ViewDirPath!, mapping.DomainName, $"{mapping.PageName}.cshtml");
            var lastModified = GetLatestModifiedDate(jsPath, cshtmlPath);

            if (lastModified == null || lastModified.Value > latestPatch.GeneratedAt)
            {
                anyPageChanged = true;
                break;
            }
        }

        if (!anyPageChanged)
        {
            throw new InvalidOperationException(
                "Patch is already up to date. No target files have been modified since the last patch was generated.");
        }
    }

    private async Task<IEnumerable<ApprovedSpDto>> GetApprovedStoredProceduresAsync(
        int projectId,
        string domainName,
        string pageName,
        CancellationToken ct = default)
    {
        return await pageMappingRepo.GetApprovedStoredProceduresAsync(projectId, domainName, pageName, ct);
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

    private static void ValidateSafePathSegment(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{parameterName} must not be null or empty.");

        if (value.Contains("..") || value.Contains('/') || value.Contains('\\'))
            throw new InvalidOperationException($"{parameterName} contains invalid characters (path traversal attempt).");

        if (!Regex.IsMatch(value, @"^[A-Za-z0-9_\-]+$"))
            throw new InvalidOperationException($"{parameterName} contains invalid characters.");
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
}
