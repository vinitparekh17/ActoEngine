using ActoEngine.WebApi.Services.ImpactAnalysis.Domain;

namespace ActoEngine.WebApi.Services.ImpactAnalysis.Engine.Pathing
{
    /// Represents an in-progress traversal path during BFS.
/// This type is mutable only during construction; each expansion creates a new instance.
/// </summary>
internal sealed class PathState(
    IReadOnlyList<EntityRef> nodes,
    IReadOnlyList<DependencyType> edges,
    DependencyType maxDependencyType,
    int maxCriticalityLevel)
    {
        public IReadOnlyList<EntityRef> Nodes { get; } = nodes;
        public IReadOnlyList<DependencyType> Edges { get; } = edges;

        public EntityRef Current => Nodes[^1];

    public int Depth => Edges.Count;

        public DependencyType MaxDependencyType { get; } = maxDependencyType;
        public int MaxCriticalityLevel { get; } = maxCriticalityLevel;

        public bool Contains(EntityRef entity)
        => Nodes.Contains(entity);
}
}