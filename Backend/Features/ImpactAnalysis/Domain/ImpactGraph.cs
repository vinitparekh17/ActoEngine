namespace ActoEngine.WebApi.Services.ImpactAnalysis.Domain;
/// <summary>
/// Immutable directed graph optimized for BFS traversal.
/// </summary>
public sealed class ImpactGraph
{
    private readonly IReadOnlyDictionary<EntityRef, GraphNode> _nodes;
    private readonly IReadOnlyDictionary<EntityRef, IReadOnlyList<GraphEdge>> _adjacency;

    public ImpactGraph(
        Dictionary<EntityRef, GraphNode> nodes,
        Dictionary<EntityRef, List<GraphEdge>> adjacency)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(adjacency);

        // Defensive copy of nodes dictionary
        _nodes = new Dictionary<EntityRef, GraphNode>(nodes);

        // Defensive deep copy of adjacency dictionary (copy lists to prevent mutation)
        var adjacencyCopy = new Dictionary<EntityRef, IReadOnlyList<GraphEdge>>();
        foreach (var kvp in adjacency)
        {
            adjacencyCopy[kvp.Key] = kvp.Value.ToArray();
        }
        _adjacency = adjacencyCopy;
    }

    public IReadOnlyCollection<GraphNode> Nodes => _nodes.Values.ToArray();

    public bool Contains(EntityRef entity) => _nodes.ContainsKey(entity);

    public GraphNode? GetNode(EntityRef entity) =>
        _nodes.TryGetValue(entity, out var node) ? node : null;

    public IReadOnlyList<GraphEdge> GetOutgoingEdges(EntityRef from)
        => _adjacency.TryGetValue(from, out var edges)
            ? edges
            : Array.Empty<GraphEdge>();
}