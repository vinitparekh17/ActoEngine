using System.Text;

namespace ActoEngine.WebApi.Features.SpBuilder;

public class SpSelectRenderer
{
    public string RenderSelect(string tableName, List<SpColumnConfig> cols, SelectSpOptions opts)
    {
        if (cols == null || cols.Count == 0)
        {
            throw new ArgumentException("Columns collection cannot be empty when rendering SELECT stored procedure.", nameof(cols));
        }

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
            ? SpTemplateRendererUtilities.BracketQualifiedName(tableName)
            : $"{SpTemplateRendererUtilities.BracketIdentifier("dbo")}.{SpTemplateRendererUtilities.BracketIdentifier(tableName)}";

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

    private static string BuildSelectColumns(List<SpColumnConfig> cols)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < cols.Count; i++)
        {
            sb.Append($"        [{cols[i].ColumnName}]");
            if (i < cols.Count - 1)
            {
                sb.Append(',');
            }

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
            if (i < cols.Count - 1)
            {
                sb.Append(',');
            }

            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildFilterParameters(List<FilterColumn> filters, List<SpColumnConfig> allCols)
    {
        if (filters.Count == 0)
        {
            return "";
        }

        var sb = new StringBuilder();
        foreach (var filter in filters)
        {
            var col = allCols.FirstOrDefault(c => c.ColumnName == filter.ColumnName);
            if (col == null)
            {
                continue;
            }

            if (filter.Operator == FilterOperator.Between)
            {
                // BETWEEN requires two parameters: Start and End
                sb.Append($"    @{filter.ColumnName}Start {SpTemplateRendererUtilities.GetSqlType(col)}");
                if (filter.IsOptional)
                {
                    sb.Append(" = NULL");
                }

                sb.AppendLine(",");
                sb.Append($"    @{filter.ColumnName}End {SpTemplateRendererUtilities.GetSqlType(col)}");
                if (filter.IsOptional)
                {
                    sb.Append(" = NULL");
                }

                sb.AppendLine(",");
            }
            else
            {
                sb.Append($"    @{filter.ColumnName} {SpTemplateRendererUtilities.GetSqlType(col)}");
                if (filter.IsOptional)
                {
                    sb.Append(" = NULL");
                }

                sb.AppendLine(",");
            }
        }
        return sb.ToString().TrimEnd(',', '\n', '\r');
    }

    private static string BuildWhereFilters(List<FilterColumn> filters)
    {
        if (filters.Count == 0)
        {
            return "";
        }

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

            if (filter.IsOptional)
            {
                sb.Append(')');
            }

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
}

