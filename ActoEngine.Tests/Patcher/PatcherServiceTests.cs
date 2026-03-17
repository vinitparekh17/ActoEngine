using ActoEngine.WebApi.Features.ImpactAnalysis;
using ActoEngine.WebApi.Features.Patcher;
using ActoEngine.WebApi.Features.Projects;
using ActoEngine.WebApi.Features.Schema;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.IO.Compression;
using System.Text.Json;

namespace ActoEngine.Tests.Patcher;

public class PatcherServiceTests
{
    private readonly IPatcherRepository _patcherRepo = Substitute.For<IPatcherRepository>();
    private readonly IPageMappingRepository _mappingRepo = Substitute.For<IPageMappingRepository>();
    private readonly IProjectRepository _projectRepo = Substitute.For<IProjectRepository>();
    private readonly IDependencyAnalysisService _dependencyAnalysisService = new DependencyAnalysisService(Substitute.For<ILogger<DependencyAnalysisService>>());
    private readonly ISchemaRepository _schemaRepo = Substitute.For<ISchemaRepository>();
    private readonly IPatchScriptRenderer _scriptRenderer = new PatchScriptRenderer();
    private readonly ILogger<PatcherService> _logger = Substitute.For<ILogger<PatcherService>>();

    [Fact]
    public async Task CheckPatchStatusAsync_UsesApprovedMappings_NotIncomingServiceNames()
    {
        var service = new PatcherService(_patcherRepo, _mappingRepo, _projectRepo, _dependencyAnalysisService, _schemaRepo, _scriptRenderer, _logger);

        var root = Path.Combine(Path.GetTempPath(), $"acto-patcher-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        _patcherRepo.GetPatchConfigAsync(7, Arg.Any<CancellationToken>())
            .Returns(new ProjectPatchConfig
            {
                ProjectRootPath = root,
                ViewDirPath = "Views",
                ScriptDirPath = "Scripts",
                PatchDownloadPath = Path.Combine(root, "patches")
            });

        _mappingRepo.GetApprovedStoredProceduresAsync(7, "Reports", "SalesPage", Arg.Any<CancellationToken>())
            .Returns([]);

        var request = new PatchStatusRequest
        {
            ProjectId = 7,
            PageMappings =
            [
                new PageSpMapping
                {
                    DomainName = "Reports",
                    PageName = "SalesPage",
                    ServiceNames = ["SHOULD_BE_IGNORED"]
                }
            ]
        };

        var result = await service.CheckPatchStatusAsync(request);

        Assert.Single(result);
        Assert.Equal("No approved mappings found for this page", result[0].Reason);
        Assert.False(result[0].NeedsRegeneration);
        await _patcherRepo.DidNotReceiveWithAnyArgs().GetLatestPatchAsync(default, default!, default!, default);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task GeneratePatchAsync_ResolvesStoredProcedures_FromApprovedMappingsOnly()
    {
        var service = new PatcherService(_patcherRepo, _mappingRepo, _projectRepo, _dependencyAnalysisService, _schemaRepo, _scriptRenderer, _logger);

        var root = Path.Combine(Path.GetTempPath(), $"acto-patcher-{Guid.NewGuid():N}");
        var viewsPath = Path.Combine(root, "Views", "Reports");
        var scriptsPath = Path.Combine(root, "Scripts", "Reports");
        var patchPath = Path.Combine(root, "patches");
        Directory.CreateDirectory(viewsPath);
        Directory.CreateDirectory(scriptsPath);
        File.WriteAllText(Path.Combine(viewsPath, "SalesPage.cshtml"), "<h1>sales</h1>");
        File.WriteAllText(Path.Combine(scriptsPath, "SalesPage.js"), "console.log('sales');");

        _projectRepo.GetByIdAsync(7).Returns(new PublicProjectDto
        {
            ProjectId = 7,
            ProjectName = "DemoProject",
            Description = "",
            DatabaseName = "DemoDb",
            IsActive = true,
            IsLinked = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = 1
        });

        _patcherRepo.GetPatchConfigAsync(7, Arg.Any<CancellationToken>())
            .Returns(new ProjectPatchConfig
            {
                ProjectRootPath = root,
                ViewDirPath = "Views",
                ScriptDirPath = "Scripts",
                PatchDownloadPath = patchPath
            });

        _mappingRepo.GetApprovedStoredProceduresAsync(7, "Reports", "SalesPage", Arg.Any<CancellationToken>())
            .Returns([new ApprovedSpDto { StoredProcedure = "sp_approved_only", MappingType = "page_specific" }]);

        _schemaRepo.GetStoredProceduresListAsync(7).Returns(
        [
            new StoredProcedureListDto
            {
                SpId = 101,
                ProcedureName = "sp_approved_only",
                SchemaName = "dbo"
            }
        ]);

        _schemaRepo.GetSpByIdAsync(101).Returns(new StoredProcedureMetadataDto
        {
            SpId = 101,
            ProjectId = 7,
            ClientId = 1,
            SchemaName = "dbo",
            ProcedureName = "sp_approved_only",
            Definition = "CREATE PROCEDURE [dbo].[sp_approved_only] AS SELECT 1",
            CreatedAt = DateTime.UtcNow
        });

        _patcherRepo.GetSpOutboundDependenciesAsync(7, 101, Arg.Any<CancellationToken>())
            .Returns([]);
        _patcherRepo.GetSpProcedureDependenciesAsync(7, 101, Arg.Any<CancellationToken>())
            .Returns([]);
        _patcherRepo.GetSpColumnDependenciesAsync(7, 101, Arg.Any<CancellationToken>())
            .Returns([]);
        _patcherRepo.SavePatchHistoryAsync(Arg.Any<PatchHistoryRecord>(), Arg.Any<List<PatchPageEntry>>(), Arg.Any<CancellationToken>())
            .Returns(501);

        var request = new PatchGenerationRequest
        {
            ProjectId = 7,
            PageMappings =
            [
                new PageSpMapping
                {
                    DomainName = "Reports",
                    PageName = "SalesPage",
                    ServiceNames = ["SHOULD_BE_IGNORED"]
                }
            ]
        };

        var response = await service.GeneratePatchAsync(request, userId: 1);

        Assert.Equal(501, response.PatchId);
        Assert.Contains(response.FilesIncluded, file => file.EndsWith("compatibility.sql", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.FilesIncluded, file => file.EndsWith("update.sql", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(response.Warnings, w => w.Contains("SHOULD_BE_IGNORED", StringComparison.OrdinalIgnoreCase));

        await _mappingRepo.Received(1).GetApprovedStoredProceduresAsync(7, "Reports", "SalesPage", Arg.Any<CancellationToken>());
        await _patcherRepo.Received(1).GetSpOutboundDependenciesAsync(7, 101, Arg.Any<CancellationToken>());
        await _patcherRepo.Received(1).GetSpProcedureDependenciesAsync(7, 101, Arg.Any<CancellationToken>());
        await _patcherRepo.Received(1).GetSpColumnDependenciesAsync(7, 101, Arg.Any<CancellationToken>());

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task GeneratePatchAsync_IncludesNestedProceduresAndNewArtifacts()
    {
        var service = new PatcherService(_patcherRepo, _mappingRepo, _projectRepo, _dependencyAnalysisService, _schemaRepo, _scriptRenderer, _logger);

        var root = Path.Combine(Path.GetTempPath(), $"acto-patcher-{Guid.NewGuid():N}");
        var viewsPath = Path.Combine(root, "Views", "Reports");
        var scriptsPath = Path.Combine(root, "Scripts", "Reports");
        var patchPath = Path.Combine(root, "patches");
        Directory.CreateDirectory(viewsPath);
        Directory.CreateDirectory(scriptsPath);
        File.WriteAllText(Path.Combine(viewsPath, "SalesPage.cshtml"), "<h1>sales</h1>");
        File.WriteAllText(Path.Combine(scriptsPath, "SalesPage.js"), "console.log('sales');");

        _projectRepo.GetByIdAsync(7).Returns(new PublicProjectDto
        {
            ProjectId = 7,
            ProjectName = "DemoProject",
            Description = "",
            DatabaseName = "DemoDb",
            IsActive = true,
            IsLinked = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = 1
        });

        _patcherRepo.GetPatchConfigAsync(7, Arg.Any<CancellationToken>())
            .Returns(new ProjectPatchConfig
            {
                ProjectRootPath = root,
                ViewDirPath = "Views",
                ScriptDirPath = "Scripts",
                PatchDownloadPath = patchPath
            });

        _mappingRepo.GetApprovedStoredProceduresAsync(7, "Reports", "SalesPage", Arg.Any<CancellationToken>())
            .Returns([new ApprovedSpDto { StoredProcedure = "dbo.sp_root", MappingType = "page_specific" }]);

        _schemaRepo.GetStoredProceduresListAsync(7).Returns(
        [
            new StoredProcedureListDto { SpId = 101, ProcedureName = "sp_root", SchemaName = "dbo" },
            new StoredProcedureListDto { SpId = 102, ProcedureName = "sp_child", SchemaName = "dbo" }
        ]);

        _schemaRepo.GetSpByIdAsync(101).Returns(new StoredProcedureMetadataDto
        {
            SpId = 101,
            ProjectId = 7,
            ClientId = 1,
            SchemaName = "dbo",
            ProcedureName = "sp_root",
            Definition = "CREATE PROCEDURE [dbo].[sp_root] AS EXEC [dbo].[sp_child]",
            CreatedAt = DateTime.UtcNow
        });

        _schemaRepo.GetSpByIdAsync(102).Returns(new StoredProcedureMetadataDto
        {
            SpId = 102,
            ProjectId = 7,
            ClientId = 1,
            SchemaName = "dbo",
            ProcedureName = "sp_child",
            Definition = "CREATE PROCEDURE [dbo].[sp_child] AS SELECT 1",
            CreatedAt = DateTime.UtcNow
        });

        _patcherRepo.GetSpProcedureDependenciesAsync(7, 101, Arg.Any<CancellationToken>())
            .Returns([new SpProcedureDependencyRow { SpId = 102, ProcedureName = "sp_child", SchemaName = "dbo" }]);
        _patcherRepo.GetSpProcedureDependenciesAsync(7, 102, Arg.Any<CancellationToken>())
            .Returns([]);
        _patcherRepo.GetSpOutboundDependenciesAsync(7, 101, Arg.Any<CancellationToken>())
            .Returns([]);
        _patcherRepo.GetSpOutboundDependenciesAsync(7, 102, Arg.Any<CancellationToken>())
            .Returns([]);
        _patcherRepo.GetSpColumnDependenciesAsync(7, 101, Arg.Any<CancellationToken>())
            .Returns([]);
        _patcherRepo.GetSpColumnDependenciesAsync(7, 102, Arg.Any<CancellationToken>())
            .Returns([]);
        _patcherRepo.SavePatchHistoryAsync(Arg.Any<PatchHistoryRecord>(), Arg.Any<List<PatchPageEntry>>(), Arg.Any<CancellationToken>())
            .Returns(777);

        var response = await service.GeneratePatchAsync(new PatchGenerationRequest
        {
            ProjectId = 7,
            PageMappings =
            [
                new PageSpMapping
                {
                    DomainName = "Reports",
                    PageName = "SalesPage",
                    ServiceNames = ["SHOULD_BE_IGNORED"]
                }
            ]
        }, userId: 1);

        Assert.Contains(response.FilesIncluded, file => file.EndsWith("rollback.sql", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.FilesIncluded, file => file.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(response.DownloadPath));

        using (var archive = ZipFile.OpenRead(response.DownloadPath))
        {
            Assert.NotNull(archive.GetEntry(archive.Entries.Single(e => e.FullName.EndsWith("rollback.sql", StringComparison.OrdinalIgnoreCase)).FullName));
            var manifestEntry = archive.Entries.Single(e => e.FullName.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase));
            using var manifestStream = manifestEntry.Open();
            using var reader = new StreamReader(manifestStream);
            var manifestJson = await reader.ReadToEndAsync();

            using var document = JsonDocument.Parse(manifestJson);
            var procedures = document.RootElement.GetProperty("Procedures").EnumerateArray().Select(p => p.GetProperty("ProcedureName").GetString()).ToList();
            Assert.Contains("sp_root", procedures);
            Assert.Contains("sp_child", procedures);
        }

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task GeneratePatchAsync_WarnsWhenProcedureContainsDynamicSql()
    {
        var service = new PatcherService(_patcherRepo, _mappingRepo, _projectRepo, _dependencyAnalysisService, _schemaRepo, _scriptRenderer, _logger);

        var root = Path.Combine(Path.GetTempPath(), $"acto-patcher-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "Views", "Reports"));
        Directory.CreateDirectory(Path.Combine(root, "Scripts", "Reports"));
        File.WriteAllText(Path.Combine(root, "Views", "Reports", "SalesPage.cshtml"), "<h1>sales</h1>");
        File.WriteAllText(Path.Combine(root, "Scripts", "Reports", "SalesPage.js"), "console.log('sales');");

        _projectRepo.GetByIdAsync(7).Returns(new PublicProjectDto
        {
            ProjectId = 7,
            ProjectName = "DemoProject",
            Description = "",
            DatabaseName = "DemoDb",
            IsActive = true,
            IsLinked = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = 1
        });

        _patcherRepo.GetPatchConfigAsync(7, Arg.Any<CancellationToken>())
            .Returns(new ProjectPatchConfig
            {
                ProjectRootPath = root,
                ViewDirPath = "Views",
                ScriptDirPath = "Scripts",
                PatchDownloadPath = Path.Combine(root, "patches")
            });

        _mappingRepo.GetApprovedStoredProceduresAsync(7, "Reports", "SalesPage", Arg.Any<CancellationToken>())
            .Returns([new ApprovedSpDto { StoredProcedure = "dbo.sp_dynamic", MappingType = "page_specific" }]);

        _schemaRepo.GetStoredProceduresListAsync(7).Returns(
        [
            new StoredProcedureListDto { SpId = 201, ProcedureName = "sp_dynamic", SchemaName = "dbo" }
        ]);

        _schemaRepo.GetSpByIdAsync(201).Returns(new StoredProcedureMetadataDto
        {
            SpId = 201,
            ProjectId = 7,
            ClientId = 1,
            SchemaName = "dbo",
            ProcedureName = "sp_dynamic",
            Definition = "CREATE PROCEDURE [dbo].[sp_dynamic] AS BEGIN DECLARE @sql NVARCHAR(MAX) = ''SELECT 1''; EXEC(@sql); END",
            CreatedAt = DateTime.UtcNow
        });

        _patcherRepo.GetSpProcedureDependenciesAsync(7, 201, Arg.Any<CancellationToken>())
            .Returns([]);
        _patcherRepo.GetSpOutboundDependenciesAsync(7, 201, Arg.Any<CancellationToken>())
            .Returns([]);
        _patcherRepo.GetSpColumnDependenciesAsync(7, 201, Arg.Any<CancellationToken>())
            .Returns([]);
        _patcherRepo.SavePatchHistoryAsync(Arg.Any<PatchHistoryRecord>(), Arg.Any<List<PatchPageEntry>>(), Arg.Any<CancellationToken>())
            .Returns(901);

        var response = await service.GeneratePatchAsync(new PatchGenerationRequest
        {
            ProjectId = 7,
            PageMappings =
            [
                new PageSpMapping
                {
                    DomainName = "Reports",
                    PageName = "SalesPage",
                    ServiceNames = ["IGNORED"]
                }
            ]
        }, userId: 1);

        Assert.Equal(901, response.PatchId);
        Assert.Contains(response.Warnings, warning => warning.Contains("dynamic SQL", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.FilesIncluded, file => file.EndsWith("compatibility.sql", StringComparison.OrdinalIgnoreCase));

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task GeneratePatchAsync_UsesBestEffortTableResolution_ForDynamicSqlStrings()
    {
        var service = new PatcherService(_patcherRepo, _mappingRepo, _projectRepo, _dependencyAnalysisService, _schemaRepo, _scriptRenderer, _logger);

        var root = Path.Combine(Path.GetTempPath(), $"acto-patcher-{Guid.NewGuid():N}");
        var viewsPath = Path.Combine(root, "Views", "Reports");
        var scriptsPath = Path.Combine(root, "Scripts", "Reports");
        var patchPath = Path.Combine(root, "patches");
        Directory.CreateDirectory(viewsPath);
        Directory.CreateDirectory(scriptsPath);
        File.WriteAllText(Path.Combine(viewsPath, "SalesPage.cshtml"), "<h1>sales</h1>");
        File.WriteAllText(Path.Combine(scriptsPath, "SalesPage.js"), "console.log('sales');");

        _projectRepo.GetByIdAsync(7).Returns(new PublicProjectDto
        {
            ProjectId = 7,
            ProjectName = "DemoProject",
            Description = "",
            DatabaseName = "DemoDb",
            IsActive = true,
            IsLinked = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = 1
        });

        _patcherRepo.GetPatchConfigAsync(7, Arg.Any<CancellationToken>())
            .Returns(new ProjectPatchConfig
            {
                ProjectRootPath = root,
                ViewDirPath = "Views",
                ScriptDirPath = "Scripts",
                PatchDownloadPath = patchPath
            });

        _mappingRepo.GetApprovedStoredProceduresAsync(7, "Reports", "SalesPage", Arg.Any<CancellationToken>())
            .Returns([new ApprovedSpDto { StoredProcedure = "dbo.sp_dynamic_table", MappingType = "page_specific" }]);

        _schemaRepo.GetStoredProceduresListAsync(7).Returns(
        [
            new StoredProcedureListDto { SpId = 301, ProcedureName = "sp_dynamic_table", SchemaName = "dbo" }
        ]);

        _schemaRepo.GetSpByIdAsync(301).Returns(new StoredProcedureMetadataDto
        {
            SpId = 301,
            ProjectId = 7,
            ClientId = 1,
            SchemaName = "dbo",
            ProcedureName = "sp_dynamic_table",
            Definition = "CREATE PROCEDURE [dbo].[sp_dynamic_table] AS BEGIN DECLARE @sql NVARCHAR(MAX) = ''SELECT * FROM dbo.Orders''; EXEC(@sql); END",
            CreatedAt = DateTime.UtcNow
        });

        _schemaRepo.GetStoredTablesAsync(7).Returns(
        [
            new TableMetadataDto
            {
                TableId = 501,
                ProjectId = 7,
                TableName = "Orders",
                SchemaName = "dbo",
                CreatedAt = DateTime.UtcNow
            }
        ]);

        _schemaRepo.GetTableByIdAsync(501).Returns(new TableMetadataDto
        {
            TableId = 501,
            ProjectId = 7,
            TableName = "Orders",
            SchemaName = "dbo",
            CreatedAt = DateTime.UtcNow
        });

        _schemaRepo.GetStoredColumnsAsync(501).Returns(
        [
            new ColumnMetadataDto
            {
                ColumnId = 1,
                TableId = 501,
                ColumnName = "OrderId",
                DataType = "int",
                IsNullable = false,
                IsPrimaryKey = true,
                IsIdentity = true
            }
        ]);
        _schemaRepo.GetStoredIndexesAsync(501).Returns([]);
        _schemaRepo.GetStoredForeignKeysAsync(501).Returns([]);

        _patcherRepo.GetSpProcedureDependenciesAsync(7, 301, Arg.Any<CancellationToken>())
            .Returns([]);
        _patcherRepo.GetSpOutboundDependenciesAsync(7, 301, Arg.Any<CancellationToken>())
            .Returns([]);
        _patcherRepo.GetSpColumnDependenciesAsync(7, 301, Arg.Any<CancellationToken>())
            .Returns([]);
        _patcherRepo.SavePatchHistoryAsync(Arg.Any<PatchHistoryRecord>(), Arg.Any<List<PatchPageEntry>>(), Arg.Any<CancellationToken>())
            .Returns(902);

        var response = await service.GeneratePatchAsync(new PatchGenerationRequest
        {
            ProjectId = 7,
            PageMappings =
            [
                new PageSpMapping
                {
                    DomainName = "Reports",
                    PageName = "SalesPage",
                    ServiceNames = ["IGNORED"]
                }
            ]
        }, userId: 1);

        using (var archive = ZipFile.OpenRead(response.DownloadPath))
        {
            var manifestEntry = archive.Entries.Single(e => e.FullName.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase));
            using var manifestStream = manifestEntry.Open();
            using var reader = new StreamReader(manifestStream);
            var manifestJson = await reader.ReadToEndAsync();

            using var document = JsonDocument.Parse(manifestJson);
            var tables = document.RootElement.GetProperty("Tables").EnumerateArray().Select(t => t.GetProperty("TableName").GetString()).ToList();
            Assert.Contains("Orders", tables);
        }
        Assert.Contains(response.Warnings, warning => warning.Contains("dynamic SQL", StringComparison.OrdinalIgnoreCase));
        Directory.Delete(root, recursive: true);
    }
}
