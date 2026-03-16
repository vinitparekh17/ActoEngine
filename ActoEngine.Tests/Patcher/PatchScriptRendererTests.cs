using ActoEngine.WebApi.Features.Patcher;

namespace ActoEngine.Tests.Patcher;

public class PatchScriptRendererTests
{
    [Fact]
    public void Render_ProducesCompatibilityUpdateRollbackAndManifestArtifacts()
    {
        var renderer = new PatchScriptRenderer();
        var manifest = new PatchManifest
        {
            ProjectId = 7,
            Pages =
            [
                new PatchManifestPage
                {
                    DomainName = "Reports",
                    PageName = "SalesPage",
                    IsNewPage = false
                }
            ],
            Procedures =
            [
                new PatchProcedureSnapshot
                {
                    SpId = 10,
                    ProcedureName = "sp_report",
                    SchemaName = "dbo",
                    Definition = "CREATE PROCEDURE [dbo].[sp_report] AS SELECT 1",
                    IsShared = false
                }
            ],
            Tables =
            [
                new PatchTableSnapshot
                {
                    TableId = 20,
                    TableName = "Orders",
                    SchemaName = "dbo",
                    Columns =
                    [
                        new PatchColumnSnapshot
                        {
                            ColumnId = 1,
                            ColumnName = "OrderId",
                            DataType = "int",
                            IsNullable = false,
                            IsPrimaryKey = true,
                            IsIdentity = true
                        },
                        new PatchColumnSnapshot
                        {
                            ColumnId = 2,
                            ColumnName = "CustomerId",
                            DataType = "int",
                            IsNullable = true
                        }
                    ],
                    Indexes =
                    [
                        new PatchIndexSnapshot
                        {
                            IndexName = "IX_Orders_CustomerId",
                            IsUnique = false,
                            IsPrimaryKey = false,
                            Columns =
                            [
                                new PatchIndexColumnSnapshot
                                {
                                    ColumnId = 2,
                                    ColumnName = "CustomerId",
                                    ColumnOrder = 1
                                }
                            ]
                        }
                    ],
                    ForeignKeys = [],
                    RequiredColumnNames = ["CustomerId"]
                }
            ],
            Warnings = [],
            BlockingIssues = [],
            GeneratedAtUtc = DateTime.UtcNow
        };

        var artifacts = renderer.Render(manifest);

        Assert.Contains("REPAIRABLE", artifacts.CompatibilitySql);
        Assert.Contains("THROW 51000", artifacts.UpdateSql);
        Assert.Contains("CREATE OR ALTER PROCEDURE", artifacts.UpdateSql);
        Assert.Contains("Rollback Script", artifacts.RollbackSql);
        Assert.Contains("\"ProcedureName\": \"sp_report\"", artifacts.ManifestJson);
    }
}
