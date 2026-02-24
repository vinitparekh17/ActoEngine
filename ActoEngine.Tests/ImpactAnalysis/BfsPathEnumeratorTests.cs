using ActoEngine.WebApi.Features.ImpactAnalysis.Domain;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Graph;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Pathing;

namespace ActoEngine.Tests.ImpactAnalysis;

public class BfsPathEnumeratorTests
{
    [Fact]
    public void Enumerate_ReturnsPathsInBfsOrder()
    {
        var rows = new List<DependencyGraphRow>
        {
            Row("SP", 10, "TABLE", 1, "SELECT"),
            Row("VIEW", 20, "TABLE", 1, "INSERT"),
            Row("FUNCTION", 30, "SP", 10, "DELETE")
        };

        var graph = new GraphBuilder().Build(rows);
        var root = new EntityRef(EntityType.Table, 1);
        var enumerator = new BfsPathEnumerator(maxDepth: 3, maxPaths: 10);

        var result = enumerator.Enumerate(graph, root);

        Assert.Equal(3, result.Paths.Count);
        Assert.Equal("Table:1->Sp:10", result.Paths[0].PathId);
        Assert.Equal("Table:1->View:20", result.Paths[1].PathId);
        Assert.Equal("Table:1->Sp:10->Function:30", result.Paths[2].PathId);
        Assert.False(result.IsTruncated);
        Assert.Equal(2, result.MaxDepthReached);
    }

    [Fact]
    public void Enumerate_SkipsCyclesWithinPath()
    {
        var rows = new List<DependencyGraphRow>
        {
            Row("SP", 10, "TABLE", 1, "SELECT"),
            Row("TABLE", 1, "SP", 10, "SELECT")
        };

        var graph = new GraphBuilder().Build(rows);
        var root = new EntityRef(EntityType.Table, 1);
        var enumerator = new BfsPathEnumerator(maxDepth: 5, maxPaths: 10);

        var result = enumerator.Enumerate(graph, root);

        Assert.Single(result.Paths);
        Assert.Equal("Table:1->Sp:10", result.Paths[0].PathId);
    }

    [Fact]
    public void Enumerate_SetsTruncation_WhenDepthLimitPreventsExpansion()
    {
        var rows = new List<DependencyGraphRow>
        {
            Row("SP", 10, "TABLE", 1, "SELECT"),
            Row("FUNCTION", 30, "SP", 10, "SELECT")
        };

        var graph = new GraphBuilder().Build(rows);
        var root = new EntityRef(EntityType.Table, 1);
        var enumerator = new BfsPathEnumerator(maxDepth: 1, maxPaths: 10);

        var result = enumerator.Enumerate(graph, root);

        Assert.Single(result.Paths);
        Assert.True(result.IsTruncated);
        Assert.Equal("PATH_LIMIT_OR_DEPTH_LIMIT", result.TruncationReason);
        Assert.Equal(1, result.MaxDepthReached);
    }

    [Fact]
    public void Enumerate_SetsTruncation_WhenPathLimitReached()
    {
        var rows = new List<DependencyGraphRow>
        {
            Row("SP", 10, "TABLE", 1, "SELECT"),
            Row("VIEW", 20, "TABLE", 1, "SELECT")
        };

        var graph = new GraphBuilder().Build(rows);
        var root = new EntityRef(EntityType.Table, 1);
        var enumerator = new BfsPathEnumerator(maxDepth: 3, maxPaths: 1);

        var result = enumerator.Enumerate(graph, root);

        Assert.Single(result.Paths);
        Assert.True(result.IsTruncated);
    }

    [Fact]
    public void Enumerate_ThrowsIfRootIsNotPresentInGraph()
    {
        var graph = new GraphBuilder().Build(new List<DependencyGraphRow>
        {
            Row("SP", 10, "TABLE", 1, "SELECT")
        });

        var enumerator = new BfsPathEnumerator(maxDepth: 3, maxPaths: 10);

        Assert.Throws<InvalidOperationException>(() =>
            enumerator.Enumerate(graph, new EntityRef(EntityType.Table, 99)));
    }

    private static DependencyGraphRow Row(string sourceType, int sourceId, string targetType, int targetId, string dependencyType)
    {
        return new DependencyGraphRow
        {
            SourceEntityType = sourceType,
            SourceEntityId = sourceId,
            TargetEntityType = targetType,
            TargetEntityId = targetId,
            DependencyType = dependencyType,
            Depth = 1
        };
    }
}
