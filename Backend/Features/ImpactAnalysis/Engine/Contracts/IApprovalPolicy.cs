using ActoEngine.WebApi.Features.ImpactAnalysis.Domain;

namespace ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Contracts;

public interface IApprovalPolicy
{
    OverallImpactSummary Evaluate(
        OverallImpactSummary overallImpact);
}