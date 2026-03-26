using ActoEngine.WebApi.Api.ApiModels;
using ActoEngine.WebApi.Features.Patcher;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ActoEngine.Tests.Patcher;

public class PatcherControllerTests
{
    private readonly IPatcherService _patcherService = Substitute.For<IPatcherService>();
    private readonly IPatcherRepository _patcherRepo = Substitute.For<IPatcherRepository>();
    private readonly ILogger<PatcherController> _logger = Substitute.For<ILogger<PatcherController>>();

    [Fact]
    public async Task DownloadPatch_ReturnsUnauthorized_WhenUserIdMissing()
    {
        var controller = CreateController();

        var result = await controller.DownloadPatch(42, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<object>>(unauthorized.Value);
        Assert.False(payload.Status);
        Assert.Equal("Unauthorized", payload.Message);
    }

    [Fact]
    public async Task DownloadPatchScript_ReturnsNotFound_WhenPatchDoesNotExist()
    {
        var controller = CreateController(userId: 7);
        _patcherRepo.GetPatchByIdAsync(42, 7, Arg.Any<CancellationToken>())
            .Returns((PatchHistoryRecord?)null);

        var result = await controller.DownloadPatchScript(42, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<object>>(notFound.Value);
        Assert.False(payload.Status);
        Assert.Equal("Patch not found", payload.Message);
    }

    [Fact]
    public async Task DownloadPatch_ReturnsNotFound_WhenPatchPathEscapesConfiguredStorageRoot()
    {
        var controller = CreateController(userId: 7);
        using var root = new TempDirectory();
        using var externalRoot = new TempDirectory();
        var externalZipPath = Path.Combine(externalRoot.Path, "patch_outside.zip");
        await File.WriteAllTextAsync(externalZipPath, "outside");

        _patcherRepo.GetPatchByIdAsync(42, 7, Arg.Any<CancellationToken>())
            .Returns(new PatchHistoryRecord
            {
                PatchId = 42,
                ProjectId = 11,
                PageName = "SalesPage",
                DomainName = "Reports",
                SpNames = "[]",
                PatchFilePath = externalZipPath,
                Status = "Generated"
            });

        _patcherRepo.GetPatchConfigAsync(11, Arg.Any<CancellationToken>())
            .Returns(new ProjectPatchConfig
            {
                ProjectRootPath = root.Path,
                PatchDownloadPath = "patches"
            });

        var result = await controller.DownloadPatch(42, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<object>>(notFound.Value);
        Assert.False(payload.Status);
        Assert.Equal("Patch file not found", payload.Message);
    }

    [Fact]
    public async Task DownloadPatchScript_ReturnsFileContentResult_WithExpectedNameAndContent()
    {
        var controller = CreateController(userId: 7);
        using var root = new TempDirectory();
        var patchesRoot = Path.Combine(root.Path, "patches");
        Directory.CreateDirectory(patchesRoot);

        var zipPath = Path.Combine(patchesRoot, "patch_reports_sales_26-03-2026_07-08-47.zip");
        await File.WriteAllTextAsync(zipPath, "zip-bytes");

        _patcherRepo.GetPatchByIdAsync(42, 7, Arg.Any<CancellationToken>())
            .Returns(new PatchHistoryRecord
            {
                PatchId = 42,
                ProjectId = 11,
                PageName = "SalesPage",
                DomainName = "Reports",
                SpNames = "[]",
                PatchFilePath = zipPath,
                Status = "Generated"
            });

        _patcherRepo.GetPatchConfigAsync(11, Arg.Any<CancellationToken>())
            .Returns(new ProjectPatchConfig
            {
                ProjectRootPath = root.Path,
                PatchDownloadPath = "patches"
            });

        var result = await controller.DownloadPatchScript(42, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/plain; charset=utf-8", file.ContentType);
        Assert.Equal("patch_reports_sales_26-03-2026_07-08-47.ps1", file.FileDownloadName);

        var script = System.Text.Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("patch_reports_sales_26-03-2026_07-08-47.zip", script);
        Assert.Contains("Get-FileHash -LiteralPath $zipPath -Algorithm SHA256", script);
        Assert.Contains("backup_", script);
    }

    private PatcherController CreateController(int? userId = null)
    {
        var controller = new PatcherController(
            _patcherService,
            _patcherRepo,
            new PatchApplyScriptBuilder(),
            _logger);

        var httpContext = new DefaultHttpContext();
        if (userId.HasValue)
        {
            httpContext.Items["UserId"] = userId.Value;
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; }

        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"acto-patcher-controller-{Guid.NewGuid():N}");
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
