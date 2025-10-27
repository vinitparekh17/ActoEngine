using ActoEngine.WebApi.Models;
using System.Text;

namespace ActoEngine.WebApi.Services.CodeGen;

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
        var spName = $"{opts.SpPrefix}_{tableName}_Select";
        var pkCols = cols.Where(c => c.IsPrimaryKey).ToList();
        var orderByCols = opts.OrderByColumns.Any() 
            ? opts.OrderByColumns 
            : pkCols.Select(c => c.ColumnName).ToList();

        if (!orderByCols.Any())
            orderByCols.Add(cols.First().ColumnName); // Fallback

        var template = SpTemplateStore.SelectTemplate;
        
        template = template.Replace("{SP_NAME}", spName);
        template = template.Replace("{TABLE_NAME}", tableName);
        template = template.Replace("{SELECT_COLUMNS}", BuildSelectColumns(cols));
        template = template.Replace("{ORDER_BY_CLAUSE}", BuildOrderByClause(orderByCols));
        
        // Filters
        if (opts.Filters.Any())
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
            template = template.Replace("{PAGINATION_PARAMS}", BuildPaginationParams(opts.Filters.Any()));
            template = template.Replace("{PAGINATION_LOGIC}", BuildPaginationLogic());
            template = template.Replace("{PAGINATION_FETCH}", BuildPaginationFetch());
            template = template.Replace("{TOTAL_COUNT}", BuildTotalCount(tableName, opts.Filters));
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
    private string BuildParameters(List<SpColumnConfig> cols)
    {
        if (!cols.Any()) return "";
        
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

    private string BuildFilterParameters(List<FilterColumn> filters, List<SpColumnConfig> allCols)
    {
        if (!filters.Any()) return "";

        var sb = new StringBuilder();
        foreach (var filter in filters)
        {
            var col = allCols.FirstOrDefault(c => c.ColumnName == filter.ColumnName);
            if (col == null) continue;

            sb.Append($"    @{filter.ColumnName} {GetSqlType(col)}");
            if (filter.IsOptional) sb.Append(" = NULL");
            sb.AppendLine(",");
        }
        return sb.ToString().TrimEnd(',', '\n', '\r');
    }

    private static string BuildWhereFilters(List<FilterColumn> filters)
    {
        if (!filters.Any()) return "";

        var sb = new StringBuilder();
        sb.AppendLine("    WHERE 1=1");
        
        foreach (var filter in filters)
        {
            if (filter.IsOptional)
            {
                sb.Append($"        AND (@{filter.ColumnName} IS NULL OR ");
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
                    sb.Append($"[{filter.ColumnName}] LIKE '%' + @{filter.ColumnName} + '%'");
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

    private string BuildTotalCount(string tableName, List<FilterColumn> filters)
    {
        var whereClause = filters.Any() ? BuildWhereFilters(filters) : "";
        return $"\n    \n    -- Total count\n    SELECT COUNT(*) AS TotalRecords\n    FROM [dbo].[{tableName}]\n{whereClause};";
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
            var len = col.MaxLength.HasValue ? col.MaxLength.Value.ToString() : "MAX";
            return $"{col.DataType}({len})";
        }
        
        if (dt == "DECIMAL" || dt == "NUMERIC")
            return $"{col.DataType}(18,2)";
        
        return col.DataType;
    }
}