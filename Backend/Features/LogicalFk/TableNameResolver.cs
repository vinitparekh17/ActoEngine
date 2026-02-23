namespace ActoEngine.WebApi.Features.LogicalFk;

/// <summary>
/// Resolves table names from naming-convention prefixes and JOIN-condition identifiers
/// to table IDs. Handles pluralisation, case-insensitive matching, schema qualification,
/// and bracket stripping.
/// </summary>
internal static class TableNameResolver
{
    /// <summary>
    /// Try to resolve a column name prefix to a table ID.
    /// Handles: "customer" → "Customers", "customers", "Customer"
    /// </summary>
    internal static int? TryResolveTable(string prefix, Dictionary<string, int> tableNames)
    {
        // Direct match
        if (tableNames.TryGetValue(prefix, out int tableId))
            return tableId;

        // Pluralized: "customer" → "customers"
        if (tableNames.TryGetValue(prefix + "s", out tableId))
            return tableId;

        // "box" -> "boxes"
        if (tableNames.TryGetValue(prefix + "es", out tableId))
            return tableId;

        // Singular from plural: "customers" → "customer"
        if (prefix.EndsWith('s') && tableNames.TryGetValue(prefix[..^1], out tableId))
            return tableId;

        // "ies" pluralization: "category" → "categories"
        if (prefix.EndsWith('y') && tableNames.TryGetValue(prefix[..^1] + "ies", out tableId))
            return tableId;

        return null;
    }

    /// <summary>
    /// Resolve a table name from a JOIN condition to a table ID.
    /// Handles schema-qualified names (dbo.Orders → Orders) and case-insensitive matching.
    /// </summary>
    internal static int? ResolveTableName(string name, Dictionary<string, int> tableNameToId)
    {
        // Direct match
        if (tableNameToId.TryGetValue(name, out var id))
            return id;

        // Strip schema prefix (dbo.Orders → Orders)
        var dotIndex = name.LastIndexOf('.');
        if (dotIndex >= 0)
        {
            var baseName = name[(dotIndex + 1)..];
            if (tableNameToId.TryGetValue(baseName, out id))
                return id;
        }

        // Strip brackets ([dbo].[Orders] → Orders)
        var cleaned = name.Replace("[", "").Replace("]", "");
        if (tableNameToId.TryGetValue(cleaned, out id))
            return id;

        dotIndex = cleaned.LastIndexOf('.');
        if (dotIndex >= 0)
        {
            var baseName = cleaned[(dotIndex + 1)..];
            if (tableNameToId.TryGetValue(baseName, out id))
                return id;
        }

        return null;
    }
}
