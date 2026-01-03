using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.ImpactAnalysis.Domain;

namespace ActoEngine.WebApi.Services.ImpactAnalysis.Mapping;

/// <summary>
/// Maps ImpactResult domain model to API response DTO.
/// Pure transformation layer.
/// </summary>
public sealed class ImpactResponseMapper
{
    // public ImpactAnalysisResponse Map(ImpactResult result)
    // {
    //     return new ImpactAnalysisResponse
    //     {
    //         Root = MapEntity(result.RootEntity),
    //         ChangeType = result.ChangeType.ToString(),
    //         ScoringVersion = result.ScoringVersion,

    //         RequiresApproval = result.OverallImpact.RequiresApproval,
    //         WorstImpactLevel = result.OverallImpact.WorstImpactLevel.ToString(),

    //         IsTruncated = result.IsTruncated,
    //         TruncationReason = result.TruncationReason,

    //         Entities = [.. result.EntityImpacts
    //             .OrderByDescending(e => e.WorstCaseImpactLevel)
    //             .ThenByDescending(e => e.WorstCaseRiskScore)
    //             .Select(MapEntityImpact)],

    //         Paths = [.. result.Paths
    //             .OrderByDescending(p => p.RiskScore)
    //             .ThenBy(p => p.Depth)
    //             .Select(MapPath)]
    //     };
    // }

    // -----------------------------
    // Entity mapping
    // -----------------------------

    private static EntityDto MapEntity(EntityRef entity)
    {
        return new EntityDto
        {
            Type = entity.Type.ToString(),
            Id = entity.Id,
            Name = entity.Name
        };
    }

    private static EntityImpactDto MapEntityImpact(EntityImpact impact)
    {
        return new EntityImpactDto
        {
            Entity = MapEntity(impact.Entity),

            WorstCaseImpactLevel = impact.WorstCaseImpactLevel.ToString(),
            WorstCaseRiskScore = impact.WorstCaseRiskScore,

            CumulativeRiskScore = impact.CumulativeRiskScore,

            DominantPathId = impact.DominantPathId,

            PathIds = [.. impact.Paths.Select(p => p.PathId)]
        };
    }

    // -----------------------------
    // Path mapping
    // -----------------------------

    private static DependencyPathDto MapPath(DependencyPath path)
    {
        return new DependencyPathDto
        {
            PathId = path.PathId,

            NodeSequence = [.. path.Nodes.Select(n => $"{n.Type}:{n.Id}")],

            DependencySequence = [.. path.Edges.Select(e => e.ToString())],

            Depth = path.Depth,
            RiskScore = path.RiskScore,
            ImpactLevel = path.ImpactLevel.ToString()
        };
    }
}