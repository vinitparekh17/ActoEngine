namespace ActoEngine.WebApi.Features.ImpactAnalysis.Domain;

public sealed class DependencyPath
{
    public required string PathId { get; init; }

    public required IReadOnlyList<EntityRef> Nodes { get; init; }
    public required IReadOnlyList<DependencyType> Edges { get; init; }

    public required int Depth { get; init; }

    public required DependencyType MaxDependencyType { get; init; }
    public required int MaxCriticalityLevel { get; init; }

    // Scoring output
    public required int RiskScore { get; init; }
    public required ImpactLevel ImpactLevel { get; init; }

    // Explainability
    public required EntityRef DominantEntity { get; init; }
    public required DependencyType DominantDependencyType { get; init; }
}
