namespace ActoEngine.WebApi.Features.LogicalFk;

/// <summary>
/// Classifies a numeric confidence score into a human-readable confidence band.
/// Thresholds: Low (&lt;0.55), Possible (0.55–0.69), Likely (0.70–0.84),
/// VeryLikely (0.85–0.94), HighlyConfident (≥0.95).
/// </summary>
public enum ConfidenceBand
{
    Low,
    Possible,
    Likely,
    VeryLikely,
    HighlyConfident
}

/// <summary>
/// Stateless classifier that maps a decimal confidence value to a <see cref="ConfidenceBand"/>.
/// </summary>
public static class ConfidenceBandClassifier
{
    public static ConfidenceBand Classify(decimal confidence)
    {
        if (confidence >= 0.95m)
            return ConfidenceBand.HighlyConfident;

        if (confidence >= 0.85m)
            return ConfidenceBand.VeryLikely;

        if (confidence >= 0.70m)
            return ConfidenceBand.Likely;

        if (confidence >= 0.55m)
            return ConfidenceBand.Possible;

        return ConfidenceBand.Low;
    }
}
