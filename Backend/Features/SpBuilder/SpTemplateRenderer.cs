using System.Text;

namespace ActoEngine.WebApi.Features.SpBuilder;

public class SpTemplateRenderer
{
    public string RenderCud(string schemaName, string tableName, List<SpColumnConfig> cols, CudSpOptions opts)
    {
        var validatedActionParamName = SpTemplateRendererUtilities.ValidateSqlIdentifier(
            opts.ActionParamName,
            nameof(opts.ActionParamName));
        var validatedPrefix = SpTemplateRendererUtilities.ValidateSqlIdentifier(
            opts.SpPrefix ?? "usp",
            nameof(opts.SpPrefix));
        var validatedSchema = SpTemplateRendererUtilities.ValidateSqlIdentifier(schemaName, nameof(schemaName));
        var validatedTableName = SpTemplateRendererUtilities.ValidateSqlIdentifier(tableName, nameof(tableName));
        var escapedPrefix = validatedPrefix.Replace("]", "]]");
        var escapedTableName = validatedTableName.Replace("]", "]]");
        var escapedSchema = validatedSchema.Replace("]", "]]");
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
        var escapedQualifiedTableName = SpTemplateRendererUtilities.BracketQualifiedName($"{validatedSchema}.{validatedTableName}");

        if (opts.GenerateCreate && createCols.Count > 0)
        {
            var returnIdentity = identityCol != null
                ? $"\n        SELECT SCOPE_IDENTITY() AS {SpTemplateRendererUtilities.BracketIdentifier(identityCol.ColumnName)};"
                : "";
            bodySections.Add(
                $"    -- CREATE\n" +
                $"    IF @{validatedActionParamName} = 'C'\n" +
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
                $"{keyword} @{validatedActionParamName} = 'U'\n" +
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
                $"{keyword} @{validatedActionParamName} = 'D'\n" +
                $"    BEGIN\n" +
                $"        DELETE FROM {escapedQualifiedTableName}\n" +
                $"        WHERE\n" +
                $"{BuildWhereClause(pkCols)};\n" +
                $"    END");
        }

        if (bodySections.Count == 0)
        {
            var missingPrereqs = new List<string>();
            if (opts.GenerateCreate && createCols.Count == 0) missingPrereqs.Add("Create requested but no creatable columns are available.");
            if (opts.GenerateUpdate && pkCols.Count == 0) missingPrereqs.Add("Update requested but no primary key columns are available.");
            if (opts.GenerateUpdate && updateCols.Count == 0) missingPrereqs.Add("Update requested but no updatable columns are available.");
            if (opts.GenerateDelete && pkCols.Count == 0) missingPrereqs.Add("Delete requested but no primary key columns are available.");

            var requestedFlags = $"Requested flags: Create={opts.GenerateCreate}, Update={opts.GenerateUpdate}, Delete={opts.GenerateDelete}.";
            var reasons = missingPrereqs.Count == 0 ? "No executable C/U/D branches can be produced." : string.Join(" ", missingPrereqs);
            throw new InvalidOperationException($"{requestedFlags} {reasons}");
        }

        var body = string.Join("\n    \n", bodySections);

        // Build the error-handling wrapper around the body
        var (ehStart, ehEnd) = BuildErrorHandling(opts);

        var sb = new StringBuilder();
        sb.AppendLine($"CREATE PROCEDURE {spName}");
        sb.AppendLine($"    @{validatedActionParamName} CHAR(1), -- {actionComment}");
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

        var validatedPrefix = SpTemplateRendererUtilities.ValidateSqlIdentifier(opts.SpPrefix ?? "usp", nameof(opts.SpPrefix));
        var validatedTableName = SpTemplateRendererUtilities.ValidateSqlIdentifier(tableName, nameof(tableName));
        var validatedSchema = SpTemplateRendererUtilities.ValidateSqlIdentifier(schemaName, nameof(schemaName));
        var escapedPrefix = validatedPrefix.Replace("]", "]]");
        var escapedTableName = validatedTableName.Replace("]", "]]");
        var escapedSchema = validatedSchema.Replace("]", "]]");
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
        string mainFromIdentifier = SpTemplateRendererUtilities.BracketQualifiedName($"{validatedSchema}.{validatedTableName}");

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
            var paramName = SpTemplateRendererUtilities.ValidateSqlIdentifier(col.ColumnName, nameof(col.ColumnName));
            sb.Append($"    @{paramName} {SpTemplateRendererUtilities.GetSqlType(col)}");
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
            sb.Append($"            {SpTemplateRendererUtilities.BracketIdentifier(cols[i].ColumnName)}");
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
            var paramName = SpTemplateRendererUtilities.ValidateSqlIdentifier(cols[i].ColumnName, nameof(SpColumnConfig.ColumnName));
            sb.Append($"            @{paramName}");
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
            var paramName = SpTemplateRendererUtilities.ValidateSqlIdentifier(col.ColumnName, nameof(col.ColumnName));
            sb.Append($"            {SpTemplateRendererUtilities.BracketIdentifier(col.ColumnName)} = @{paramName}");
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
            var paramName = SpTemplateRendererUtilities.ValidateSqlIdentifier(col.ColumnName, nameof(col.ColumnName));
            sb.Append($"            {SpTemplateRendererUtilities.BracketIdentifier(col.ColumnName)} = @{paramName}");
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
            sb.Append($"        {SpTemplateRendererUtilities.BracketIdentifier(cols[i].ColumnName)}");
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
            var orderByCol = SpTemplateRendererUtilities.ValidateSqlIdentifier(cols[i], "orderByColumns");
            sb.Append($"        {SpTemplateRendererUtilities.BracketIdentifier(orderByCol)}");
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
                var parameterName = SpTemplateRendererUtilities.ValidateSqlIdentifier(filter.ColumnName, nameof(filter.ColumnName));
                // BETWEEN requires two parameters: Start and End
                sb.Append($"    @{parameterName}Start {SpTemplateRendererUtilities.GetSqlType(col)}");
                if (filter.IsOptional)
                {
                    sb.Append(" = NULL");
                }

                sb.AppendLine(",");
                sb.Append($"    @{parameterName}End {SpTemplateRendererUtilities.GetSqlType(col)}");
                if (filter.IsOptional)
                {
                    sb.Append(" = NULL");
                }

                sb.AppendLine(",");
            }
            else if (filter.Operator == FilterOperator.In)
            {
                var parameterName = SpTemplateRendererUtilities.ValidateSqlIdentifier(filter.ColumnName, nameof(filter.ColumnName));
                sb.Append($"    @{parameterName} NVARCHAR(MAX)");
                if (filter.IsOptional)
                {
                    sb.Append(" = NULL");
                }

                sb.AppendLine(",");
            }
            else
            {
                var parameterName = SpTemplateRendererUtilities.ValidateSqlIdentifier(filter.ColumnName, nameof(filter.ColumnName));
                sb.Append($"    @{parameterName} {SpTemplateRendererUtilities.GetSqlType(col)}");
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
            var parameterName = SpTemplateRendererUtilities.ValidateSqlIdentifier(filter.ColumnName, nameof(filter.ColumnName));
            var columnIdentifier = SpTemplateRendererUtilities.BracketIdentifier(parameterName);
            if (filter.IsOptional)
            {
                if (filter.Operator == FilterOperator.Between)
                {
                    sb.Append($"        AND (@{parameterName}Start IS NULL OR @{parameterName}End IS NULL OR ");
                }
                else if (filter.Operator == FilterOperator.In)
                {
                    sb.Append($"        AND (@{parameterName} IS NULL OR LTRIM(RTRIM(@{parameterName})) = '' OR ");
                }
                else
                {
                    sb.Append($"        AND (@{parameterName} IS NULL OR ");
                }
            }
            else
            {
                sb.Append($"        AND ");
            }

            switch (filter.Operator)
            {
                case FilterOperator.Equals:
                    sb.Append($"{columnIdentifier} = @{parameterName}");
                    break;
                case FilterOperator.Like:
                    sb.Append($"{columnIdentifier} LIKE CONCAT('%', COALESCE(@{parameterName}, ''), '%')");
                    break;
                case FilterOperator.GreaterThan:
                    sb.Append($"{columnIdentifier} > @{parameterName}");
                    break;
                case FilterOperator.LessThan:
                    sb.Append($"{columnIdentifier} < @{parameterName}");
                    break;
                case FilterOperator.Between:
                    sb.Append($"{columnIdentifier} BETWEEN @{parameterName}Start AND @{parameterName}End");
                    break;
                case FilterOperator.In:
                    sb.Append($"{columnIdentifier} IN (SELECT LTRIM(RTRIM(value)) FROM STRING_SPLIT(@{parameterName}, ',') WHERE LTRIM(RTRIM(value)) <> '')");
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
