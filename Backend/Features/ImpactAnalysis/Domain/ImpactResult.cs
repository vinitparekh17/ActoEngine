namespace ActoEngine.WebApi.Services.ImpactAnalysis.Domain;

public sealed class ImpactResult
{
    public required EntityRef RootEntity { get; init; }
    public required ChangeType ChangeType { get; init; }

    // Versioning
    public required string ScoringVersion { get; init; }
    public required object ScoringPolicySnapshot { get; init; }

    // Graph statistics
    public required int TotalPaths { get; init; }
    public required int TotalEntities { get; init; }
    public required int MaxDepthReached { get; init; }

    // Truncation
    public required bool IsTruncated { get; init; }
    public string? TruncationReason { get; init; }

    // Results
    public required OverallImpactSummary OverallImpact { get; init; }
    public required IReadOnlyList<EntityImpact> EntityImpacts { get; init; }
    public required IReadOnlyList<DependencyPath> Paths { get; init; }
}
