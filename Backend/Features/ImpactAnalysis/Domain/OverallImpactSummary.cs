namespace ActoEngine.WebApi.Features.ImpactAnalysis.Domain;

public sealed class OverallImpactSummary
{
    public required ImpactLevel WorstImpactLevel { get; init; }
    public required int WorstRiskScore { get; init; }

    public required EntityRef TriggeringEntity { get; init; }
    public required string TriggeringPathId { get; init; }

    public required bool RequiresApproval { get; init; }

    /// <summary>
    /// Safe sentinel for empty/no-impact results.
    /// Avoids null! on TriggeringEntity that would NRE in downstream consumers.
    /// </summary>
    public static OverallImpactSummary Empty() => new()
    {
        WorstImpactLevel = ImpactLevel.None,
        WorstRiskScore = 0,
        TriggeringEntity = new EntityRef(EntityType.Table, 0, "(none)"),
        TriggeringPathId = string.Empty,
        RequiresApproval = false
    };
}
