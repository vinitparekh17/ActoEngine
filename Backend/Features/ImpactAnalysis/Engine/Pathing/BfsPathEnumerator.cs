using ActoEngine.WebApi.Features.ImpactAnalysis.Domain;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Contracts;

namespace ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Pathing;

/// <summary>
/// Breadth-first dependency path enumerator.
/// This implementation is authoritative and enforces
/// domain depth limits and safety path limits.
/// </summary>
public sealed class BfsPathEnumerator : IPathEnumerator
{
    private readonly int _maxDepth;
    private readonly int _maxPaths;

    public BfsPathEnumerator(int maxDepth, int maxPaths)
    {
        if (maxDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth));
        }

        if (maxPaths <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPaths));
        }

        _maxDepth = maxDepth;
        _maxPaths = maxPaths;
    }

    public PathEnumerationResult Enumerate(
        ImpactGraph graph,
        EntityRef root)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(root);

        var paths = new List<DependencyPath>();
        var queue = new Queue<PathState>();

        bool isTruncated = false;
        int maxDepthReached = 0;

        // Initialize BFS with root-only path
        var rootNode = graph.GetNode(root)
            ?? throw new InvalidOperationException(
                $"Root entity '{root.StableKey}' not found in graph. Ensure the graph contains the root before enumeration.");

        var initialState = new PathState(
            nodes: [root],
            edges: [],
            maxDependencyType: DependencyType.Unknown,
            maxCriticalityLevel: rootNode.CriticalityLevel
        );

        queue.Enqueue(initialState);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            // Stop if safety path limit is reached
            if (paths.Count >= _maxPaths)
            {
                isTruncated = true;
                break;
            }

            foreach (var edge in graph.GetOutgoingEdges(current.Current))
            {
                // Cycle detection (per-path)
                if (current.Contains(edge.To))
                {
                    continue;
                }

                var nextDepth = current.Depth + 1;
                if (nextDepth > _maxDepth)
                {
                    isTruncated = true;
                    continue;
                }

                var nextNode = graph.GetNode(edge.To);
                if (nextNode == null)
                {
                    // Node referenced by edge but not in graph; skip silently
                    continue;
                }

                var nextNodes = new List<EntityRef>(current.Nodes)
                {
                    edge.To
                };

                var nextEdges = new List<DependencyType>(current.Edges)
                {
                    edge.DependencyType
                };

                var nextMaxDependency = MaxDependency(
                    current.MaxDependencyType,
                    edge.DependencyType);

                var nextMaxCriticality = Math.Max(
                    current.MaxCriticalityLevel,
                    nextNode.CriticalityLevel);

                var nextState = new PathState(
                    nodes: nextNodes,
                    edges: nextEdges,
                    maxDependencyType: nextMaxDependency,
                    maxCriticalityLevel: nextMaxCriticality
                );

                // Emit path immediately (BFS guarantee)
                paths.Add(CreateDependencyPath(nextState));

                maxDepthReached = Math.Max(maxDepthReached, nextState.Depth);

                // Enqueue for further expansion
                if (nextState.Depth < _maxDepth)
                {
                    queue.Enqueue(nextState);
                }
                else if (graph.GetOutgoingEdges(nextState.Current).Count > 0)
                {
                    // We reached depth boundary and did not expand a node that still has dependents.
                    // Mark analysis as truncated so callers can treat result as bounded.
                    isTruncated = true;
                }

                if (paths.Count >= _maxPaths)
                {
                    isTruncated = true;
                    break;
                }
            }
        }

        return new PathEnumerationResult
        {
            Paths = paths,
            IsTruncated = isTruncated,
            TruncationReason = isTruncated ? "PATH_LIMIT_OR_DEPTH_LIMIT" : null,
            MaxDepthReached = maxDepthReached
        };
    }

    private static DependencyType MaxDependency(
        DependencyType a,
        DependencyType b)
    {
        return (DependencyType)Math.Max((int)a, (int)b);
    }

    private static DependencyPath CreateDependencyPath(PathState state)
    {
        // PathId must be deterministic
        var pathId = string.Join(
            "->",
            state.Nodes.Select(n => n.StableKey));

        return new DependencyPath
        {
            PathId = pathId,
            Nodes = state.Nodes,
            Edges = state.Edges,
            Depth = state.Depth,
            MaxDependencyType = state.MaxDependencyType,
            MaxCriticalityLevel = state.MaxCriticalityLevel,

            // Scoring placeholders (filled later by PathRiskEvaluator)
            RiskScore = 0,
            ImpactLevel = ImpactLevel.None,
            DominantEntity = state.Nodes[^1],
            DominantDependencyType = state.MaxDependencyType
        };
    }
}
