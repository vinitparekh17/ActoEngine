using ActoEngine.WebApi.Models;
using System.Text;

namespace ActoEngine.WebApi.Services.SpBuilder;

public class SpTemplateRenderer
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

    public string RenderSelect(string tableName, List<SpColumnConfig> cols, SelectSpOptions opts)
    {
        if (cols == null || cols.Count == 0)
            throw new ArgumentException("Columns collection cannot be empty when rendering SELECT stored procedure.", nameof(cols));

        var spName = $"{opts.SpPrefix}_{tableName}_Select";
        var pkCols = cols.Where(c => c.IsPrimaryKey).ToList();
        var orderByCols = opts.OrderByColumns.Count != 0
            ? opts.OrderByColumns 
            : [.. pkCols.Select(c => c.ColumnName)];

        if (orderByCols.Count == 0)
        {
            var firstCol = cols.FirstOrDefault() ?? throw new InvalidOperationException("Cannot determine ORDER BY columns because no columns were provided.");
            orderByCols.Add(firstCol.ColumnName); // Fallback
        }

        var template = SpTemplateStore.SelectTemplate;
        // Determine the exact table identifier used by the main SELECT's FROM clause,
        // so the TOTAL COUNT query uses the same source. If the provided tableName is
        // already schema-qualified (schema.Table), use that; otherwise default to dbo.
        string mainFromIdentifier = tableName.Contains('.')
            ? BracketQualifiedName(tableName)
            : $"{BracketIdentifier("dbo")}.{BracketIdentifier(tableName)}";
        
        template = template.Replace("{SP_NAME}", spName);
        template = template.Replace("{TABLE_NAME}", tableName);
        template = template.Replace("{SELECT_COLUMNS}", BuildSelectColumns(cols));
        template = template.Replace("{ORDER_BY_CLAUSE}", BuildOrderByClause(orderByCols));
        
        // Filters
        if (opts.Filters.Count != 0)
        {
            template = template.Replace("{FILTER_PARAMETERS}", BuildFilterParameters(opts.Filters, cols));
            template = template.Replace("{WHERE_FILTERS}", BuildWhereFilters(opts.Filters));
        }
        else
        {
            template = template.Replace("{FILTER_PARAMETERS}", "");
            template = template.Replace("{WHERE_FILTERS}", "");
        }
        
        // Pagination
        if (opts.IncludePagination)
        {
            template = template.Replace("{PAGINATION_PARAMS}", BuildPaginationParams(opts.Filters.Count != 0));
            template = template.Replace("{PAGINATION_LOGIC}", BuildPaginationLogic());
            template = template.Replace("{PAGINATION_FETCH}", BuildPaginationFetch());
            // Use the same qualified identifier for count that the main SELECT intends to use
            template = template.Replace("{TOTAL_COUNT}", BuildTotalCount(mainFromIdentifier, opts.Filters));
        }
        else
        {
            template = template.Replace("{PAGINATION_PARAMS}", "");
            template = template.Replace("{PAGINATION_LOGIC}", "");
            template = template.Replace("{PAGINATION_FETCH}", "");
            template = template.Replace("{TOTAL_COUNT}", "");
        }

        return template;
    }

    // Helper methods
    private static string BuildParameters(List<SpColumnConfig> cols)
    {
        if (cols.Count == 0) return "";
        
        var sb = new StringBuilder();
        for (int i = 0; i < cols.Count; i++)
        {
            var col = cols[i];
            sb.Append($"    @{col.ColumnName} {GetSqlType(col)}");
            if (col.IsNullable) sb.Append(" = NULL");
            sb.Append(",");
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
            if (i < cols.Count - 1) sb.Append(",");
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
            if (i < cols.Count - 1) sb.Append(",");
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
            if (i < cols.Count - 1) sb.Append(",");
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
            if (i < cols.Count - 1) sb.Append(" AND");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildSelectColumns(List<SpColumnConfig> cols)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < cols.Count; i++)
        {
            sb.Append($"        [{cols[i].ColumnName}]");
            if (i < cols.Count - 1) sb.Append(",");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildOrderByClause(List<string> cols)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < cols.Count; i++)
        {
            sb.Append($"        [{cols[i]}]");
            if (i < cols.Count - 1) sb.Append(",");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildFilterParameters(List<FilterColumn> filters, List<SpColumnConfig> allCols)
    {
        if (filters.Count == 0) return "";

        var sb = new StringBuilder();
        foreach (var filter in filters)
        {
            var col = allCols.FirstOrDefault(c => c.ColumnName == filter.ColumnName);
            if (col == null) continue;

            if (filter.Operator == FilterOperator.Between)
            {
                // BETWEEN requires two parameters: Start and End
                sb.Append($"    @{filter.ColumnName}Start {GetSqlType(col)}");
                if (filter.IsOptional) sb.Append(" = NULL");
                sb.AppendLine(",");
                sb.Append($"    @{filter.ColumnName}End {GetSqlType(col)}");
                if (filter.IsOptional) sb.Append(" = NULL");
                sb.AppendLine(",");
            }
            else
            {
                sb.Append($"    @{filter.ColumnName} {GetSqlType(col)}");
                if (filter.IsOptional) sb.Append(" = NULL");
                sb.AppendLine(",");
            }
        }
        return sb.ToString().TrimEnd(',', '\n', '\r');
    }

    private static string BuildWhereFilters(List<FilterColumn> filters)
    {
        if (filters.Count == 0) return "";

        var sb = new StringBuilder();
        sb.AppendLine("    WHERE 1=1");
        
        foreach (var filter in filters)
        {
            if (filter.IsOptional)
            {
                if (filter.Operator == FilterOperator.Between)
                {
                    sb.Append($"        AND (@{filter.ColumnName}Start IS NULL OR @{filter.ColumnName}End IS NULL OR ");
                }
                else
                {
                    sb.Append($"        AND (@{filter.ColumnName} IS NULL OR ");
                }
            }
            else
            {
                sb.Append($"        AND ");
            }

            switch (filter.Operator)
            {
                case FilterOperator.Equals:
                    sb.Append($"[{filter.ColumnName}] = @{filter.ColumnName}");
                    break;
                case FilterOperator.Like:
                    sb.Append($"[{filter.ColumnName}] LIKE CONCAT('%', COALESCE(@{filter.ColumnName}, ''), '%')");
                    break;
                case FilterOperator.GreaterThan:
                    sb.Append($"[{filter.ColumnName}] > @{filter.ColumnName}");
                    break;
                case FilterOperator.LessThan:
                    sb.Append($"[{filter.ColumnName}] < @{filter.ColumnName}");
                    break;
                case FilterOperator.Between:
                    sb.Append($"[{filter.ColumnName}] BETWEEN @{filter.ColumnName}Start AND @{filter.ColumnName}End");
                    break;
            }

            if (filter.IsOptional) sb.Append(")");
            sb.AppendLine();
        }
        
        return sb.ToString().TrimEnd();
    }

    private static string BuildPaginationParams(bool hasFilters)
    {
        var comma = hasFilters ? "," : "";
        return $"{comma}\n    @PageNumber INT = 1,\n    @PageSize INT = 50";
    }

    private static string BuildPaginationLogic()
    {
        return "    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;\n";
    }

    private static string BuildPaginationFetch()
    {
        return "\n    OFFSET @Offset ROWS\n    FETCH NEXT @PageSize ROWS ONLY";
    }

    private static string BuildTotalCount(string schemaQualifiedTableName, List<FilterColumn> filters)
    {
        var whereClause = filters.Count != 0 ? BuildWhereFilters(filters) : "";
        var fromIdentifier = schemaQualifiedTableName; // expected to be bracketed/escaped already
        return $"\n    \n    -- Total count\n    SELECT COUNT(*) AS TotalRecords\n    FROM {fromIdentifier}\n{whereClause};";
    }

    /// <summary>
    /// Brackets and escapes a possibly schema-qualified identifier like "schema.Table" or "Table".
    /// If the parts are already bracketed, they will be normalized.
    /// </summary>
    private static string BracketQualifiedName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;

        // Split on '.' to handle schema-qualified names; simple approach assuming 2 parts max.
        var parts = name.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            return BracketIdentifier(parts[0]);
        }
        else if (parts.Length >= 2)
        {
            // Only use the first two parts (schema.table); if more, join remaining as table segment
            var schema = parts[0];
            var table = string.Join('.', parts.Skip(1));
            return $"{BracketIdentifier(schema)}.{BracketIdentifier(table)}";
        }

        return BracketIdentifier(name);
    }

    /// <summary>
    /// Wraps the identifier in brackets and escapes any closing bracket characters.
    /// Accepts identifiers that may already be bracketed and normalizes them.
    /// </summary>
    private static string BracketIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return identifier;

        var trimmed = identifier.Trim();
        // Remove outer brackets if present
        if (trimmed.StartsWith("[") && trimmed.EndsWith("]") && trimmed.Length >= 2)
        {
            trimmed = trimmed.Substring(1, trimmed.Length - 2);
        }

        // Escape any closing brackets inside the name
        trimmed = trimmed.Replace("]", "]]");

        return $"[{trimmed}]";
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

    private static string GetSqlType(SpColumnConfig col)
    {
        var dt = col.DataType.ToUpper();
        
        if (dt == "VARCHAR" || dt == "NVARCHAR" || dt == "CHAR" || dt == "NCHAR")
        {
            // SQL Server uses -1 to represent MAX for (VAR)CHAR/N(VARCHAR) types; also treat null as MAX
            var len = (!col.MaxLength.HasValue || col.MaxLength.Value == -1)
                ? "MAX"
                : col.MaxLength.Value.ToString();
            return $"{col.DataType}({len})";
        }
        
        if (dt == "DECIMAL" || dt == "NUMERIC")
        {
            var precision = col.Precision ?? 18; // Default precision
            var scale = col.Scale ?? 2; // Default scale
            return $"{col.DataType}({precision},{scale})";
        }
        
        return col.DataType;
    }
}