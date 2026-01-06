using ActoEngine.WebApi.Models;
using System.Text;

namespace ActoEngine.WebApi.Services.SpBuilderService;

public class SpCudRenderer
{
    public string RenderCud(string tableName, List<SpColumnConfig> cols, CudSpOptions opts)
    {
        var spName = $"{opts.SpPrefix}_{tableName}_CUD";
        var pkCols = cols.Where(c => c.IsPrimaryKey).ToList();
        var createCols = cols.Where(c => c.IncludeInCreate && !c.IsIdentity).ToList();
        var updateCols = cols.Where(c => c.IncludeInUpdate && !c.IsIdentity && !c.IsPrimaryKey).ToList();
        var identityCol = cols.FirstOrDefault(c => c.IsIdentity);

        // All params needed: PK + create + update (union to avoid duplicates)
        var allParams = pkCols
            .Union(createCols.Where(c => !c.IsPrimaryKey))
            .Union(updateCols)
            .Distinct()
            .ToList();

        var template = SpTemplateStore.CudTemplate;

        template = template.Replace("{SP_NAME}", spName);
        template = template.Replace("{ACTION_PARAM}", opts.ActionParamName);
        template = template.Replace("{TABLE_NAME}", tableName);
        template = template.Replace("{PARAMETERS}", BuildParameters(allParams));
        template = template.Replace("{INSERT_COLUMNS}", BuildInsertColumns(createCols));
        template = template.Replace("{INSERT_VALUES}", BuildInsertValues(createCols));
        template = template.Replace("{RETURN_IDENTITY}", identityCol != null
            ? $"        SELECT SCOPE_IDENTITY() AS [{identityCol.ColumnName}];"
            : "");
        template = template.Replace("{UPDATE_SET_CLAUSE}", BuildUpdateSetClause(updateCols));
        template = template.Replace("{WHERE_CLAUSE}", BuildWhereClause(pkCols));
        template = ReplaceErrorHandling(template, opts);

        return template;
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

    private static string ReplaceErrorHandling(string template, CudSpOptions opts)
    {
        if (!opts.IncludeErrorHandling)
        {
            return template
                .Replace("{ERROR_HANDLING_START}", "    SET NOCOUNT ON;\n")
                .Replace("{ERROR_HANDLING_END}", "");
        }

        var start = opts.IncludeTransaction
            ? SpTemplateStore.ErrorHandlingStart
            : SpTemplateStore.ErrorHandlingStartNoTrans;

        var end = opts.IncludeTransaction
            ? SpTemplateStore.ErrorHandlingEnd
            : SpTemplateStore.ErrorHandlingEndNoTrans;

        return template
            .Replace("{ERROR_HANDLING_START}", start)
            .Replace("{ERROR_HANDLING_END}", end);
    }
}

