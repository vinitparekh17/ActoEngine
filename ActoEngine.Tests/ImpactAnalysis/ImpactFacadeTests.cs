using ActoEngine.WebApi.Features.ImpactAnalysis;
using ActoEngine.WebApi.Features.ImpactAnalysis.Domain;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Contracts;
using NSubstitute;

namespace ActoEngine.Tests.ImpactAnalysis;

public class ImpactFacadeTests
{
    [Fact]
    public async Task AnalyzeAsync_ReturnsFastPath_WhenNoDependenciesExist()
    {
        var dependencyRepository = Substitute.For<IDependencyRepository>();
        dependencyRepository.GetDownstreamDependentsAsync(1, "TABLE", 1, Arg.Any<CancellationToken>())
            .Returns(new List<DependencyGraphRow>());

        var graphBuilder = Substitute.For<IGraphBuilder>();
        var pathEnumerator = Substitute.For<IPathEnumerator>();
        var riskEvaluator = Substitute.For<IPathRiskEvaluator>();
        riskEvaluator.Version.Returns("v-test");
        riskEvaluator.PolicySnapshot.Returns(new Dictionary<string, object> { ["Version"] = "v-test" });

        var impactAggregator = Substitute.For<IImpactAggregator>();
        var approvalPolicy = Substitute.For<IApprovalPolicy>();

        var facade = new ImpactFacade(
            dependencyRepository,
            graphBuilder,
            pathEnumerator,
            riskEvaluator,
            impactAggregator,
            approvalPolicy);

        var root = new EntityRef(EntityType.Table, 1, "Orders");
        var result = await facade.AnalyzeAsync(1, root, ChangeType.Modify);

        Assert.Equal(root, result.RootEntity);
        Assert.Equal(0, result.TotalPaths);
        Assert.Equal(0, result.TotalEntities);
        Assert.False(result.IsTruncated);
        Assert.Equal(ImpactLevel.None, result.OverallImpact.WorstImpactLevel);

        graphBuilder.DidNotReceiveWithAnyArgs().Build(default!);
        pathEnumerator.DidNotReceiveWithAnyArgs().Enumerate(default!, default!);
        impactAggregator.DidNotReceiveWithAnyArgs().Aggregate(default!);
    }

    [Fact]
    public async Task AnalyzeAsync_CallsPipelineInCorrectOrder()
    {
        var (facade, mocks) = CreatePipelineFixture();

        await facade.AnalyzeAsync(1, new EntityRef(EntityType.Table, 1), ChangeType.Modify);

        await mocks.DependencyRepo.Received(1)
            .GetDownstreamDependentsAsync(1, "TABLE", 1, Arg.Any<CancellationToken>());
        mocks.GraphBuilder.Received(1).Build(mocks.DependencyRows);
        mocks.PathEnumerator.Received(1).Enumerate(mocks.Graph, Arg.Is<EntityRef>(e => e.Type == EntityType.Table && e.Id == 1));
        mocks.RiskEvaluator.Received(1).Evaluate(mocks.UnscoredPath, ChangeType.Modify);
        mocks.ImpactAggregator.Received(1).Aggregate(Arg.Any<IReadOnlyList<DependencyPath>>());
        mocks.ApprovalPolicy.Received(1).Evaluate(Arg.Any<OverallImpactSummary>());
    }

    [Fact]
    public async Task AnalyzeAsync_PropagatesTruncationMetadata()
    {
        var (facade, _) = CreatePipelineFixture();

        var result = await facade.AnalyzeAsync(1, new EntityRef(EntityType.Table, 1), ChangeType.Modify);

        Assert.True(result.IsTruncated);
        Assert.Equal("PATH_LIMIT_OR_DEPTH_LIMIT", result.TruncationReason);
        Assert.Equal(1, result.MaxDepthReached);
    }

    [Fact]
    public async Task AnalyzeAsync_AppliesApprovalPolicy()
    {
        var (facade, _) = CreatePipelineFixture();

        var result = await facade.AnalyzeAsync(1, new EntityRef(EntityType.Table, 1), ChangeType.Modify);

        Assert.True(result.OverallImpact.RequiresApproval);
        Assert.Equal(ImpactLevel.High, result.OverallImpact.WorstImpactLevel);
    }

    [Fact]
    public async Task AnalyzeAsync_ResolvesRootEntityName_FromGraph()
    {
        var (facade, _) = CreatePipelineFixture();

        var result = await facade.AnalyzeAsync(1, new EntityRef(EntityType.Table, 1), ChangeType.Modify);

        Assert.Equal("Orders", result.RootEntity.Name);
        Assert.Equal(1, result.TotalPaths);
        Assert.Equal(1, result.TotalEntities);
    }

    #region Pipeline Fixture

    private record PipelineMocks(
        IDependencyRepository DependencyRepo,
        IGraphBuilder GraphBuilder,
        IPathEnumerator PathEnumerator,
        IPathRiskEvaluator RiskEvaluator,
        IImpactAggregator ImpactAggregator,
        IApprovalPolicy ApprovalPolicy,
        List<DependencyGraphRow> DependencyRows,
        ImpactGraph Graph,
        DependencyPath UnscoredPath);

