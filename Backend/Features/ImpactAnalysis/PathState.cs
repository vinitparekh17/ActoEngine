using ActoEngine.WebApi.Features.ImpactAnalysis.Domain;

namespace ActoEngine.WebApi.Features.ImpactAnalysis
{
    /// <summary>
    /// Represents an in-progress traversal path during BFS.
    /// This type is mutable only during construction; each expansion creates a new instance.
    /// </summary>
    internal sealed class PathState
    {
        public IReadOnlyList<EntityRef> Nodes { get; }
        public IReadOnlyList<DependencyType> Edges { get; }

        public EntityRef Current => Nodes[^1];

        public int Depth => Edges.Count;

        public DependencyType MaxDependencyType { get; }
        public int MaxCriticalityLevel { get; }

        public PathState(
            IReadOnlyList<EntityRef> nodes,
            IReadOnlyList<DependencyType> edges,
            DependencyType maxDependencyType,
            int maxCriticalityLevel)
        {
            ArgumentNullException.ThrowIfNull(nodes);
            ArgumentNullException.ThrowIfNull(edges);

            if (nodes.Count == 0)
            {
                throw new ArgumentException("Nodes collection cannot be empty.", nameof(nodes));
            }

            if (nodes.Count != edges.Count + 1)
            {
                throw new ArgumentException(
                    $"Invalid path structure: nodes.Count ({nodes.Count}) must equal edges.Count + 1 ({edges.Count + 1}).",
                    nameof(nodes));
            }

            Nodes = nodes;
            Edges = edges;
            MaxDependencyType = maxDependencyType;
            MaxCriticalityLevel = maxCriticalityLevel;
        }

        public bool Contains(EntityRef entity)
            => Nodes.Contains(entity);
    }
}