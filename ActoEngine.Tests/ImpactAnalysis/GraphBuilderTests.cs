using ActoEngine.WebApi.Features.ImpactAnalysis.Domain;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Graph;

namespace ActoEngine.Tests.ImpactAnalysis;

public class GraphBuilderTests
{
    private readonly GraphBuilder _builder = new();

    [Fact]
    public void Build_CreatesDirectedEdgesFromDependencyToDependent()
    {
        var rows = new List<DependencyGraphRow>
        {
            new()
            {
                SourceEntityType = "SP",
                SourceEntityId = 10,
                TargetEntityType = "TABLE",
                TargetEntityId = 1,
                DependencyType = "SELECT",
                Depth = 1,
                SourceEntityName = "sp_GetOrders",
                TargetEntityName = "Orders",
                SourceCriticalityLevel = 4
            }
        };

        var graph = _builder.Build(rows);
        var root = new EntityRef(EntityType.Table, 1, "Orders");

        Assert.True(graph.Contains(root));
        var edge = Assert.Single(graph.GetOutgoingEdges(root));
        Assert.Equal(DependencyType.Select, edge.DependencyType);
        Assert.Equal(EntityType.Sp, edge.To.Type);
        Assert.Equal(10, edge.To.Id);
    }

    [Fact]
    public void Build_NormalizesCriticality_AndPrefersExplicitValue()
    {
        var rows = new List<DependencyGraphRow>
        {
            new()
            {
                SourceEntityType = "SP",
                SourceEntityId = 10,
                TargetEntityType = "TABLE",
                TargetEntityId = 1,
                DependencyType = "SELECT",
                Depth = 1,
                SourceCriticalityLevel = null
            },
            new()
            {
                SourceEntityType = "SP",
                SourceEntityId = 10,
                TargetEntityType = "TABLE",
                TargetEntityId = 2,
                DependencyType = "UPDATE",
                Depth = 1,
                SourceCriticalityLevel = 8
            }
        };

        var graph = _builder.Build(rows);
        var node = graph.GetNode(new EntityRef(EntityType.Sp, 10));

        Assert.NotNull(node);
        Assert.Equal(5, node!.CriticalityLevel);
    }

    [Fact]
    public void Build_ParsesLogicalFkDependencyTypeFromUnderscoreToken()
    {
        var rows = new List<DependencyGraphRow>
        {
            new()
            {
                SourceEntityType = "TABLE",
                SourceEntityId = 2,
                TargetEntityType = "TABLE",
                TargetEntityId = 1,
                DependencyType = "LOGICAL_FK",
                Depth = 1
            }
        };

        var graph = _builder.Build(rows);
        var root = new EntityRef(EntityType.Table, 1);

        var edge = Assert.Single(graph.GetOutgoingEdges(root));
        Assert.Equal(DependencyType.LogicalFk, edge.DependencyType);
    }

    [Fact]
    public void Build_UsesUnknownDependencyTypeWhenTokenIsUnrecognized()
    {
        var rows = new List<DependencyGraphRow>
        {
            new()
            {
                SourceEntityType = "TABLE",
                SourceEntityId = 2,
                TargetEntityType = "TABLE",
                TargetEntityId = 1,
                DependencyType = "SOMETHING_NEW",
                Depth = 1
            }
        };

        var graph = _builder.Build(rows);
        var root = new EntityRef(EntityType.Table, 1);

        var edge = Assert.Single(graph.GetOutgoingEdges(root));
        Assert.Equal(DependencyType.Unknown, edge.DependencyType);
    }

    [Fact]
    public void Build_ThrowsForUnknownEntityType()
    {
        var rows = new List<DependencyGraphRow>
        {
            new()
            {
                SourceEntityType = "JOBRUN",
                SourceEntityId = 2,
                TargetEntityType = "TABLE",
                TargetEntityId = 1,
                DependencyType = "SELECT",
                Depth = 1
            }
        };

        Assert.Throws<InvalidOperationException>(() => _builder.Build(rows));
    }
}
