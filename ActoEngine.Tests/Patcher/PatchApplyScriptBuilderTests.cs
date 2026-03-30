using ActoEngine.WebApi.Features.Patcher;
using System.IO.Compression;

namespace ActoEngine.Tests.Patcher;

public class PatchApplyScriptBuilderTests
{
    [Fact]
    public void Build_EmitsBatWithHashVerificationBackupAndExtraction()
    {
        var builder = new PatchApplyScriptBuilder();
        using var root = new TempDirectory();
        var zipPath = Path.Combine(root.Path, "patch_reports_sales_26-03-2026_07-08-47.zip");

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("Views/Reports/SalesPage.cshtml");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("<h1>sales</h1>");
        }

        var script = builder.Build(zipPath);

        Assert.StartsWith("@echo off", script);
        Assert.Contains("patch_reports_sales_26-03-2026_07-08-47.zip", script);
        Assert.Contains("certutil -hashfile", script);
        Assert.Contains("backup_", script);
        Assert.Contains("Unsafe archive entry detected", script);
        Assert.Contains("Expand-Archive", script);
        Assert.Contains("pause", script);
    }

    [Fact]
    public void Build_IncludesMarkOfTheWebRemoval()
    {
        var builder = new PatchApplyScriptBuilder();
        using var root = new TempDirectory();
        var zipPath = Path.Combine(root.Path, "patch.zip");

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            archive.CreateEntry("dummy.txt");
        }

        var script = builder.Build(zipPath);

        Assert.Contains("Zone.Identifier", script);
    }

    [Fact]
    public void Build_IncludesAdminWarningNotEnforcement()
    {
        var builder = new PatchApplyScriptBuilder();
        using var root = new TempDirectory();
        var zipPath = Path.Combine(root.Path, "patch.zip");

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            archive.CreateEntry("dummy.txt");
        }

        var script = builder.Build(zipPath);

        Assert.Contains("[WARNING] Not running as Administrator", script);
        Assert.DoesNotContain("exit /b 1\r\n)", script);
    }

    [Fact]
    public void Build_IncludesWorkingDirectoryFix()
    {
        var builder = new PatchApplyScriptBuilder();
        using var root = new TempDirectory();
        var zipPath = Path.Combine(root.Path, "patch.zip");

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            archive.CreateEntry("dummy.txt");
        }

        var script = builder.Build(zipPath);

        Assert.Contains("cd /d \"%~dp0\"", script);
    }

    [Fact]
    public void Build_IncludesPathTraversalChecks()
    {
        var builder = new PatchApplyScriptBuilder();
        using var root = new TempDirectory();
        var zipPath = Path.Combine(root.Path, "patch.zip");

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            archive.CreateEntry("dummy.txt");
        }

        var script = builder.Build(zipPath);

        Assert.Contains("Unsafe archive entry detected", script);
        Assert.Contains("Resolved path escapes target root", script);
        Assert.Contains("Resolved path escapes backup root", script);
    }

    [Fact]
    public void Build_IncludesLocaleSafeTimestamp()
    {
        var builder = new PatchApplyScriptBuilder();
        using var root = new TempDirectory();
        var zipPath = Path.Combine(root.Path, "patch.zip");

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            archive.CreateEntry("dummy.txt");
        }

        var script = builder.Build(zipPath);

        Assert.Contains("wmic os get localdatetime", script);
    }

    [Fact]
    public void Build_IncludesLogFileCreation()
    {
        var builder = new PatchApplyScriptBuilder();
        using var root = new TempDirectory();
        var zipPath = Path.Combine(root.Path, "patch.zip");

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            archive.CreateEntry("dummy.txt");
        }

        var script = builder.Build(zipPath);

        Assert.Contains("apply_patch.log", script);
    }

    [Fact]
    public void Build_ThrowsWhenZipNotFound()
    {
        var builder = new PatchApplyScriptBuilder();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.zip");

        Assert.Throws<FileNotFoundException>(() => builder.Build(nonExistentPath));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; }

        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"acto-patcher-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
