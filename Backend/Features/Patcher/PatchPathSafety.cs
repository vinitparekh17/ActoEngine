namespace ActoEngine.WebApi.Features.Patcher;

internal static class PatchPathSafety
{
    public static string NormalizePath(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"{parameterName} must not be empty.");
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"{parameterName} contains an invalid path value.", ex);
        }
    }

    public static string ResolveRelativePathUnderRoot(string rootPath, string relativePath, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException($"{parameterName} must not be empty.");
        }

        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException($"{parameterName} must be project-relative.");
        }

        var combined = NormalizePath(Path.Combine(rootPath, relativePath), parameterName);
        EnsurePathIsUnderRoot(combined, rootPath, parameterName);
        return combined;
    }

    public static string ResolvePath(string rootPath, string configuredPath, string parameterName)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return NormalizePath(configuredPath, parameterName);
        }

        return NormalizePath(Path.Combine(rootPath, configuredPath), parameterName);
    }

    public static void EnsurePathIsUnderRoot(string candidatePath, string rootPath, string parameterName)
    {
        var normalizedCandidate = NormalizePath(candidatePath, parameterName);
        var normalizedRoot = NormalizePath(rootPath, $"{parameterName}.Root");
        var rootWithSeparator = normalizedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        if (!normalizedCandidate.StartsWith(rootWithSeparator, comparison)
            && !string.Equals(normalizedCandidate, normalizedRoot, comparison))
        {
            throw new InvalidOperationException($"{parameterName} escapes the allowed root directory.");
        }
    }

    public static string NormalizeArchiveEntryPath(string entryPath)
    {
        var normalized = entryPath.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Archive entry path must not be empty.");
        }

        if (normalized.Contains(':', StringComparison.Ordinal)
            || normalized.StartsWith("../", StringComparison.Ordinal)
            || normalized.Contains("/../", StringComparison.Ordinal)
            || normalized.Contains("/..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Archive entry path contains traversal sequences.");
        }

        return normalized;
    }
}
