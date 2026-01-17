namespace ActoEngine.WebApi.Features.ImpactAnalysis.Domain;

public sealed class EntityImpact
{
    public required EntityRef Entity { get; init; }

    public required ImpactLevel WorstCaseImpactLevel { get; init; }
    public required int WorstCaseRiskScore { get; init; }

    public required int CumulativeRiskScore { get; init; }

    public required string DominantPathId { get; init; }

    public required IReadOnlyList<DependencyPath> Paths { get; init; }
}
