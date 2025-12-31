namespace ActoEngine.WebApi.Services.ImpactAnalysis.Domain;
/// <summary>
/// Immutable directed graph optimized for BFS traversal.
/// </summary>
public sealed class ImpactGraph(
    Dictionary<EntityRef, GraphNode> nodes,
    Dictionary<EntityRef, List<GraphEdge>> adjacency)
{
    public IReadOnlyCollection<GraphNode> Nodes => nodes.Values;

    public bool Contains(EntityRef entity) => nodes.ContainsKey(entity);

    public GraphNode GetNode(EntityRef entity) => nodes[entity];

    public IReadOnlyList<GraphEdge> GetOutgoingEdges(EntityRef from)
        => adjacency.TryGetValue(from, out var edges)
            ? edges
            : Array.Empty<GraphEdge>();
}