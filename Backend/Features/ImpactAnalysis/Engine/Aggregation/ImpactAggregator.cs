using ActoEngine.WebApi.Features.ImpactAnalysis.Domain;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Contracts;

namespace ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Aggregation;

/// <summary>
/// Aggregates scored dependency paths into entity-level and overall impact.
/// Worst-case dominates, cumulative risk is preserved.
/// </summary>
public sealed class ImpactAggregator : IImpactAggregator
{
    public ImpactAggregationResult Aggregate(
        IReadOnlyList<DependencyPath> scoredPaths)
    {
        if (scoredPaths.Count == 0)
        {
            return new ImpactAggregationResult
            {
                OverallImpact = new OverallImpactSummary
                {
                    WorstImpactLevel = ImpactLevel.None,
                    WorstRiskScore = 0,
                    TriggeringEntity = null!,
                    TriggeringPathId = string.Empty,
                    RequiresApproval = false
                },
                EntityImpacts = Array.Empty<EntityImpact>()
            };
        }

        // Group paths by terminal entity (last node in path)
        // Filter out any paths with empty Nodes to prevent IndexOutOfRangeException
        var entityGroups = scoredPaths
            .Where(p => p.Nodes != null && p.Nodes.Count > 0)
            .GroupBy(p => p.Nodes[^1]);

        var entityImpacts = new List<EntityImpact>();

        foreach (var group in entityGroups)
        {
            var paths = group.ToList();

            var dominantPath = paths
                .OrderByDescending(p => p.RiskScore)
                .First();

            var worstImpactLevel = paths
                .Max(p => p.ImpactLevel);

            var worstRiskScore = paths
                .Max(p => p.RiskScore);

            var cumulativeRisk = paths
                .Sum(p => p.RiskScore);

            entityImpacts.Add(new EntityImpact
            {
                Entity = group.Key,
                WorstCaseImpactLevel = worstImpactLevel,
                WorstCaseRiskScore = worstRiskScore,
                CumulativeRiskScore = cumulativeRisk,
                DominantPathId = dominantPath.PathId,
                Paths = paths
            });
        }

        if (entityImpacts.Count == 0)
        {
            return new ImpactAggregationResult
            {
                OverallImpact = new OverallImpactSummary
                {
                    WorstImpactLevel = ImpactLevel.None,
                    WorstRiskScore = 0,
                    TriggeringEntity = null!,
                    TriggeringPathId = string.Empty,
                    RequiresApproval = false
                },
                EntityImpacts = Array.Empty<EntityImpact>()
            };
        }

        // Determine overall worst-case entity
        var triggeringEntityImpact = entityImpacts
            .OrderByDescending(e => e.WorstCaseImpactLevel)
            .ThenByDescending(e => e.WorstCaseRiskScore)
            .First();

        var overallImpact = new OverallImpactSummary
        {
            WorstImpactLevel = triggeringEntityImpact.WorstCaseImpactLevel,
            WorstRiskScore = triggeringEntityImpact.WorstCaseRiskScore,
            TriggeringEntity = triggeringEntityImpact.Entity,
            TriggeringPathId = triggeringEntityImpact.DominantPathId,

            // Approval decision happens later (policy)
            RequiresApproval = false
        };

        return new ImpactAggregationResult
        {
            OverallImpact = overallImpact,
            EntityImpacts = entityImpacts
        };
    }
}
