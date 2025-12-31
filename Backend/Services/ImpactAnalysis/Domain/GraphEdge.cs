namespace ActoEngine.WebApi.Services.ImpactAnalysis.Domain;

public sealed class GraphEdge
{
    public required EntityRef From { get; init; }
    public required EntityRef To { get; init; }

    public required DependencyType DependencyType { get; init; }
}