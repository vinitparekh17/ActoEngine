using System.Text;

namespace ActoEngine.WebApi.Features.SpBuilder;

public class SpTemplateRenderer
{
    public string RenderCud(string schemaName, string tableName, List<SpColumnConfig> cols, CudSpOptions opts)
    {
        var escapedPrefix = opts.SpPrefix?.Replace("]", "]]") ?? "usp";
        var escapedTableName = tableName.Replace("]", "]]");
        var escapedSchema = schemaName.Replace("]", "]]");
        var spName = $"[{escapedSchema}].[{escapedPrefix}_{escapedTableName}_CUD]";
        var pkCols = cols.Where(c => c.IsPrimaryKey).ToList();
        var createCols = opts.GenerateCreate ? cols.Where(c => c.IncludeInCreate && !c.IsIdentity).ToList() : [];
        var updateCols = opts.GenerateUpdate ? cols.Where(c => c.IncludeInUpdate && !c.IsIdentity && !c.IsPrimaryKey).ToList() : [];
        var identityCol = cols.FirstOrDefault(c => c.IsIdentity);

        // Build action comment from enabled operations
        var activeActions = new List<string>();
        if (opts.GenerateCreate) activeActions.Add("'C' = Create");
        if (opts.GenerateUpdate) activeActions.Add("'U' = Update");
        if (opts.GenerateDelete) activeActions.Add("'D' = Delete");
        if (activeActions.Count == 0)
        {
            throw new ArgumentException("No CUD operations enabled; at least one of GenerateCreate/GenerateUpdate/GenerateDelete must be true");
        }
        var actionComment = string.Join(", ", activeActions);

        // All params needed: PK + create columns + update columns (union to avoid duplicates)
        var allParams = pkCols
            .Union(createCols.Where(c => !c.IsPrimaryKey))
            .Union(updateCols)
            .Distinct()
            .ToList();

        // Build the conditional body sections
        var bodySections = new List<string>();
        var fullTableName = tableName.Contains(".") || tableName.StartsWith("[dbo]") || tableName.StartsWith($"[{schemaName}]") 
            ? tableName 
            : $"{schemaName}.{tableName}";
        var escapedQualifiedTableName = BracketQualifiedName(fullTableName);

        if (opts.GenerateCreate && createCols.Count > 0)
        {
            var returnIdentity = identityCol != null
                ? $"\n        SELECT SCOPE_IDENTITY() AS [{identityCol.ColumnName}];"
                : "";
            bodySections.Add(
                $"    -- CREATE\n" +
                $"    IF @{opts.ActionParamName} = 'C'\n" +
                $"    BEGIN\n" +
                $"        INSERT INTO {escapedQualifiedTableName} (\n" +
                $"{BuildInsertColumns(createCols)}\n" +
                $"        )\n" +
                $"        VALUES (\n" +
                $"{BuildInsertValues(createCols)}\n" +
                $"        );{returnIdentity}\n" +
                $"    END");
        }

        if (opts.GenerateUpdate && updateCols.Count > 0 && pkCols.Count > 0)
        {
            var keyword = bodySections.Count > 0 ? "    ELSE IF" : "    IF";
            bodySections.Add(
                $"    -- UPDATE\n" +
                $"{keyword} @{opts.ActionParamName} = 'U'\n" +
                $"    BEGIN\n" +
                $"        UPDATE {escapedQualifiedTableName}\n" +
                $"        SET\n" +
                $"{BuildUpdateSetClause(updateCols)}\n" +
                $"        WHERE\n" +
                $"{BuildWhereClause(pkCols)};\n" +
                $"    END");
        }

        if (opts.GenerateDelete && pkCols.Count > 0)
        {
            var keyword = bodySections.Count > 0 ? "    ELSE IF" : "    IF";
            bodySections.Add(
                $"    -- DELETE\n" +
                $"{keyword} @{opts.ActionParamName} = 'D'\n" +
                $"    BEGIN\n" +
                $"        DELETE FROM {escapedQualifiedTableName}\n" +
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

    public string RenderSelect(string schemaName, string tableName, List<SpColumnConfig> cols, SelectSpOptions opts)
    {
        if (cols == null || cols.Count == 0)
        {
            throw new ArgumentException("Columns collection cannot be empty when rendering SELECT stored procedure.", nameof(cols));
        }

        var escapedPrefix = opts.SpPrefix?.Replace("]", "]]") ?? "usp";
        var escapedTableName = tableName.Replace("]", "]]");
        var escapedSchema = schemaName.Replace("]", "]]");
        var spName = $"[{escapedSchema}].[{escapedPrefix}_{escapedTableName}_Select]";
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
        var fullTableName = tableName.Contains(".") || tableName.StartsWith("[dbo]") || tableName.StartsWith($"[{schemaName}]") 
            ? tableName 
            : $"{schemaName}.{tableName}";
        string mainFromIdentifier = BracketQualifiedName(fullTableName);

        template = template.Replace("{SP_NAME}", spName);
        template = template.Replace("{TABLE_NAME}", mainFromIdentifier);
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
        if (cols.Count == 0)
        {
            return "";
        }

        var sb = new StringBuilder();
        for (int i = 0; i < cols.Count; i++)
        {
            var col = cols[i];
            sb.Append($"    @{col.ColumnName} {GetSqlType(col)}");
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
                sb.Append($"    @{filter.ColumnName}Start {GetSqlType(col)}");
                if (filter.IsOptional)
                {
                    sb.Append(" = NULL");
                }

                sb.AppendLine(",");
                sb.Append($"    @{filter.ColumnName}End {GetSqlType(col)}");
                if (filter.IsOptional)
                {
                    sb.Append(" = NULL");
                }

                sb.AppendLine(",");
            }
            else
            {
                sb.Append($"    @{filter.ColumnName} {GetSqlType(col)}");
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

    /// <summary>
    /// Brackets and escapes a possibly schema-qualified identifier like "schema.Table" or "Table".
    /// If the parts are already bracketed, they will be normalized.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when name is null, empty, or whitespace.</exception>
    private static string BracketQualifiedName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Identifier name cannot be null, empty, or whitespace.", nameof(name));
        }

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
    /// <exception cref="ArgumentException">Thrown when identifier is null, empty, or whitespace.</exception>
    private static string BracketIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Identifier cannot be null, empty, or whitespace.", nameof(identifier));
        }

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