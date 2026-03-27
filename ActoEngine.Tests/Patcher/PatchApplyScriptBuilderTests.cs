using ActoEngine.WebApi.Features.Patcher;
using System.IO.Compression;

namespace ActoEngine.Tests.Patcher;

public class PatchApplyScriptBuilderTests
{
    [Fact]
    public void Build_EmbedsSiblingZipBackupTraversalGuardAndHashVerification()
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

        Assert.Contains("$zipPath = Join-Path $scriptRoot 'patch_reports_sales_26-03-2026_07-08-47.zip'", script);
        Assert.Contains("Get-FileHash -LiteralPath $zipPath -Algorithm SHA256", script);
        Assert.Contains("backup_", script);
        Assert.Contains("Assert-PathUnderRoot", script);
        Assert.Contains("Unsafe archive entry detected", script);
        Assert.Contains("Expand-Archive -LiteralPath $zipPath -DestinationPath $resolvedTargetPath -Force", script);
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
