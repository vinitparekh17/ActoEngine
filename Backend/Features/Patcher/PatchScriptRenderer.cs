using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ActoEngine.WebApi.Features.Patcher;

public interface IPatchScriptRenderer
{
    PatchArchiveArtifacts Render(PatchManifest manifest);
}

internal sealed partial class PatchScriptRenderer : IPatchScriptRenderer
{
    public PatchArchiveArtifacts Render(PatchManifest manifest)
    {
        return new PatchArchiveArtifacts
        {
            CompatibilitySql = RenderCompatibilitySql(manifest),
            UpdateSql = RenderUpdateSql(manifest),
            RollbackSql = RenderRollbackSql(manifest),
            ManifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true })
        };
    }

    private static string RenderCompatibilitySql(PatchManifest manifest)
    {
        var sb = new StringBuilder();

        AppendHeader(sb, "Compatibility Check Script", manifest);
        sb.AppendLine("SET NOCOUNT ON;");
        sb.AppendLine("DECLARE @RepairMode BIT = 0;");
        sb.AppendLine("DECLARE @Issues TABLE (Severity NVARCHAR(20), Category NVARCHAR(50), ObjectName NVARCHAR(512), Message NVARCHAR(MAX));");
        sb.AppendLine();

        // ═════════════════════════════════════════════════════════
        // PHASE 1: Pre-Flight Validation
        // ═════════════════════════════════════════════════════════
        foreach (var procedure in manifest.Procedures.OrderBy(p => p.IsShared).ThenBy(p => p.SchemaName).ThenBy(p => p.ProcedureName))
        {
            var procName = QualifiedName(procedure.SchemaName, procedure.ProcedureName);
            var procLiteral = SqlUnicodeLiteral(procName);
            sb.AppendLine($"IF OBJECT_ID({procLiteral}, 'P') IS NULL");
            sb.AppendLine($"    INSERT INTO @Issues VALUES ('REPAIRABLE', 'PROCEDURE', {procLiteral}, 'Stored procedure is missing and will be created by update.sql.');");
            sb.AppendLine("ELSE");
            sb.AppendLine($"    INSERT INTO @Issues VALUES ('OK', 'PROCEDURE', {procLiteral}, 'Stored procedure exists.');");
            sb.AppendLine();
        }

        // Schema checks - emit once per unique schema
        var uniqueSchemas = manifest.Tables.Select(t => t.SchemaName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
        foreach (var schema in uniqueSchemas)
        {
            AppendSchemaChecks(sb, schema);
        }

        foreach (var table in manifest.Tables.OrderBy(t => t.SchemaName).ThenBy(t => t.TableName))
        {
            AppendTableChecks(sb, table);
            AppendColumnChecks(sb, table);
            AppendIndexChecks(sb, table);
            AppendForeignKeyChecks(sb, table);
        }

        sb.AppendLine("DECLARE @RepairableCount INT = (SELECT COUNT(*) FROM @Issues WHERE Severity = 'REPAIRABLE');");
        sb.AppendLine("DECLARE @BlockerCount INT = (SELECT COUNT(*) FROM @Issues WHERE Severity = 'BLOCKER');");
        sb.AppendLine("SELECT Severity, Category, ObjectName, Message FROM @Issues ORDER BY CASE Severity WHEN 'BLOCKER' THEN 1 WHEN 'REPAIRABLE' THEN 2 ELSE 3 END, Category, ObjectName;");
        sb.AppendLine("PRINT CONCAT('Repairable issues: ', @RepairableCount);");
        sb.AppendLine("PRINT CONCAT('Blockers: ', @BlockerCount);");
        sb.AppendLine("IF @BlockerCount > 0");
        sb.AppendLine("    PRINT 'Compatibility result: BLOCKED';");
        sb.AppendLine("ELSE IF @RepairableCount > 0");
        sb.AppendLine("    PRINT 'Compatibility result: REPAIRABLE';");
        sb.AppendLine("ELSE");
        sb.AppendLine("    PRINT 'Compatibility result: READY';");
        sb.AppendLine();

        // ═════════════════════════════════════════════════════════
        // PHASE 2: Repair Mode
        // ═════════════════════════════════════════════════════════
        sb.AppendLine("IF @RepairMode = 1");
        sb.AppendLine("BEGIN");
        sb.AppendLine("    IF @BlockerCount > 0");
        sb.AppendLine("    BEGIN");
        sb.AppendLine("        ;THROW 51000, 'Repair aborted: compatibility blockers remain. Fix blockers manually before running repairs.', 1;");
        sb.AppendLine("    END");
        sb.AppendLine("    IF @RepairableCount = 0");
        sb.AppendLine("    BEGIN");
        sb.AppendLine("        PRINT 'No repairable issues found. Repair mode skipped.';");
        sb.AppendLine("    END");
        sb.AppendLine("    ELSE");
        sb.AppendLine("    BEGIN");
        sb.AppendLine("        PRINT '=== Phase 2: Applying Repairs ===';");
        sb.AppendLine("        BEGIN TRY");
        sb.AppendLine("            BEGIN TRANSACTION;");
        sb.AppendLine();

        // 1. Create missing schemas
        foreach (var schema in uniqueSchemas.Append("dbo").Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s))
        {
            var schemaLiteral = SqlUnicodeLiteral(schema);
            sb.AppendLine($"            IF SCHEMA_ID({schemaLiteral}) IS NULL EXEC(N'CREATE SCHEMA {Bracket(schema)}');");
        }
        sb.AppendLine();

        // 2. Repair tables, columns, constraints
        foreach (var table in manifest.Tables.OrderBy(t => t.SchemaName).ThenBy(t => t.TableName))
        {
            AppendTableRepairs(sb, table);
        }

        // 3. Create missing indexes
        foreach (var table in manifest.Tables.OrderBy(t => t.SchemaName).ThenBy(t => t.TableName))
        {
            AppendIndexRepairs(sb, table);
        }

        // 4. Create missing foreign keys
        foreach (var table in manifest.Tables.OrderBy(t => t.SchemaName).ThenBy(t => t.TableName))
        {
            AppendForeignKeyRepairs(sb, table);
        }

        sb.AppendLine("            COMMIT TRANSACTION;");
        sb.AppendLine("            PRINT '=== Repair completed successfully ===';");
        sb.AppendLine("        END TRY");
        sb.AppendLine("        BEGIN CATCH");
        sb.AppendLine("            IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;");
        sb.AppendLine("            PRINT 'REPAIR ERROR: ' + ERROR_MESSAGE();");
        sb.AppendLine("            ;THROW;");
        sb.AppendLine("        END CATCH");
        sb.AppendLine("    END");
        sb.AppendLine("END");

        return sb.ToString();
    }

    private static string RenderUpdateSql(PatchManifest manifest)
    {
        var sb = new StringBuilder();

        AppendHeader(sb, "Patch Update Script", manifest);
        sb.AppendLine("SET NOCOUNT ON;");
        sb.AppendLine("DECLARE @IncludeSharedSPs BIT = 0;");
        sb.AppendLine("DECLARE @BlockerCount INT = 0;");
        sb.AppendLine();

        foreach (var table in manifest.Tables.OrderBy(t => t.SchemaName).ThenBy(t => t.TableName))
        {
            AppendBlockerPrecheck(sb, table);
        }

        sb.AppendLine("IF @BlockerCount > 0");
        sb.AppendLine("BEGIN");
        sb.AppendLine("    ;THROW 51000, 'Patch blocked: compatibility blockers remain. Run compatibility.sql first and resolve blockers.', 1;");
        sb.AppendLine("END");
        sb.AppendLine();
        sb.AppendLine("BEGIN TRY");
        sb.AppendLine("    BEGIN TRANSACTION;");
        sb.AppendLine();

        foreach (var procedure in manifest.Procedures.Where(p => !p.IsShared).OrderBy(p => p.SchemaName).ThenBy(p => p.ProcedureName))
        {
            AppendProcedureCreateOrAlter(sb, procedure, "    ");
        }

        if (manifest.Procedures.Any(p => p.IsShared))
        {
            sb.AppendLine("    IF @IncludeSharedSPs = 1");
            sb.AppendLine("    BEGIN");
            foreach (var procedure in manifest.Procedures.Where(p => p.IsShared).OrderBy(p => p.SchemaName).ThenBy(p => p.ProcedureName))
            {
                AppendProcedureCreateOrAlter(sb, procedure, "        ");
            }
            sb.AppendLine("    END");
            sb.AppendLine("    ELSE");
            sb.AppendLine("    BEGIN");
            sb.AppendLine("        PRINT 'Skipping shared stored procedures. Set @IncludeSharedSPs = 1 to apply them.';");
            sb.AppendLine("    END");
            sb.AppendLine();
        }

        sb.AppendLine("    COMMIT TRANSACTION;");
        sb.AppendLine("END TRY");
        sb.AppendLine("BEGIN CATCH");
        sb.AppendLine("    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;");
        sb.AppendLine("    ;THROW;");
        sb.AppendLine("END CATCH");

        return sb.ToString();
    }

    private static string RenderRollbackSql(PatchManifest manifest)
    {
        var sb = new StringBuilder();

        AppendHeader(sb, "Rollback Script", manifest);
        sb.AppendLine("SET NOCOUNT ON;");
        sb.AppendLine("DECLARE @IncludeSharedSPs BIT = 0;");
        sb.AppendLine("PRINT 'This rollback script restores stored procedures to the metadata snapshot used to generate the patch.';");
        sb.AppendLine("PRINT 'Additive schema changes are not dropped automatically; review compatibility.sql and update.sql before manual rollback.';");
        sb.AppendLine();

        foreach (var procedure in manifest.Procedures.Where(p => !p.IsShared).OrderBy(p => p.SchemaName).ThenBy(p => p.ProcedureName))
        {
            AppendProcedureCreateOrAlter(sb, procedure, string.Empty);
        }

        if (manifest.Procedures.Any(p => p.IsShared))
        {
            sb.AppendLine("IF @IncludeSharedSPs = 1");
            sb.AppendLine("BEGIN");
            foreach (var procedure in manifest.Procedures.Where(p => p.IsShared).OrderBy(p => p.SchemaName).ThenBy(p => p.ProcedureName))
            {
                AppendProcedureCreateOrAlter(sb, procedure, "    ");
            }
            sb.AppendLine("END");
            sb.AppendLine("ELSE");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    PRINT 'Skipping rollback of shared stored procedures. Set @IncludeSharedSPs = 1 to roll them back.';");
            sb.AppendLine("END");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb, string title, PatchManifest manifest)
    {
        sb.AppendLine("-- =============================================");
        sb.AppendLine($"-- {title}");
        sb.AppendLine($"-- Generated by ActoEngine on {manifest.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"-- Procedures: {string.Join(", ", manifest.Procedures.Select(p => QualifiedName(p.SchemaName, p.ProcedureName)))}");
        sb.AppendLine("-- =============================================");
        sb.AppendLine();
    }

    private static void AppendSchemaChecks(StringBuilder sb, string schemaName)
    {
        var schemaLiteral = SqlUnicodeLiteral(schemaName);
        sb.AppendLine($"IF SCHEMA_ID({schemaLiteral}) IS NULL");
        sb.AppendLine($"    INSERT INTO @Issues VALUES ('REPAIRABLE', 'SCHEMA', {schemaLiteral}, 'Schema is missing and can be created safely.');");
        sb.AppendLine("ELSE");
        sb.AppendLine($"    INSERT INTO @Issues VALUES ('OK', 'SCHEMA', {schemaLiteral}, 'Schema exists.');");
        sb.AppendLine();
    }

    private static void AppendTableChecks(StringBuilder sb, PatchTableSnapshot table)
    {
        var objectLiteral = SqlUnicodeLiteral(QualifiedName(table.SchemaName, table.TableName));
        sb.AppendLine($"IF OBJECT_ID({objectLiteral}, 'U') IS NULL");
        sb.AppendLine($"    INSERT INTO @Issues VALUES ('REPAIRABLE', 'TABLE', {objectLiteral}, 'Table is missing and full metadata is available to create it.');");
        sb.AppendLine("ELSE");
        sb.AppendLine($"    INSERT INTO @Issues VALUES ('OK', 'TABLE', {objectLiteral}, 'Table exists.');");
        sb.AppendLine();
    }

    private static void AppendColumnChecks(StringBuilder sb, PatchTableSnapshot table)
    {
        var objectLiteral = SqlUnicodeLiteral(QualifiedName(table.SchemaName, table.TableName));
        foreach (var column in table.Columns.Where(c => table.RequiredColumnNames.Contains(c.ColumnName, StringComparer.OrdinalIgnoreCase)).OrderBy(c => c.ColumnName))
        {
            var columnLiteral = SqlUnicodeLiteral($"{QualifiedName(table.SchemaName, table.TableName)}.{Bracket(column.ColumnName)}");
            var missingSeverity = column.IsNullable || !string.IsNullOrWhiteSpace(column.DefaultValue) ? "REPAIRABLE" : "BLOCKER";
            sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID({objectLiteral}) AND name = N'{EscapeSql(column.ColumnName)}')");
            sb.AppendLine($"    INSERT INTO @Issues VALUES ('{missingSeverity}', 'COLUMN', {columnLiteral}, 'Required column is missing.');");

            sb.AppendLine("ELSE");
            sb.AppendLine("BEGIN");
            sb.AppendLine($"    IF EXISTS ({BuildColumnMismatchQuery(table, column)})");
            sb.AppendLine($"        INSERT INTO @Issues VALUES ('BLOCKER', 'COLUMN', {columnLiteral}, 'Column metadata is incompatible with the source snapshot.');");
            sb.AppendLine("    ELSE");
            sb.AppendLine($"        INSERT INTO @Issues VALUES ('OK', 'COLUMN', {columnLiteral}, 'Column is compatible.');");

            if (!string.IsNullOrWhiteSpace(column.DefaultValue))
            {
                sb.AppendLine($"    IF NOT EXISTS (SELECT 1 FROM sys.default_constraints dc INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id WHERE dc.parent_object_id = OBJECT_ID({objectLiteral}) AND c.name = N'{EscapeSql(column.ColumnName)}')");
                sb.AppendLine($"        INSERT INTO @Issues VALUES ('REPAIRABLE', 'DEFAULT', {columnLiteral}, 'Default constraint is missing.');");
            }

            sb.AppendLine("END");
            sb.AppendLine();
        }
    }

    private static void AppendIndexChecks(StringBuilder sb, PatchTableSnapshot table)
    {
        var tableLiteral = SqlUnicodeLiteral(QualifiedName(table.SchemaName, table.TableName));
        foreach (var index in table.Indexes.Where(i => !i.IsPrimaryKey).OrderBy(i => i.IndexName))
        {
            var indexLiteral = SqlUnicodeLiteral($"{QualifiedName(table.SchemaName, table.TableName)}.{Bracket(index.IndexName)}");
            sb.AppendLine($"IF OBJECT_ID({tableLiteral}, 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID({tableLiteral}) AND name = N'{EscapeSql(index.IndexName)}')");
            sb.AppendLine($"    INSERT INTO @Issues VALUES ('REPAIRABLE', 'INDEX', {indexLiteral}, 'Required index is missing.');");
            sb.AppendLine();
        }
    }

    private static void AppendForeignKeyChecks(StringBuilder sb, PatchTableSnapshot table)
    {
        var tableLiteral = SqlUnicodeLiteral(QualifiedName(table.SchemaName, table.TableName));
        foreach (var foreignKey in table.ForeignKeys.OrderBy(f => f.ForeignKeyName ?? $"{f.ColumnName}_{f.ReferencedTableName}_{f.ReferencedColumnName}"))
        {
            var fkName = foreignKey.ForeignKeyName ?? $"FK_{table.TableName}_{foreignKey.ReferencedTableName}_{foreignKey.ColumnName}";
            var fkLiteral = SqlUnicodeLiteral($"{QualifiedName(table.SchemaName, table.TableName)}.{Bracket(fkName)}");
            sb.AppendLine($"IF OBJECT_ID({tableLiteral}, 'U') IS NOT NULL AND OBJECT_ID(N'{EscapeSql(QualifiedName(foreignKey.ReferencedSchemaName, foreignKey.ReferencedTableName))}', 'U') IS NOT NULL");
            sb.AppendLine("BEGIN");
            sb.AppendLine($"    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID({tableLiteral}) AND name = N'{EscapeSql(fkName)}')");
            sb.AppendLine($"        INSERT INTO @Issues VALUES ('REPAIRABLE', 'FOREIGN_KEY', {fkLiteral}, 'Required foreign key is missing.');");
            sb.AppendLine("END");
            sb.AppendLine();
        }
    }

    private static void AppendBlockerPrecheck(StringBuilder sb, PatchTableSnapshot table)
    {
        var objectLiteral = SqlUnicodeLiteral(QualifiedName(table.SchemaName, table.TableName));
        foreach (var column in table.Columns.Where(c => table.RequiredColumnNames.Contains(c.ColumnName, StringComparer.OrdinalIgnoreCase)).OrderBy(c => c.ColumnName))
        {
            if (!column.IsNullable && string.IsNullOrWhiteSpace(column.DefaultValue))
            {
                sb.AppendLine($"IF OBJECT_ID({objectLiteral}, 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID({objectLiteral}) AND name = N'{EscapeSql(column.ColumnName)}')");
                sb.AppendLine("    SET @BlockerCount = @BlockerCount + 1;");
            }

            sb.AppendLine($"IF OBJECT_ID({objectLiteral}, 'U') IS NOT NULL AND EXISTS ({BuildColumnMismatchQuery(table, column)})");
            sb.AppendLine("    SET @BlockerCount = @BlockerCount + 1;");
        }
    }

    private static void AppendTableRepairs(StringBuilder sb, PatchTableSnapshot table)
    {
        var qualifiedTable = QualifiedName(table.SchemaName, table.TableName);
        var tableLiteral = SqlUnicodeLiteral(qualifiedTable);
        sb.AppendLine($"            IF OBJECT_ID({tableLiteral}, 'U') IS NULL");
        sb.AppendLine("            BEGIN");
        sb.AppendLine($"                EXEC(N'CREATE TABLE {qualifiedTable} ({string.Join(", ", table.Columns.Select(c => BuildColumnDefinition(c))).Replace("'", "''")})');");
        sb.AppendLine("            END");
        sb.AppendLine("            ELSE");
        sb.AppendLine("            BEGIN");

        foreach (var column in table.Columns.Where(c => table.RequiredColumnNames.Contains(c.ColumnName, StringComparer.OrdinalIgnoreCase)).OrderBy(c => c.ColumnName))
        {
            if (!column.IsNullable && string.IsNullOrWhiteSpace(column.DefaultValue))
            {
                continue;
            }

            sb.AppendLine($"                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID({tableLiteral}) AND name = N'{EscapeSql(column.ColumnName)}')");
            sb.AppendLine($"                    EXEC(N'ALTER TABLE {qualifiedTable} ADD {EscapeForNestedExec(BuildColumnDefinition(column))}');");
        }

        foreach (var column in table.Columns.Where(c => !string.IsNullOrWhiteSpace(c.DefaultValue)).OrderBy(c => c.ColumnName))
        {
            var constraintName = DefaultConstraintName(table, column);
            sb.AppendLine($"                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID({tableLiteral}) AND name = N'{EscapeSql(column.ColumnName)}')");
            sb.AppendLine($"                   AND NOT EXISTS (SELECT 1 FROM sys.default_constraints dc INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id WHERE dc.parent_object_id = OBJECT_ID({tableLiteral}) AND c.name = N'{EscapeSql(column.ColumnName)}')");
            sb.AppendLine($"                    EXEC(N'ALTER TABLE {qualifiedTable} ADD CONSTRAINT {Bracket(constraintName)} DEFAULT {EscapeForNestedExec(column.DefaultValue!)} FOR {Bracket(column.ColumnName)}');");
        }

        sb.AppendLine("            END");
        sb.AppendLine();
    }

    private static void AppendIndexRepairs(StringBuilder sb, PatchTableSnapshot table)
    {
        var qualifiedTable = QualifiedName(table.SchemaName, table.TableName);
        var tableLiteral = SqlUnicodeLiteral(qualifiedTable);
        foreach (var index in table.Indexes.Where(i => !i.IsPrimaryKey).OrderBy(i => i.IndexName))
        {
            var columns = string.Join(", ", index.Columns.OrderBy(c => c.ColumnOrder).Select(c => Bracket(c.ColumnName)));
            var uniqueness = index.IsUnique ? "UNIQUE " : string.Empty;
            sb.AppendLine($"            IF OBJECT_ID({tableLiteral}, 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID({tableLiteral}) AND name = N'{EscapeSql(index.IndexName)}')");
            sb.AppendLine($"                EXEC(N'CREATE {uniqueness}INDEX {Bracket(index.IndexName)} ON {qualifiedTable} ({EscapeForNestedExec(columns)})');");
            sb.AppendLine();
        }
    }

    private static void AppendForeignKeyRepairs(StringBuilder sb, PatchTableSnapshot table)
    {
        var qualifiedTable = QualifiedName(table.SchemaName, table.TableName);
        var tableLiteral = SqlUnicodeLiteral(qualifiedTable);
        foreach (var foreignKey in table.ForeignKeys.OrderBy(f => f.ForeignKeyName ?? $"{f.ColumnName}_{f.ReferencedTableName}_{f.ReferencedColumnName}"))
        {
            var fkName = foreignKey.ForeignKeyName ?? $"FK_{table.TableName}_{foreignKey.ReferencedTableName}_{foreignKey.ColumnName}";
            var referencedTable = QualifiedName(foreignKey.ReferencedSchemaName, foreignKey.ReferencedTableName);
            sb.AppendLine($"            IF OBJECT_ID({tableLiteral}, 'U') IS NOT NULL AND OBJECT_ID(N'{EscapeSql(referencedTable)}', 'U') IS NOT NULL");
            sb.AppendLine($"               AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID({tableLiteral}) AND name = N'{EscapeSql(fkName)}')");
            sb.AppendLine($"                EXEC(N'ALTER TABLE {qualifiedTable} WITH CHECK ADD CONSTRAINT {Bracket(fkName)} FOREIGN KEY ({Bracket(foreignKey.ColumnName)}) REFERENCES {referencedTable} ({Bracket(foreignKey.ReferencedColumnName)}) ON DELETE {EscapeForNestedExec(foreignKey.OnDeleteAction)} ON UPDATE {EscapeForNestedExec(foreignKey.OnUpdateAction)}');");
            sb.AppendLine();
        }
    }

    private static void AppendProcedureCreateOrAlter(StringBuilder sb, PatchProcedureSnapshot procedure, string indent)
    {
        var definition = NormalizeProcedureDefinition(procedure.Definition);
        var escapedDefinition = definition.Replace("'", "''");
        sb.AppendLine($"{indent}PRINT 'Applying {QualifiedName(procedure.SchemaName, procedure.ProcedureName)}';");
        sb.AppendLine($"{indent}EXEC(N'{escapedDefinition}');");
        sb.AppendLine();
    }

    private static string BuildColumnMismatchQuery(PatchTableSnapshot table, PatchColumnSnapshot column)
    {
        var conditions = new List<string>
        {
            $"UPPER(t.name) <> '{EscapeSql(column.DataType.ToUpperInvariant())}'",
            $"c.is_nullable <> {(column.IsNullable ? 1 : 0)}",
            $"c.is_identity <> {(column.IsIdentity ? 1 : 0)}"
        };

        if (column.MaxLength.HasValue)
        {
            var isUnicode = column.DataType.Equals("NVARCHAR", StringComparison.OrdinalIgnoreCase) ||
                            column.DataType.Equals("NCHAR", StringComparison.OrdinalIgnoreCase) ||
                            column.DataType.Equals("SYSNAME", StringComparison.OrdinalIgnoreCase);

            var expectedLength = column.MaxLength.Value;
            conditions.Add($"c.max_length <> {expectedLength}");
        }

        if (column.Precision.HasValue)
        {
            conditions.Add($"c.precision <> {column.Precision.Value}");
        }

        if (column.Scale.HasValue)
        {
            conditions.Add($"c.scale <> {column.Scale.Value}");
        }

        if (column.IsPrimaryKey)
        {
            conditions.Add("NOT EXISTS (SELECT 1 FROM sys.indexes i INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id WHERE i.object_id = c.object_id AND i.is_primary_key = 1 AND ic.column_id = c.column_id)");
        }

        return $@"SELECT 1
            FROM sys.columns c
            INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
            WHERE c.object_id = OBJECT_ID(N'{EscapeSql(QualifiedName(table.SchemaName, table.TableName))}')
              AND c.name = N'{EscapeSql(column.ColumnName)}'
              AND c.is_computed = 0
              AND ({string.Join(" OR ", conditions)})";
    }

    private static string BuildColumnDefinition(PatchColumnSnapshot column)
    {
        var sb = new StringBuilder();
        sb.Append(Bracket(column.ColumnName)).Append(' ').Append(BuildDataTypeDefinition(column));

        if (column.IsIdentity)
        {
            sb.Append(" IDENTITY(1,1)");
        }

        sb.Append(column.IsNullable ? " NULL" : " NOT NULL");

        if (column.IsPrimaryKey)
        {
            sb.Append(" PRIMARY KEY");
        }

        if (!string.IsNullOrWhiteSpace(column.DefaultValue))
        {
            sb.Append(" DEFAULT ").Append(column.DefaultValue);
        }

        return sb.ToString();
    }

    private static string BuildDataTypeDefinition(PatchColumnSnapshot column)
    {
        var dataType = column.DataType.ToUpperInvariant();
        return dataType switch
        {
            "NVARCHAR" or "VARCHAR" or "CHAR" or "NCHAR" => $"{dataType}({FormatLength(column.MaxLength)})",
            "DECIMAL" or "NUMERIC" => $"{dataType}({column.Precision ?? 18},{column.Scale ?? 0})",
            "VARBINARY" or "BINARY" => $"{dataType}({FormatLength(column.MaxLength)})",
            _ => dataType
        };
    }

    private static string FormatLength(int? length)
    {
        if (!length.HasValue || length.Value < 0)
        {
            return "MAX";
        }

        return length.Value.ToString();
    }

    private static string DefaultConstraintName(PatchTableSnapshot table, PatchColumnSnapshot column)
    {
        return $"DF_{table.TableName}_{column.ColumnName}";
    }

    private static string NormalizeProcedureDefinition(string definition)
    {
        var normalized = CreateOrAlterProcedureRegex().Replace(definition, "CREATE OR ALTER PROCEDURE");
        return normalized.Trim();
    }

    private static string QualifiedName(string schemaName, string objectName)
    {
        return $"{Bracket(schemaName)}.{Bracket(objectName)}";
    }

    private static string Bracket(string name)
    {
        return $"[{name.Replace("]", "]]")}]";
    }

    private static string SqlUnicodeLiteral(string value)
    {
        return $"N'{EscapeSql(value)}'";
    }

    private static string EscapeSql(string value)
    {
        return value.Replace("'", "''");
    }

    private static string EscapeForNestedExec(string value)
    {
        return value.Replace("'", "''''");
    }

    [GeneratedRegex(@"^\s*(CREATE|ALTER)\s+PROCEDURE", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CreateOrAlterProcedureRegex();
}
