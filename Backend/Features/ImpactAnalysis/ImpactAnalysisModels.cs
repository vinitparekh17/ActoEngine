using ActoEngine.WebApi.Features.ImpactAnalysis.Domain;

namespace ActoEngine.WebApi.Features.ImpactAnalysis;

/// <summary>
/// Represents an entity affected by a change
/// </summary>
public class AffectedEntity
{
    public required string EntityType { get; set; }
    public int EntityId { get; set; }
    public required string EntityName { get; set; }
    public required string Owner { get; set; }
    public int Depth { get; set; }
    public required string ImpactLevel { get; set; }
    public int RiskScore { get; set; }
    public required string Reason { get; set; }
    public int CriticalityLevel { get; set; }
}

/// <summary>
/// Graph edge for visualization
/// </summary>
public class GraphEdge
{
    public required string Source { get; set; }
    public required string Target { get; set; }
    public required string Type { get; set; }
}

/// <summary>
/// Summary statistics of impact analysis
/// </summary>
public class ImpactSummary
{
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }
}

/// <summary>
/// Internal result from impact calculation
/// </summary>
public class ImpactAnalysisResult
{
    public required string RootId { get; set; }
    public int TotalRiskScore { get; set; }
    public List<AffectedEntity> AffectedEntities { get; set; } = [];
    public List<GraphEdge> GraphEdges { get; set; } = [];
}

/// <summary>
/// API response for impact analysis
/// </summary>
public class ImpactDecisionResponse
{
    // === Decision (what users read first) ===
    public ImpactVerdict Verdict { get; set; } = default!;

    // === Aggregated facts (secondary) ===
    public object Summary { get; set; } = default!;

    // === Ranked entity impacts (trimmed) ===
    public List<object> Entities { get; set; } = [];

    // === Evidence (expandable / optional) ===
    public object? Paths { get; set; }
    public object? Graph { get; set; }
}

public sealed class EntityDto
{
    public required string Type { get; init; }
    public required int Id { get; init; }
    public string? Name { get; init; }
}

public sealed class DependencyPathDto
{
    public required string PathId { get; init; }
    public required IReadOnlyList<string> NodeSequence { get; init; }
    public required IReadOnlyList<string> DependencySequence { get; init; }

    public required int Depth { get; init; }
    public required int RiskScore { get; init; }
    public required string ImpactLevel { get; init; }
}

public sealed class EntityImpactDto
{
    public required EntityDto Entity { get; init; }

    public required string WorstCaseImpactLevel { get; init; }
    public required int WorstCaseRiskScore { get; init; }

    public required int CumulativeRiskScore { get; init; }

    public required string DominantPathId { get; init; }

    public required IReadOnlyList<string> PathIds { get; init; }
}