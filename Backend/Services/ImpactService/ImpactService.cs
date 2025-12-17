using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;

namespace ActoEngine.WebApi.Services.ImpactService;

public interface IImpactService
{
    Task<ImpactAnalysisResponse> GetImpactAnalysisAsync(int projectId, string entityType, int entityId, string changeType, CancellationToken cancellationToken = default);
}

public class ImpactService(
    IDependencyRepository depRepo,
    ILogger<ImpactService> logger) : IImpactService
{
    public async Task<ImpactAnalysisResponse> GetImpactAnalysisAsync(
        int projectId,
        string entityType,
        int entityId,
        string changeType,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Analyzing impact for {EntityType} {EntityId} in project {ProjectId}", entityType, entityId, projectId);

        // 1. Get the full tree (Transitive dependencies)
        var rawGraph = await depRepo.GetDownstreamDependentsAsync(projectId, entityType, entityId, cancellationToken);

        // Null check to prevent NullReferenceException
        if (rawGraph == null || !rawGraph.Any())
        {
            logger.LogInformation("No dependencies found for {EntityType} {EntityId}", entityType, entityId);
            return new ImpactAnalysisResponse
            {
                RootId = $"{entityType}_{entityId}",
                TotalRiskScore = 0,
                RequiresApproval = false
            };
        }

        // 2. Distinct list of affected entities (deduplicated)
        var uniqueAffected = rawGraph
            .GroupBy(g => new { g.SourceType, g.SourceId })
            .Select(g => g.OrderBy(n => n.Depth).First())
            .ToList();

        var affectedEntities = new List<AffectedEntity>();
        var edges = new List<GraphEdge>();

        // 3. Build Analysis & Edges
        foreach (var node in uniqueAffected)
        {
            // Calculate Impact based on Change Type AND Depth
            var (impactLevel, score) = CalculateRisk(changeType, node.DependencyType, node.CriticalityLevel, node.Depth);

            affectedEntities.Add(new AffectedEntity
            {
                EntityType = node.SourceType,
                EntityId = node.SourceId,
                EntityName = node.EntityName ?? "Unknown",
                Owner = node.DataOwner ?? "Unassigned",
                Depth = node.Depth,
                ImpactLevel = impactLevel,
                RiskScore = score,
                Reason = FormatReason(changeType, node.DependencyType, node.Depth),
                CriticalityLevel = node.CriticalityLevel
            });
        }

        // 4. Build Edges for Visualization
        foreach (var node in rawGraph)
        {
            edges.Add(new GraphEdge
            {
                Source = $"{node.TargetType}_{node.TargetId}", // The dependency
                Target = $"{node.SourceType}_{node.SourceId}", // The dependent
                Type = node.DependencyType
            });
        }

        // 5. Calculate summary statistics
        var summary = new ImpactSummary
        {
            CriticalCount = affectedEntities.Count(e => e.ImpactLevel == "CRITICAL"),
            HighCount = affectedEntities.Count(e => e.ImpactLevel == "HIGH"),
            MediumCount = affectedEntities.Count(e => e.ImpactLevel == "MEDIUM"),
            LowCount = affectedEntities.Count(e => e.ImpactLevel == "LOW")
        };

        int totalRisk = Math.Min(100, affectedEntities.Sum(e => e.RiskScore));

        logger.LogInformation("Impact analysis complete: {TotalEntities} affected, risk score {RiskScore}", affectedEntities.Count, totalRisk);

        return new ImpactAnalysisResponse
        {
            RootId = $"{entityType}_{entityId}",
            TotalRiskScore = totalRisk,
            RequiresApproval = totalRisk > 50,
            Summary = summary,
            AffectedEntities = affectedEntities.OrderBy(e => e.Depth).ThenByDescending(e => e.RiskScore).ToList(),
            GraphEdges = edges
        };
    }

    private static (string Level, int Score) CalculateRisk(string changeType, string depType, int criticality, int depth)
    {
        // Base weights
        int typeWeight = depType switch { "INSERT" => 10, "UPDATE" => 10, "DELETE" => 10, "SELECT" => 5, _ => 1 };
        int changeMultiplier = changeType == "DELETE" ? 3 : (changeType == "MODIFY" ? 2 : 1);

        // Depth decay: Direct impact (Depth 1) is 100%, Depth 2 is 80%, Depth 3 is 60%...
        double depthFactor = Math.Max(0.2, 1.0 - ((depth - 1) * 0.2));

        // Criticality: Default to 3 if unknown
        int critFactor = criticality == 0 ? 3 : criticality;

        double rawScore = (typeWeight * changeMultiplier * critFactor) * depthFactor;

        string level = rawScore switch
        {
            > 50 => "CRITICAL",
            > 25 => "HIGH",
            > 10 => "MEDIUM",
            _ => "LOW"
        };

        return (level, (int)rawScore);
    }

    private static string FormatReason(string changeType, string depType, int depth)
    {
        string direct = $"Uses {depType}";
        return depth == 1 ? $"Direct Dependency: {direct}" : $"Indirect Dependency (Level {depth}): Chain reaction";
    }
}
