using ActoEngine.WebApi.Services.ImpactAnalysis.Domain;

namespace ActoEngine.WebApi.Services.ImpactAnalysis.Engine.Contracts;

public interface IImpactAggregator
{
    ImpactAggregationResult Aggregate(
        IReadOnlyList<DependencyPath> scoredPaths);
}


public sealed class ImpactAggregationResult
{
    public required OverallImpactSummary OverallImpact { get; init; }

    public required IReadOnlyList<EntityImpact> EntityImpacts { get; init; }
}