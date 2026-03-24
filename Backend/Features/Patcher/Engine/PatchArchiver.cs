using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace ActoEngine.WebApi.Features.Patcher.Engine;

/// <summary>
/// Assembles the patch zip archive from rendered SQL artifacts and source files.
/// Pure I/O — receives everything it needs via method parameters.
/// </summary>
public sealed partial class PatchArchiver
{
    [GeneratedRegex(@"^[A-Za-z0-9_\-]+$")]
    private static partial Regex AlphanumericDashRegex();
    /// <summary>
    /// Creates a zip archive containing source files, SQL scripts, and manifest,
    /// writes it to disk, and returns the file path, included entries, and any warnings.
    /// </summary>
    public async Task<(string ZipFilePath, List<string> FilesIncluded, List<string> Warnings)> CreateArchiveAsync(
        PatchGenerationRequest request,
        ProjectPatchConfig config,
        PatchArchiveArtifacts artifacts,
        string safePatchName,
        string timestamp,
        CancellationToken ct)
    {
        var filesIncluded = new List<string>();
        var warnings = new List<string>();
        var projectRoot = PatchPathSafety.NormalizePath(config.ProjectRootPath!, nameof(config.ProjectRootPath));
        var viewRoot = PatchPathSafety.ResolveRelativePathUnderRoot(projectRoot, config.ViewDirPath!, nameof(config.ViewDirPath));
        var scriptRoot = PatchPathSafety.ResolveRelativePathUnderRoot(projectRoot, config.ScriptDirPath!, nameof(config.ScriptDirPath));
        var patchDownloadRoot = PatchPathSafety.ResolvePath(projectRoot, config.PatchDownloadPath!, nameof(config.PatchDownloadPath));

        var zipFileName = $"patch_{safePatchName}_{timestamp}.zip";
        Directory.CreateDirectory(patchDownloadRoot);
        var zipFilePath = Path.Combine(patchDownloadRoot, zipFileName);

        await using var fileStream = new FileStream(
            zipFilePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            useAsync: true);
        using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false))
        {
            var uniqueMappings = request.PageMappings
                .GroupBy(mapping => PatcherRepository.BuildPageKey(mapping.DomainName, mapping.PageName), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            foreach (var mapping in uniqueMappings)
            {
                ValidatePathSegment(mapping.DomainName, nameof(mapping.DomainName));
                ValidatePathSegment(mapping.PageName, nameof(mapping.PageName));

                var cshtmlPath = BuildPageFilePath(viewRoot, mapping.DomainName, $"{mapping.PageName}.cshtml");
                if (File.Exists(cshtmlPath))
                {
                    var entryPath = BuildSafeArchiveEntry(projectRoot, cshtmlPath);
                    await AddFileToArchiveAsync(archive, entryPath, cshtmlPath, ct);
                    filesIncluded.Add(entryPath);
                }
                else
                {
                    warnings.Add($"View file not found: {cshtmlPath}");
                }

                var jsPath = BuildPageFilePath(scriptRoot, mapping.DomainName, $"{mapping.PageName}.js");
                if (File.Exists(jsPath))
                {
                    var entryPath = BuildSafeArchiveEntry(projectRoot, jsPath);
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
                    var menuEntry = PatchPathSafety.NormalizeArchiveEntryPath(
                        $"sql/{mapping.DomainName}_{mapping.PageName}_{timestamp}/menu_permission.sql");
                    await AddTextToArchiveAsync(archive, menuEntry, menuSql, ct);
                    filesIncluded.Add(menuEntry);
                }
            }

            var sqlDir = PatchPathSafety.NormalizeArchiveEntryPath($"sql/{safePatchName}_{timestamp}");

            var compatibilityEntry = PatchPathSafety.NormalizeArchiveEntryPath($"{sqlDir}/compatibility.sql");
            await AddTextToArchiveAsync(archive, compatibilityEntry, artifacts.CompatibilitySql, ct);
            filesIncluded.Add(compatibilityEntry);

            var updateEntry = PatchPathSafety.NormalizeArchiveEntryPath($"{sqlDir}/update.sql");
            await AddTextToArchiveAsync(archive, updateEntry, artifacts.UpdateSql, ct);
            filesIncluded.Add(updateEntry);

            var rollbackEntry = PatchPathSafety.NormalizeArchiveEntryPath($"{sqlDir}/rollback.sql");
            await AddTextToArchiveAsync(archive, rollbackEntry, artifacts.RollbackSql, ct);
            filesIncluded.Add(rollbackEntry);

            var manifestEntry = PatchPathSafety.NormalizeArchiveEntryPath($"{sqlDir}/manifest.json");
            await AddTextToArchiveAsync(archive, manifestEntry, artifacts.ManifestJson, ct);
            filesIncluded.Add(manifestEntry);
        }

        return (zipFilePath, filesIncluded, warnings);
    }

    internal static string GenerateMenuPermissionSql(string pageName, string domainName)
    {
        if (!AlphanumericDashRegex().IsMatch(pageName))
        {
            throw new ArgumentException("PageName contains invalid characters.");
        }
        if (!AlphanumericDashRegex().IsMatch(domainName))
        {
            throw new ArgumentException("DomainName contains invalid characters.");
        }

        var safePageName = pageName.Replace("'", "''");
        var safeDomainName = domainName.Replace("'", "''");

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");

        return PatcherQueries.MenuPermissionScriptTemplate
            .Replace("{SafePageName}", safePageName)
            .Replace("{SafeDomainName}", safeDomainName)
            .Replace("{Timestamp}", timestamp);
    }

    private static string BuildPageFilePath(string domainRoot, string domain, string fileName)
    {
        var fullPath = Path.GetFullPath(Path.Combine(domainRoot, domain, fileName));
        PatchPathSafety.EnsurePathIsUnderRoot(fullPath, domainRoot, nameof(domainRoot));
        return fullPath;
    }

    private static string BuildSafeArchiveEntry(string projectRoot, string filePath)
    {
        PatchPathSafety.EnsurePathIsUnderRoot(filePath, projectRoot, nameof(projectRoot));
        var relativePath = Path.GetRelativePath(projectRoot, filePath);
        return PatchPathSafety.NormalizeArchiveEntryPath(relativePath);
    }

    private static void ValidatePathSegment(string value, string parameterName)
    {
        if (!AlphanumericDashRegex().IsMatch(value))
        {
            throw new InvalidOperationException($"{parameterName} contains invalid characters.");
        }
    }

    private static async Task AddFileToArchiveAsync(ZipArchive archive, string entryName, string filePath, CancellationToken ct)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        await fileStream.CopyToAsync(entryStream, ct);
        await entryStream.FlushAsync(ct);
    }

    private static async Task AddTextToArchiveAsync(ZipArchive archive, string entryName, string content, CancellationToken ct)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var entryStream = entry.Open();
        await using var writer = new StreamWriter(entryStream, Encoding.UTF8);
        await writer.WriteAsync(content.AsMemory(), ct);
        await writer.FlushAsync(ct);
    }
}