    private static (ImpactFacade Facade, PipelineMocks Mocks) CreatePipelineFixture()
    {
        var dependencyRows = new List<DependencyGraphRow>
        {
            new()
            {
                SourceEntityType = "SP",
                SourceEntityId = 10,
                TargetEntityType = "TABLE",
                TargetEntityId = 1,
                DependencyType = "SELECT",
                Depth = 1
            }
        };

        var dependencyRepository = Substitute.For<IDependencyRepository>();
        dependencyRepository.GetDownstreamDependentsAsync(1, "TABLE", 1, Arg.Any<CancellationToken>())
            .Returns(dependencyRows);

        var rootInGraph = new EntityRef(EntityType.Table, 1, "Orders");
        var dependent = new EntityRef(EntityType.Sp, 10, "sp_sync");

        var graph = BuildGraph(rootInGraph, dependent);

        var graphBuilder = Substitute.For<IGraphBuilder>();
        graphBuilder.Build(dependencyRows).Returns(graph);

        var unscoredPath = new DependencyPath
        {
            PathId = "Table:1->Sp:10",
            Nodes = new List<EntityRef> { rootInGraph, dependent },
            Edges = new List<DependencyType> { DependencyType.Select },
            Depth = 1,
            MaxDependencyType = DependencyType.Select,
            MaxCriticalityLevel = 3,
            RiskScore = 0,
            ImpactLevel = ImpactLevel.None,
            DominantEntity = dependent,
            DominantDependencyType = DependencyType.Select
        };

        var pathEnumerator = Substitute.For<IPathEnumerator>();
        pathEnumerator.Enumerate(graph, Arg.Any<EntityRef>())
            .Returns(new PathEnumerationResult
            {
                Paths = new List<DependencyPath> { unscoredPath },
                IsTruncated = true,
                TruncationReason = "PATH_LIMIT_OR_DEPTH_LIMIT",
                MaxDepthReached = 1
            });

        var scoredPath = new DependencyPath
        {
            PathId = unscoredPath.PathId,
            Nodes = unscoredPath.Nodes,
            Edges = unscoredPath.Edges,
            Depth = unscoredPath.Depth,
            MaxDependencyType = unscoredPath.MaxDependencyType,
            MaxCriticalityLevel = unscoredPath.MaxCriticalityLevel,
            RiskScore = 40,
            ImpactLevel = ImpactLevel.High,
            DominantEntity = unscoredPath.DominantEntity,
            DominantDependencyType = DependencyType.Select
        };

        var riskEvaluator = Substitute.For<IPathRiskEvaluator>();
        riskEvaluator.Version.Returns("v1.0");
        riskEvaluator.PolicySnapshot.Returns(new Dictionary<string, object> { ["Version"] = "v1.0" });
        riskEvaluator.Evaluate(unscoredPath, ChangeType.Modify).Returns(scoredPath);

        var entityImpact = new EntityImpact
        {
            Entity = dependent,
            WorstCaseImpactLevel = ImpactLevel.High,
            WorstCaseRiskScore = 40,
            CumulativeRiskScore = 40,
            DominantPathId = scoredPath.PathId,
            Paths = new List<DependencyPath> { scoredPath }
        };

        var aggregatedOverall = new OverallImpactSummary
        {
            WorstImpactLevel = ImpactLevel.High,
            WorstRiskScore = 40,
            TriggeringEntity = dependent,
            TriggeringPathId = scoredPath.PathId,
            RequiresApproval = false
        };

        var impactAggregator = Substitute.For<IImpactAggregator>();
        impactAggregator.Aggregate(Arg.Any<IReadOnlyList<DependencyPath>>())
            .Returns(new ImpactAggregationResult
            {
                OverallImpact = aggregatedOverall,
                EntityImpacts = new List<EntityImpact> { entityImpact }
            });

        var approvalPolicy = Substitute.For<IApprovalPolicy>();
        approvalPolicy.Evaluate(aggregatedOverall)
            .Returns(new OverallImpactSummary
            {
                WorstImpactLevel = aggregatedOverall.WorstImpactLevel,
                WorstRiskScore = aggregatedOverall.WorstRiskScore,
                TriggeringEntity = aggregatedOverall.TriggeringEntity,
                TriggeringPathId = aggregatedOverall.TriggeringPathId,
                RequiresApproval = true
            });

        var facade = new ImpactFacade(
            dependencyRepository,
            graphBuilder,
            pathEnumerator,
            riskEvaluator,
            impactAggregator,
            approvalPolicy);

        var mocks = new PipelineMocks(
            dependencyRepository, graphBuilder, pathEnumerator,
            riskEvaluator, impactAggregator, approvalPolicy,
            dependencyRows, graph, unscoredPath);

        return (facade, mocks);
    }

    private static ImpactGraph BuildGraph(EntityRef root, EntityRef dependent)
    {
        var nodes = new Dictionary<EntityRef, GraphNode>
        {
            [root] = new GraphNode { Entity = root, CriticalityLevel = 3 },
            [dependent] = new GraphNode { Entity = dependent, CriticalityLevel = 3 }
        };

        var adjacency = new Dictionary<EntityRef, List<ActoEngine.WebApi.Features.ImpactAnalysis.Domain.GraphEdge>>
        {
            [root] = new List<ActoEngine.WebApi.Features.ImpactAnalysis.Domain.GraphEdge>
            {
                new() { From = root, To = dependent, DependencyType = DependencyType.Select }
            }
        };

        return new ImpactGraph(nodes, adjacency);
    }

    #endregion
}
