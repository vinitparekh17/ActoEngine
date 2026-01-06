using System.Text.RegularExpressions;

namespace ActoEngine.WebApi.Infrastructure.Security;

public static partial class SecurityHelper
{
    // Regex to match common connection string keys like Password, Pwd, User Id, Uid, etc.
    // Captures the value part to be redacted.
    [GeneratedRegex(@"(?<=((User\s?Id)|(Uid)|(Password)|(Pwd)|(Access\s?Key)|(Secret\s?Key))\s?=\s?)[^;]+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ConnectionStringSensitiveKeysRegex();

    /// <summary>
    /// Redacts sensitive information from a connection string or any string containing connection string patterns.
    /// </summary>
    /// <param name="input">The input string to sanitize.</param>
    /// <returns>A string with sensitive values replaced by [Redacted].</returns>
    public static string RedactConnectionString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        return ConnectionStringSensitiveKeysRegex().Replace(input, "[Redacted]");
    }
}
