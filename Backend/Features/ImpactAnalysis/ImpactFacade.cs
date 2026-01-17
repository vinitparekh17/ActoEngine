using ActoEngine.WebApi.Features.ImpactAnalysis.Domain;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Contracts;

namespace ActoEngine.WebApi.Features.ImpactAnalysis;
/// <summary>
/// Authoritative orchestrator for Impact Analysis.
/// This class coordinates graph building, path enumeration,
/// scoring, aggregation, and approval policy evaluation.
/// </summary>
public sealed class ImpactFacade(
    IDependencyRepository dependencyRepository,
    IGraphBuilder graphBuilder,
    IPathEnumerator pathEnumerator,
    IPathRiskEvaluator riskEvaluator,
    IImpactAggregator impactAggregator,
    IApprovalPolicy approvalPolicy) : IImpactFacade
{
    public async Task<ImpactResult> AnalyzeAsync(
        int projectId,
        EntityRef rootEntity,
        ChangeType changeType,
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch raw dependency edges
        var dependencyRows =
            await dependencyRepository.GetDownstreamDependentsAsync(
                projectId,
                rootEntity.Type.ToString().ToUpperInvariant(),
                rootEntity.Id,
                cancellationToken);

        // 1a. Fast path: Zero dependencies
        if (dependencyRows.Count == 0)
        {
            return new ImpactResult
            {
                RootEntity = rootEntity,
                ChangeType = changeType,
                ScoringVersion = riskEvaluator.Version,
                ScoringPolicySnapshot = riskEvaluator.PolicySnapshot,
                TotalPaths = 0,
                TotalEntities = 0,
                MaxDepthReached = 0,
                IsTruncated = false,
                OverallImpact = new OverallImpactSummary
                {
                    WorstImpactLevel = ImpactLevel.None,
                    WorstRiskScore = 0,
                    TriggeringEntity = rootEntity, // Self-reference as placeholder
                    TriggeringPathId = string.Empty,
                    RequiresApproval = false
                },
                EntityImpacts = [],
                Paths = []
            };
        }

        // 2. Build in-memory graph
        var graph = graphBuilder.Build(dependencyRows);

        // Enrich root entity with name if found in graph (populated from DB metadata)
        if (graph.Contains(rootEntity))
        {
            rootEntity = graph.GetNode(rootEntity).Entity;
        }

        // 3. Enumerate dependency paths (BFS, bounded)
        var pathEnumeration = pathEnumerator.Enumerate(graph, rootEntity);

        // 4. Score each path (immutable transformation)
        var scoredPaths = pathEnumeration.Paths
            .Select(p => riskEvaluator.Evaluate(p, changeType))
            .ToList();

        // 5. Aggregate impacts (entity + overall)
        var aggregation = impactAggregator.Aggregate(scoredPaths);

        // 6. Apply approval policy
        var approvedOverallImpact =
            approvalPolicy.Evaluate(aggregation.OverallImpact);

        // 7. Assemble final authoritative result
        return new ImpactResult
        {
            RootEntity = rootEntity,
            ChangeType = changeType,

            ScoringVersion = riskEvaluator.Version,
            ScoringPolicySnapshot = riskEvaluator.PolicySnapshot,

            TotalPaths = scoredPaths.Count,
            TotalEntities = aggregation.EntityImpacts.Count,
            MaxDepthReached = pathEnumeration.MaxDepthReached,

            IsTruncated = pathEnumeration.IsTruncated,
            TruncationReason = pathEnumeration.TruncationReason,

            OverallImpact = approvedOverallImpact,
            EntityImpacts = aggregation.EntityImpacts,
            Paths = scoredPaths
        };
    }
}