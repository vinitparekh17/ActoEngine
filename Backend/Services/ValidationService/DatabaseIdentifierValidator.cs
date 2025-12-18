using System.Text.RegularExpressions;

namespace ActoEngine.WebApi.Services.ValidationService;

public interface IDatabaseIdentifierValidator
{
    bool IsValidIdentifier(string identifier);
    string ValidateIdentifier(string identifier);
}

public partial class DatabaseIdentifierValidator : IDatabaseIdentifierValidator
{
    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex IdentifierRegex();

    public bool IsValidIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return false;
        if (identifier.Length > 128) return false;
        return IdentifierRegex().IsMatch(identifier);
    }

    public string ValidateIdentifier(string identifier)
    {
        if (!IsValidIdentifier(identifier))
        {
            throw new ArgumentException("Invalid database identifier");
        }
        return identifier;
    }
}
