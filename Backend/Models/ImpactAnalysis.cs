namespace ActoEngine.WebApi.Models;

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
public class ImpactAnalysisResponse
{
    public required string RootId { get; set; }
    public int TotalRiskScore { get; set; }
    public bool RequiresApproval { get; set; }
    public ImpactSummary Summary { get; set; } = new();
    public List<AffectedEntity> AffectedEntities { get; set; } = [];
    public List<GraphEdge> GraphEdges { get; set; } = [];
}
