namespace ActoEngine.WebApi.Features.LogicalFk;

/// <summary>
/// Shared limits for DetectionReason persistence and formatting.
/// </summary>
public static class LogicalFkDetectionReasonPolicy
{
    public const int DetectionReasonMaxChars = 4000;
    public const int SpNamesPreviewCount = 10;

    public static string Clamp(string? reason)
    {
        if (string.IsNullOrEmpty(reason))
        {
            return string.Empty;
        }

        return reason.Length <= DetectionReasonMaxChars
            ? reason
            : reason[..DetectionReasonMaxChars];
    }
}
