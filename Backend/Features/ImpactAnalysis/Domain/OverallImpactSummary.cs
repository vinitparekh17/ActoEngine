namespace ActoEngine.WebApi.Services.ImpactAnalysis.Domain;

public sealed class OverallImpactSummary
{
    public required ImpactLevel WorstImpactLevel { get; init; }
    public required int WorstRiskScore { get; init; }

    public required EntityRef TriggeringEntity { get; init; }
    public required string TriggeringPathId { get; init; }

    public required bool RequiresApproval { get; init; }
}
