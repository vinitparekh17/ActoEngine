using ActoEngine.WebApi.Features.ImpactAnalysis.Domain;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Contracts;

namespace ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Approval;



/// <summary>
/// Approval policy v1.
/// Decides whether an impact analysis result requires approval
/// based solely on overall worst-case impact.
///
/// Rules (locked):
/// - CRITICAL or HIGH impact requires approval
/// - MEDIUM and below do not
/// - No scoring or aggregation logic here
/// </summary>
public sealed class ApprovalPolicyV1 : IApprovalPolicy
{
    public OverallImpactSummary Evaluate(OverallImpactSummary overallImpact)
    {
        bool requiresApproval = overallImpact.WorstImpactLevel >= ImpactLevel.High;


        return new OverallImpactSummary
        {
            WorstImpactLevel = overallImpact.WorstImpactLevel,
            WorstRiskScore = overallImpact.WorstRiskScore,
            TriggeringEntity = overallImpact.TriggeringEntity,
            TriggeringPathId = overallImpact.TriggeringPathId,
            RequiresApproval = requiresApproval
        };
    }
}