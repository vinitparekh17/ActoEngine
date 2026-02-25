using ActoEngine.WebApi.Features.ImpactAnalysis.Domain;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Aggregation;

namespace ActoEngine.Tests.ImpactAnalysis;

public class ImpactAggregatorTests
{
    private readonly ImpactAggregator _aggregator = new();

    [Fact]
    public void Aggregate_ReturnsNoneForEmptyInput()
    {
        var result = _aggregator.Aggregate(new List<DependencyPath>());

        Assert.Equal(ImpactLevel.None, result.OverallImpact.WorstImpactLevel);
        Assert.Equal(0, result.OverallImpact.WorstRiskScore);
        Assert.Empty(result.EntityImpacts);
    }

    [Fact]
    public void Aggregate_GroupsByTerminalEntity_AndChoosesDominantPath()
    {
        var root = new EntityRef(EntityType.Table, 1, "Orders");
        var entityA = new EntityRef(EntityType.Sp, 10, "sp_A");
        var entityB = new EntityRef(EntityType.View, 20, "vw_B");

        var scoredPaths = new List<DependencyPath>
        {
            ScoredPath("p1", root, entityA, 20, ImpactLevel.Medium, DependencyType.Select),
            ScoredPath("p2", root, entityA, 40, ImpactLevel.High, DependencyType.Update),
            ScoredPath("p3", root, entityB, 30, ImpactLevel.High, DependencyType.Insert)
        };

        var result = _aggregator.Aggregate(scoredPaths);

        Assert.Equal(2, result.EntityImpacts.Count);

        var impactA = Assert.Single(result.EntityImpacts, e => e.Entity.Equals(entityA));
        Assert.Equal(ImpactLevel.High, impactA.WorstCaseImpactLevel);
        Assert.Equal(40, impactA.WorstCaseRiskScore);
        Assert.Equal(60, impactA.CumulativeRiskScore);
        Assert.Equal("p2", impactA.DominantPathId);

        Assert.Equal(entityA, result.OverallImpact.TriggeringEntity);
        Assert.Equal("p2", result.OverallImpact.TriggeringPathId);
    }

    [Fact]
    public void Aggregate_ReturnsNoneWhenAllPathsHaveNoNodes()
    {
        var placeholder = new EntityRef(EntityType.Table, -1, "placeholder");
        var invalidPath = new DependencyPath
        {
            PathId = "invalid",
            Nodes = new List<EntityRef>(),
            Edges = new List<DependencyType>(),
            Depth = 1,
            MaxDependencyType = DependencyType.Select,
            MaxCriticalityLevel = 1,
            RiskScore = 10,
            ImpactLevel = ImpactLevel.Low,
            DominantEntity = placeholder,
            DominantDependencyType = DependencyType.Select
        };

        var result = _aggregator.Aggregate(new List<DependencyPath> { invalidPath });

        Assert.Equal(ImpactLevel.None, result.OverallImpact.WorstImpactLevel);
        Assert.Equal(0, result.OverallImpact.WorstRiskScore);
        Assert.Empty(result.EntityImpacts);
    }

    private static DependencyPath ScoredPath(
        string pathId,
        EntityRef root,
        EntityRef terminal,
        int score,
        ImpactLevel level,
        DependencyType dependencyType)
    {
        return new DependencyPath
        {
            PathId = pathId,
            Nodes = new List<EntityRef> { root, terminal },
            Edges = new List<DependencyType> { dependencyType },
            Depth = 1,
            MaxDependencyType = dependencyType,
            MaxCriticalityLevel = 3,
            RiskScore = score,
            ImpactLevel = level,
            DominantEntity = terminal,
            DominantDependencyType = dependencyType
        };
    }
}
