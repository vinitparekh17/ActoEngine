using ActoEngine.WebApi.Features.ImpactAnalysis.Domain;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Approval;

namespace ActoEngine.Tests.ImpactAnalysis;

public class ApprovalPolicyV1Tests
{
    private readonly ApprovalPolicyV1 _policy = new();

    [Theory]
    [InlineData(ImpactLevel.Critical, true)]
    [InlineData(ImpactLevel.High, true)]
    [InlineData(ImpactLevel.Medium, false)]
    [InlineData(ImpactLevel.Low, false)]
    [InlineData(ImpactLevel.None, false)]
    public void Evaluate_AppliesThresholdContract(ImpactLevel level, bool expected)
    {
        var input = new OverallImpactSummary
        {
            WorstImpactLevel = level,
            WorstRiskScore = 42,
            TriggeringEntity = new EntityRef(EntityType.Table, 1, "Orders"),
            TriggeringPathId = "path-1",
            RequiresApproval = false
        };

        var output = _policy.Evaluate(input);

        Assert.Equal(expected, output.RequiresApproval);
        Assert.Equal(input.WorstImpactLevel, output.WorstImpactLevel);
        Assert.Equal(input.WorstRiskScore, output.WorstRiskScore);
        Assert.Equal(input.TriggeringEntity, output.TriggeringEntity);
        Assert.Equal(input.TriggeringPathId, output.TriggeringPathId);
    }
}
