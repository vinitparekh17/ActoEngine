namespace ActoEngine.WebApi.Services.ImpactAnalysis.Domain;

/// <summary>
/// Represents a node in the impact graph.
/// Immutable after construction.
/// </summary>
public sealed class GraphNode
{
    private int _criticalityLevel = 3;

    public required EntityRef Entity { get; init; }

    /// <summary>
    /// Bounded criticality scale (1–5).
    /// Defaults to 3 (Medium) when unknown.
    /// Values outside range are clamped.
    /// </summary>
    public int CriticalityLevel
    {
        get => _criticalityLevel;
        init => _criticalityLevel = Math.Clamp(value, 1, 5);
    }
}