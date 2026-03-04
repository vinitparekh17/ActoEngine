using System.Text;

namespace ActoEngine.WebApi.Features.SpBuilder;

public class SpCudRenderer
{
    public string RenderCud(string tableName, List<SpColumnConfig> cols, CudSpOptions opts)
    {
        var spName = $"{opts.SpPrefix}_{tableName}_CUD";
        var pkCols = cols.Where(c => c.IsPrimaryKey).ToList();
        var createCols = opts.GenerateCreate ? cols.Where(c => c.IncludeInCreate && !c.IsIdentity).ToList() : [];
        var updateCols = opts.GenerateUpdate ? cols.Where(c => c.IncludeInUpdate && !c.IsIdentity && !c.IsPrimaryKey).ToList() : [];
        var identityCol = cols.FirstOrDefault(c => c.IsIdentity);

        // Build action comment from enabled operations
        var activeActions = new List<string>();
        if (opts.GenerateCreate) activeActions.Add("'C' = Create");
        if (opts.GenerateUpdate) activeActions.Add("'U' = Update");
        if (opts.GenerateDelete) activeActions.Add("'D' = Delete");
        var actionComment = string.Join(", ", activeActions);

        // All params needed: PK + create columns + update columns (union to avoid duplicates)
        var allParams = pkCols
            .Union(createCols.Where(c => !c.IsPrimaryKey))
            .Union(updateCols)
            .Distinct()
            .ToList();

        // Build the conditional body sections
        var bodySections = new List<string>();

        if (opts.GenerateCreate)
        {
            var returnIdentity = identityCol != null
                ? $"\n        SELECT SCOPE_IDENTITY() AS [{identityCol.ColumnName}];"
                : "";
            bodySections.Add(
                $"    -- CREATE\n" +
                $"    IF @{opts.ActionParamName} = 'C'\n" +
                $"    BEGIN\n" +
                $"        INSERT INTO {tableName} (\n" +
                $"{BuildInsertColumns(createCols)}\n" +
                $"        )\n" +
                $"        VALUES (\n" +
                $"{BuildInsertValues(createCols)}\n" +
                $"        );{returnIdentity}\n" +
                $"    END");
        }

        if (opts.GenerateUpdate)
        {
            var keyword = bodySections.Count > 0 ? "    ELSE IF" : "    IF";
            bodySections.Add(
                $"    -- UPDATE\n" +
                $"{keyword} @{opts.ActionParamName} = 'U'\n" +
                $"    BEGIN\n" +
                $"        UPDATE {tableName}\n" +
                $"        SET\n" +
                $"{BuildUpdateSetClause(updateCols)}\n" +
                $"        WHERE\n" +
                $"{BuildWhereClause(pkCols)};\n" +
                $"    END");
        }

        if (opts.GenerateDelete)
        {
            var keyword = bodySections.Count > 0 ? "    ELSE IF" : "    IF";
            bodySections.Add(
                $"    -- DELETE\n" +
                $"{keyword} @{opts.ActionParamName} = 'D'\n" +
                $"    BEGIN\n" +
                $"        DELETE FROM {tableName}\n" +
                $"        WHERE\n" +
                $"{BuildWhereClause(pkCols)};\n" +
                $"    END");
        }

        var body = string.Join("\n    \n", bodySections);

        // Build the error-handling wrapper around the body
        var (ehStart, ehEnd) = BuildErrorHandling(opts);

        var sb = new StringBuilder();
        sb.AppendLine($"CREATE PROCEDURE {spName}");
        sb.AppendLine($"    @{opts.ActionParamName} CHAR(1), -- {actionComment}");
        sb.AppendLine(BuildParameters(allParams));
        sb.AppendLine("AS");
        sb.AppendLine("BEGIN");
        sb.Append(ehStart);
        sb.AppendLine(body);
        sb.Append(ehEnd);
        sb.AppendLine("END");

        return sb.ToString();
    }

    private static (string Start, string End) BuildErrorHandling(CudSpOptions opts)
    {
        if (!opts.IncludeErrorHandling)
            return ("    SET NOCOUNT ON;\n\n", "");

        var start = opts.IncludeTransaction
            ? SpTemplateStore.ErrorHandlingStart
            : SpTemplateStore.ErrorHandlingStartNoTrans;

        var end = opts.IncludeTransaction
            ? SpTemplateStore.ErrorHandlingEnd
            : SpTemplateStore.ErrorHandlingEndNoTrans;

        return (start, end + "\n");
    }

    private static string BuildParameters(List<SpColumnConfig> cols)
    {
        if (cols.Count == 0)
        {
            return "";
        }

        var sb = new StringBuilder();
        for (int i = 0; i < cols.Count; i++)
        {
            var col = cols[i];
            sb.Append($"    @{col.ColumnName} {SpTemplateRendererUtilities.GetSqlType(col)}");
            if (col.IsNullable)
            {
                sb.Append(" = NULL");
            }

            sb.Append(',');
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd(',', '\n', '\r');
    }

    private static string BuildInsertColumns(List<SpColumnConfig> cols)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < cols.Count; i++)
        {
            sb.Append($"            [{cols[i].ColumnName}]");
            if (i < cols.Count - 1)
            {
                sb.Append(',');
            }

            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildInsertValues(List<SpColumnConfig> cols)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < cols.Count; i++)
        {
            sb.Append($"            @{cols[i].ColumnName}");
            if (i < cols.Count - 1)
            {
                sb.Append(',');
            }

            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildUpdateSetClause(List<SpColumnConfig> cols)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < cols.Count; i++)
        {
            var col = cols[i];
            sb.Append($"            [{col.ColumnName}] = @{col.ColumnName}");
            if (i < cols.Count - 1)
            {
                sb.Append(',');
            }

            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildWhereClause(List<SpColumnConfig> cols)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < cols.Count; i++)
        {
            var col = cols[i];
            sb.Append($"            [{col.ColumnName}] = @{col.ColumnName}");
            if (i < cols.Count - 1)
            {
                sb.Append(" AND");
            }

            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }
}

