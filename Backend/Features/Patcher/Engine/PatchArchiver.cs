using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace ActoEngine.WebApi.Features.Patcher.Engine;

/// <summary>
/// Assembles the patch zip archive from rendered SQL artifacts and source files.
/// Pure I/O — receives everything it needs via method parameters.
/// </summary>
public sealed class PatchArchiver
{
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

            var sqlDir = $"sql/{safePatchName}_{timestamp}";

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

        var zipFileName = $"patch_{safePatchName}_{timestamp}.zip";
        Directory.CreateDirectory(config.PatchDownloadPath!);
        var zipFilePath = Path.Combine(config.PatchDownloadPath!, zipFileName);

        zipStream.Position = 0;
        await using (var fileStream = new FileStream(zipFilePath, FileMode.Create))
        {
            await zipStream.CopyToAsync(fileStream, ct);
        }

        return (zipFilePath, filesIncluded, warnings);
    }

    internal static string GenerateMenuPermissionSql(string pageName, string domainName)
    {
        if (!Regex.IsMatch(pageName, @"^[A-Za-z0-9_\-]+$"))
        {
            throw new ArgumentException("PageName contains invalid characters.");
        }
        if (!Regex.IsMatch(domainName, @"^[A-Za-z0-9_\-]+$"))
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

    private static string BuildFilePath(string rootPath, string relativeDir, string domain, string fileName)
    {
        return Path.Combine(rootPath, relativeDir, domain, fileName);
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
}
