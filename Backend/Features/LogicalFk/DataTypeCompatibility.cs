namespace ActoEngine.WebApi.Features.LogicalFk;

/// <summary>
/// Checks whether two SQL Server data types belong to the same compatibility family
/// for logical FK matching (e.g. int ↔ bigint, varchar ↔ nvarchar).
/// </summary>
internal static class DataTypeCompatibility
{
    /// <summary>
    /// Returns true if <paramref name="sourceType"/> and <paramref name="targetType"/>
    /// normalise to the same type family.
    /// </summary>
    internal static bool AreCompatible(string sourceType, string targetType)
    {
        var source = NormalizeDataType(sourceType);
        var target = NormalizeDataType(targetType);
        return source == target;
    }

    internal static string NormalizeDataType(string dataType)
    {
        var lower = dataType.Trim().ToLowerInvariant();
        return lower switch
        {
            "int" or "bigint" or "smallint" or "tinyint" => "integer_family",
            "uniqueidentifier" => "guid",
            "nvarchar" or "varchar" or "char" or "nchar" => "string_family",
            _ => lower
        };
    }
}
