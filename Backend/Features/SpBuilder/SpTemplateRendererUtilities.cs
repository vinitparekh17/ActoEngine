namespace ActoEngine.WebApi.Features.SpBuilder;

public static class SpTemplateRendererUtilities
{
    /// <summary>
    /// Brackets and escapes a possibly schema-qualified identifier like "schema.Table" or "Table".
    /// If the parts are already bracketed, they will be normalized.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when name is null, empty, or whitespace.</exception>
    public static string BracketQualifiedName(string name)
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
    public static string BracketIdentifier(string identifier)
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

    public static string GetSqlType(SpColumnConfig col)
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

