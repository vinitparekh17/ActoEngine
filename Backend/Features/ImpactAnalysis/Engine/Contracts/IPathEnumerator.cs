using ActoEngine.WebApi.Features.ImpactAnalysis.Domain;

namespace ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Contracts;

/// <summary>
/// Enumerates dependency paths starting from a root entity.
/// Implementations MUST use breadth-first traversal (BFS)
/// and MUST enforce truncation rules deterministically.
/// </summary>
public interface IPathEnumerator
{
    /// <summary>
    /// Enumerates all dependency paths starting from the root entity.
    /// 
    /// Guarantees:
    /// - Breadth-first traversal (BFS)
    /// - Depth is bounded by domain rules
    /// - Path count is bounded by safety limits
    /// - Truncation is explicit and observable
    /// </summary>
    /// <param name="graph">Pre-built immutable impact graph</param>
    /// <param name="root">Root entity for impact analysis</param>
    /// <returns>
    /// A PathEnumerationResult containing all discovered paths
    /// and truncation metadata if limits were reached.
    /// </returns>
    PathEnumerationResult Enumerate(
        ImpactGraph graph,
        EntityRef root);
}

/// <summary>
/// Result of path enumeration from a root entity.
/// </summary>
public sealed class PathEnumerationResult
{
    public required IReadOnlyList<DependencyPath> Paths { get; init; }

    public required bool IsTruncated { get; init; }
    public string? TruncationReason { get; init; }

    public required int MaxDepthReached { get; init; }
}