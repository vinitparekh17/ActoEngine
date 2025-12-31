namespace ActoEngine.WebApi.Services.ImpactAnalysis.Domain;

/// <summary>
/// Represents a node in the impact graph.
/// Immutable after construction.
/// </summary>
public sealed class GraphNode
{
    public required EntityRef Entity { get; init; }

    /// <summary>
    /// Bounded criticality scale (1–5).
    /// Defaults to 3 (Medium) when unknown.
    /// </summary>
    public int CriticalityLevel { get; init; } = 3;
}