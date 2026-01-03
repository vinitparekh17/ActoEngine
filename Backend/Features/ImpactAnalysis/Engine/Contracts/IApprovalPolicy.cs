using ActoEngine.WebApi.Services.ImpactAnalysis.Domain;

namespace ActoEngine.WebApi.Services.ImpactAnalysis.Engine.Contracts;

public interface IApprovalPolicy
{
    OverallImpactSummary Evaluate(
        OverallImpactSummary overallImpact);
}