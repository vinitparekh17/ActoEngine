using ActoEngine.WebApi.Features.Patcher;
using System.Text.RegularExpressions;

namespace ActoEngine.Tests.Patcher;

public class PatchScriptRendererTests
{
    private static PatchManifest CreateTestManifest(
        List<PatchColumnSnapshot>? columns = null,
        List<PatchIndexSnapshot>? indexes = null,
        List<PatchForeignKeySnapshot>? foreignKeys = null,
        List<string>? requiredColumns = null,
        List<PatchProcedureSnapshot>? procedures = null)
    {
        columns ??=
        [
            new PatchColumnSnapshot
            {
                ColumnId = 1, ColumnName = "OrderId", DataType = "int",
                IsNullable = false, IsPrimaryKey = true, IsIdentity = true
            },
            new PatchColumnSnapshot
            {
                ColumnId = 2, ColumnName = "CustomerId", DataType = "int",
                IsNullable = true
            }
        ];

        indexes ??=
        [
            new PatchIndexSnapshot
            {
                IndexName = "IX_Orders_CustomerId", IsUnique = false, IsPrimaryKey = false,
                Columns = [new PatchIndexColumnSnapshot { ColumnId = 2, ColumnName = "CustomerId", ColumnOrder = 1 }]
            }
        ];

        procedures ??=
        [
            new PatchProcedureSnapshot
            {
                SpId = 10, ProcedureName = "sp_report", SchemaName = "dbo",
                Definition = "CREATE PROCEDURE [dbo].[sp_report] AS SELECT 1", IsShared = false
            }
        ];

        return new PatchManifest
        {
            ProjectId = 7,
            Pages = [new PatchManifestPage { DomainName = "Reports", PageName = "SalesPage", IsNewPage = false }],
            Procedures = procedures,
            Tables =
            [
                new PatchTableSnapshot
                {
                    TableId = 20, TableName = "Orders", SchemaName = "dbo",
                    Columns = columns,
                    Indexes = indexes,
                    ForeignKeys = foreignKeys ?? [],
                    RequiredColumnNames = requiredColumns ?? ["CustomerId"]
                }
            ],
            Warnings = [],
            BlockingIssues = [],
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    [Fact]
    public void Render_ProducesCompatibilityUpdateRollbackAndManifestArtifacts()
    {
        var renderer = new PatchScriptRenderer();
        var artifacts = renderer.Render(CreateTestManifest());

        Assert.Contains("REPAIRABLE", artifacts.CompatibilitySql);
        Assert.Contains("THROW 51000", artifacts.UpdateSql);
        Assert.Contains("CREATE OR ALTER PROCEDURE", artifacts.UpdateSql);
        Assert.Contains("Rollback Script", artifacts.RollbackSql);
        Assert.Contains("\"ProcedureName\": \"sp_report\"", artifacts.ManifestJson);
    }

    [Fact]
    public void Render_CompatibilitySql_DoesNotProduceEmptyElseBlocks_WhenNoExistingTableRepairsArePossible()
    {
        var renderer = new PatchScriptRenderer();
        var manifest = CreateTestManifest(
            columns:
            [
                new PatchColumnSnapshot
                {
                    ColumnId = 1,
                    ColumnName = "OrderId",
                    DataType = "int",
                    IsNullable = false
                }
            ],
            indexes: [],
            requiredColumns: ["OrderId"]);

        var artifacts = renderer.Render(manifest);

        var emptyElsePattern = new Regex(@"ELSE\s*\r?\n\s*BEGIN\s*\r?\n\s*END", RegexOptions.IgnoreCase);
        Assert.DoesNotMatch(emptyElsePattern, artifacts.CompatibilitySql);
    }

    [Fact]
    public void Render_CompatibilitySql_NestsColumnChecksInsideTableExists()
    {
        var renderer = new PatchScriptRenderer();
        var artifacts = renderer.Render(CreateTestManifest());
        var sql = artifacts.CompatibilitySql.Replace("\r\n", "\n");

        Assert.Contains("ELSE\nBEGIN\n    INSERT INTO @Issues VALUES ('OK', 'TABLE',", sql);
        Assert.Contains("    IF NOT EXISTS (SELECT 1 FROM sys.columns", sql);
        Assert.Contains("    IF NOT EXISTS (SELECT 1 FROM sys.indexes", sql);
    }

    [Fact]
    public void Render_CompatibilitySql_IdentityColumn_DoesNotCheckIsNullable()
    {
        var renderer = new PatchScriptRenderer();
        var manifest = CreateTestManifest(requiredColumns: ["OrderId"]);
        var artifacts = renderer.Render(manifest);
        var sql = artifacts.CompatibilitySql;

        var orderIdSection = ExtractColumnMismatchBlock(sql, "OrderId");
        Assert.Contains("c.is_identity", orderIdSection);
        Assert.DoesNotContain("c.is_nullable", orderIdSection);
    }

    [Fact]
    public void Render_CompatibilitySql_NonIdentityColumn_DoesNotCheckIsIdentity()
    {
        var renderer = new PatchScriptRenderer();
        var manifest = CreateTestManifest(requiredColumns: ["CustomerId"]);
        var artifacts = renderer.Render(manifest);
        var sql = artifacts.CompatibilitySql;

        var customerIdSection = ExtractColumnMismatchBlock(sql, "CustomerId");
        Assert.DoesNotContain("c.is_identity", customerIdSection);
    }

    [Fact]
    public void Render_CompatibilitySql_ExplicitNotNullNonIdentity_ChecksIsNullable()
    {
        var renderer = new PatchScriptRenderer();

        var columns = new List<PatchColumnSnapshot>
        {
            new()
            {
                ColumnId = 3, ColumnName = "Status", DataType = "nvarchar",
                MaxLength = 100, IsNullable = false, IsIdentity = false
            }
        };
        var manifest = CreateTestManifest(columns: columns, indexes: [], requiredColumns: ["Status"]);
        var artifacts = renderer.Render(manifest);
        var sql = artifacts.CompatibilitySql;

        var statusSection = ExtractColumnMismatchBlock(sql, "Status");
        Assert.Contains("c.is_nullable <> 0", statusSection);
    }

    [Fact]
    public void Render_CompatibilitySql_NonNumericColumn_DoesNotCheckPrecisionOrScale()
    {
        var renderer = new PatchScriptRenderer();

        var columns = new List<PatchColumnSnapshot>
        {
            new()
            {
                ColumnId = 3,
                ColumnName = "OccurredAt",
                DataType = "datetime",
                Precision = 23,
                Scale = 3,
                IsNullable = true
            }
        };
        var manifest = CreateTestManifest(columns: columns, indexes: [], requiredColumns: ["OccurredAt"]);
        var artifacts = renderer.Render(manifest);
        var sql = artifacts.CompatibilitySql;

        var occurredAtSection = ExtractColumnMismatchBlock(sql, "OccurredAt");
        Assert.DoesNotContain("c.precision", occurredAtSection);
        Assert.DoesNotContain("c.scale", occurredAtSection);
    }

    [Fact]
    public void Render_CompatibilitySql_DecimalColumn_ChecksPrecisionAndScale()
    {
        var renderer = new PatchScriptRenderer();

        var columns = new List<PatchColumnSnapshot>
        {
            new()
            {
                ColumnId = 3,
                ColumnName = "Amount",
                DataType = "decimal",
                Precision = 18,
                Scale = 2,
                IsNullable = true
            }
        };
        var manifest = CreateTestManifest(columns: columns, indexes: [], requiredColumns: ["Amount"]);
        var artifacts = renderer.Render(manifest);
        var sql = artifacts.CompatibilitySql;

        var amountSection = ExtractColumnMismatchBlock(sql, "Amount");
        Assert.Contains("c.precision <> 18", amountSection);
        Assert.Contains("c.scale <> 2", amountSection);
    }

    [Fact]
    public void Render_CompatibilitySql_SkipsDboSchemaChecks_And_IncludesProcedureSchemas()
    {
        var renderer = new PatchScriptRenderer();
        var manifest = CreateTestManifest(
            procedures:
            [
                new PatchProcedureSnapshot
                {
                    SpId = 11,
                    ProcedureName = "sp_report",
                    SchemaName = "reporting",
                    Definition = "CREATE PROCEDURE [reporting].[sp_report] AS SELECT 1",
                    IsShared = false
                }
            ]);

        var artifacts = renderer.Render(manifest);
        var sql = artifacts.CompatibilitySql;

        Assert.DoesNotContain("IF SCHEMA_ID(N'dbo') IS NULL", sql);
        Assert.Contains("IF SCHEMA_ID(N'reporting') IS NULL", sql);
        Assert.Contains("IF SCHEMA_ID(N'reporting') IS NULL EXEC(N'CREATE SCHEMA [reporting]');", sql);
    }

    [Fact]
    public void Render_CompatibilitySql_DefaultRepairs_AreLimitedToRequiredColumns()
    {
        var renderer = new PatchScriptRenderer();
        var manifest = CreateTestManifest(
            columns:
            [
                new PatchColumnSnapshot
                {
                    ColumnId = 1, ColumnName = "OrderId", DataType = "int",
                    IsNullable = false, IsPrimaryKey = true, IsIdentity = true
                },
                new PatchColumnSnapshot
                {
                    ColumnId = 2, ColumnName = "Status", DataType = "int",
                    IsNullable = true, DefaultValue = "((0))"
                },
                new PatchColumnSnapshot
                {
                    ColumnId = 3, ColumnName = "OptionalFlag", DataType = "bit",
                    IsNullable = true, DefaultValue = "((1))"
                }
            ],
            indexes: [],
            requiredColumns: ["Status"]);

        var artifacts = renderer.Render(manifest);
        var sql = artifacts.CompatibilitySql;

        Assert.Contains("DF_Orders_Status", sql);
        Assert.Contains("N'[dbo].[Orders].[Status]', 'Default constraint is missing.'", sql);
        Assert.DoesNotContain("DF_Orders_OptionalFlag", sql);
        Assert.DoesNotContain("N'[dbo].[Orders].[OptionalFlag]', 'Default constraint is missing.'", sql);
    }

    [Fact]
    public void Render_CompatibilitySql_UsesSpecificBlockerMessages()
    {
        var renderer = new PatchScriptRenderer();
        var manifest = CreateTestManifest(
            columns:
            [
                new PatchColumnSnapshot
                {
                    ColumnId = 3, ColumnName = "Status", DataType = "int",
                    IsNullable = false, IsIdentity = false
                }
            ],
            indexes: [],
            requiredColumns: ["Status"]);

        var artifacts = renderer.Render(manifest);
        var sql = artifacts.CompatibilitySql;

        Assert.Contains("Required column is missing and cannot be auto-added safely because expected column is INT NOT NULL with no default.", sql);
        Assert.Contains("Column metadata mismatch. Expected INT NOT NULL.", sql);
    }

    private static string ExtractColumnMismatchBlock(string sql, string columnName)
    {
        var pattern = new Regex(
            $@"IF EXISTS \(SELECT 1.*?c\.name = N'{columnName}'.*?\)\)",
            RegexOptions.Singleline);
        var match = pattern.Match(sql);
        return match.Success ? match.Value : string.Empty;
    }
}
