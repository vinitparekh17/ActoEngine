using ActoEngine.WebApi.Features.ImpactAnalysis.Domain;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Scoring;

namespace ActoEngine.Tests.ImpactAnalysis;

public class PathRiskEvaluatorV1Tests
{
    private readonly PathRiskEvaluatorV1 _evaluator = new();

    [Fact]
    public void Evaluate_ComputesExpectedScore_AndImpactLevel()
    {
        var path = CreatePath(
            pathId: "p1",
            maxDependencyType: DependencyType.Delete,
            maxCriticality: 5,
            depth: 1);

        var scored = _evaluator.Evaluate(path, ChangeType.Modify);

        Assert.Equal(100, scored.RiskScore);
        Assert.Equal(ImpactLevel.Critical, scored.ImpactLevel);
        Assert.Equal(DependencyType.Delete, scored.DominantDependencyType);
    }

    [Fact]
    public void Evaluate_UsesExplicitLogicalFkWeight()
    {
        var path = CreatePath(
            pathId: "p2",
            maxDependencyType: DependencyType.LogicalFk,
            maxCriticality: 1,
            depth: 1);

        var scored = _evaluator.Evaluate(path, ChangeType.Create);

        Assert.Equal(6, scored.RiskScore);
        Assert.Equal(ImpactLevel.Low, scored.ImpactLevel);
    }

    [Fact]
    public void Evaluate_AppliesDepthDecayFloor()
    {
        var path = CreatePath(
            pathId: "p3",
            maxDependencyType: DependencyType.Delete,
            maxCriticality: 1,
            depth: 20);

        var scored = _evaluator.Evaluate(path, ChangeType.Create);

        // 10 * 1 * 1 * floor(0.2) = 2
        Assert.Equal(2, scored.RiskScore);
    }

    [Fact]
    public void Evaluate_ThrowsWhenPathHasNoNodes()
    {
        var emptyNodePath = new DependencyPath
        {
            PathId = "empty",
            Nodes = new List<EntityRef>(),
            Edges = new List<DependencyType>(),
            Depth = 1,
            MaxDependencyType = DependencyType.Select,
            MaxCriticalityLevel = 1,
            RiskScore = 0,
            ImpactLevel = ImpactLevel.None,
            DominantEntity = new EntityRef(EntityType.Table, -1),
            DominantDependencyType = DependencyType.Select
        };

        Assert.Throws<ArgumentException>(() => _evaluator.Evaluate(emptyNodePath, ChangeType.Modify));
    }

    [Fact]
    public void PolicySnapshot_ContainsStableVersionAndLogicalFkWeight()
    {
        var snapshot = _evaluator.PolicySnapshot;

        Assert.Equal("v1.0", snapshot["Version"]);

        var dependencyWeights = Assert.IsType<Dictionary<string, int>>(snapshot["DependencyWeights"]);
        Assert.Equal(6, dependencyWeights["LogicalFk"]);

        var multipliers = Assert.IsType<Dictionary<string, int>>(snapshot["ChangeTypeMultipliers"]);
        Assert.Equal(3, multipliers["Delete"]);
    }

    private static DependencyPath CreatePath(string pathId, DependencyType maxDependencyType, int maxCriticality, int depth)
    {
        var root = new EntityRef(EntityType.Table, 1, "Orders");
        var terminal = new EntityRef(EntityType.Sp, 10, "sp_WriteOrders");

        return new DependencyPath
        {
            PathId = pathId,
            Nodes = new List<EntityRef> { root, terminal },
            Edges = new List<DependencyType> { maxDependencyType },
            Depth = depth,
            MaxDependencyType = maxDependencyType,
            MaxCriticalityLevel = maxCriticality,
            RiskScore = 0,
            ImpactLevel = ImpactLevel.None,
            DominantEntity = terminal,
            DominantDependencyType = maxDependencyType
        };
    }
}
