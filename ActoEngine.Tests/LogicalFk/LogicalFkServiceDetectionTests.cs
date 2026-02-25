using ActoEngine.Tests.Builders;
using ActoEngine.WebApi.Features.ImpactAnalysis;
using ActoEngine.WebApi.Features.LogicalFk;
using static ActoEngine.Tests.Builders.LogicalFkServiceBuilder;

namespace ActoEngine.Tests.LogicalFk;

public class LogicalFkServiceDetectionTests
{
    [Fact]
    public async Task T01_Service_NamingOnly_TypeMatch_Returns070()
    {
        List<DetectionColumnInfo> columns =
        [
            Col(10, 1, "Orders", "customer_id", "int"),
            Col(20, 2, "Customers", "id", "int", isPk: true, isUnique: true),
            Col(21, 2, "Customers", "name", "nvarchar")
        ];

        var service = LogicalFkServiceBuilder.Create().WithColumns(columns).Build();
        var candidates = await service.DetectCandidatesAsync(1);

        var candidate = Assert.Single(candidates);
        Assert.Equal(0.70m, candidate.ConfidenceScore);
        Assert.Equal(ConfidenceBand.Likely, candidate.ConfidenceBand);
        Assert.Equal(["NAME_CONVENTION"], candidate.DiscoveryMethods);
        Assert.False(candidate.IsAmbiguous);
    }

    [Fact]
    public async Task T02_Service_NamingOnly_TypeMismatch_Returns050()
    {
        List<DetectionColumnInfo> columns =
        [
            Col(10, 1, "Orders", "customer_id", "varchar"),
            Col(20, 2, "Customers", "id", "int", isPk: true, isUnique: true),
            Col(21, 2, "Customers", "name", "nvarchar")
        ];

        var service = LogicalFkServiceBuilder.Create().WithColumns(columns).Build();
        var candidates = await service.DetectCandidatesAsync(1);

        var candidate = Assert.Single(candidates);
        Assert.Equal(0.50m, candidate.ConfidenceScore);
        Assert.Equal(ConfidenceBand.Low, candidate.ConfidenceBand);
        Assert.Equal(["NAME_CONVENTION"], candidate.DiscoveryMethods);
    }

    [Fact]
    public async Task T03_Service_SpJoinSingle_TypeMatch_Returns075()
    {
        List<DetectionColumnInfo> columns =
        [
            Col(10, 1, "Orders", "cust_id", "int"),
            Col(20, 2, "Customers", "id", "int", isPk: true, isUnique: true)
        ];

        List<JoinConditionInfo> joinConditions =
        [
            new JoinConditionInfo
            {
                LeftTable = "Orders",
                LeftColumn = "cust_id",
                RightTable = "Customers",
                RightColumn = "id"
            }
        ];

        var service = LogicalFkServiceBuilder.Create()
            .WithColumns(columns)
            .WithProcedures(1)
            .WithJoinConditions(joinConditions)
            .Build();
        var candidates = await service.DetectCandidatesAsync(1);

        var candidate = Assert.Single(candidates);
        Assert.Equal(0.75m, candidate.ConfidenceScore);
        Assert.Equal(["SP_JOIN"], candidate.DiscoveryMethods);
    }

    [Fact]
    public async Task T04_Service_SpJoinFive_TypeMatch_Returns085()
    {
        List<DetectionColumnInfo> columns =
        [
            Col(10, 1, "Orders", "cust_id", "int"),
            Col(20, 2, "Customers", "id", "int", isPk: true, isUnique: true)
        ];

        List<JoinConditionInfo> joinConditions =
        [
            new JoinConditionInfo
            {
                LeftTable = "Orders",
                LeftColumn = "cust_id",
                RightTable = "Customers",
                RightColumn = "id"
            }
        ];

        var service = LogicalFkServiceBuilder.Create()
            .WithColumns(columns)
            .WithProcedures(5)
            .WithJoinConditions(joinConditions)
            .Build();
        var candidates = await service.DetectCandidatesAsync(1);

        var candidate = Assert.Single(candidates);
        Assert.Equal(0.85m, candidate.ConfidenceScore);
        Assert.Equal(5, candidate.MatchCount);
        Assert.Contains("SP_ONLY_CAP", candidate.Reason);
    }

    [Fact]
    public async Task T05_Service_SpJoinFive_TypeMismatch_Returns055()
    {
        List<DetectionColumnInfo> columns =
        [
            Col(10, 1, "Orders", "cust_id", "varchar"),
            Col(20, 2, "Customers", "id", "int", isPk: true, isUnique: true)
        ];

        List<JoinConditionInfo> joinConditions =
        [
            new JoinConditionInfo
            {
                LeftTable = "Orders",
                LeftColumn = "cust_id",
                RightTable = "Customers",
                RightColumn = "id"
            }
        ];

        var service = LogicalFkServiceBuilder.Create()
            .WithColumns(columns)
            .WithProcedures(5)
            .WithJoinConditions(joinConditions)
            .Build();
        var candidates = await service.DetectCandidatesAsync(1);

        var candidate = Assert.Single(candidates);
        Assert.Equal(0.55m, candidate.ConfidenceScore);
        Assert.Contains("TYPE_MISMATCH_CAP", candidate.Reason);
    }

    [Fact]
    public async Task T06_Service_Corroborated_TypeMatch_Returns095()
    {
        List<DetectionColumnInfo> columns =
        [
            Col(10, 1, "Orders", "customer_id", "int"),
            Col(20, 2, "Customers", "id", "int", isPk: true, isUnique: true),
            Col(21, 2, "Customers", "name", "nvarchar")
        ];

        List<JoinConditionInfo> joinConditions =
        [
            new JoinConditionInfo
            {
                LeftTable = "Orders",
                LeftColumn = "customer_id",
                RightTable = "Customers",
                RightColumn = "id"
            }
        ];

        var service = LogicalFkServiceBuilder.Create()
            .WithColumns(columns)
            .WithProcedures(1)
            .WithJoinConditions(joinConditions)
            .Build();
        var candidates = await service.DetectCandidatesAsync(1);

        var candidate = Assert.Single(candidates);
        Assert.Equal(0.95m, candidate.ConfidenceScore);
        Assert.Equal(ConfidenceBand.HighlyConfident, candidate.ConfidenceBand);
        Assert.Equal(2, candidate.DiscoveryMethods.Count);
        Assert.DoesNotContain("TYPE_MISMATCH_CAP", candidate.Reason);
    }

    [Fact]
    public async Task T07_Service_Corroborated_TypeMismatch_Returns075()
    {
        List<DetectionColumnInfo> columns =
        [
            Col(10, 1, "Orders", "customer_id", "varchar"),
            Col(20, 2, "Customers", "id", "int", isPk: true, isUnique: true),
            Col(21, 2, "Customers", "name", "nvarchar")
        ];

        List<JoinConditionInfo> joinConditions =
        [
            new JoinConditionInfo
            {
                LeftTable = "Orders",
                LeftColumn = "customer_id",
                RightTable = "Customers",
                RightColumn = "id"
            }
        ];

        var service = LogicalFkServiceBuilder.Create()
            .WithColumns(columns)
            .WithProcedures(1)
            .WithJoinConditions(joinConditions)
            .Build();
        var candidates = await service.DetectCandidatesAsync(1);

        var candidate = Assert.Single(candidates);
        Assert.Equal(0.75m, candidate.ConfidenceScore);
        Assert.Equal(ConfidenceBand.Likely, candidate.ConfidenceBand);
        Assert.DoesNotContain("TYPE_MISMATCH_CAP", candidate.Reason);
    }

    [Fact]
    public async Task T08_Service_PkBeatsUnique_WhenTierIsHigher()
    {
        List<DetectionColumnInfo> columns =
        [
            Col(10, 1, "Orders", "customer_id", "int"),
            Col(20, 2, "Customers", "id", "int", isPk: true, isUnique: true),
            Col(21, 2, "Customers", "legacy_id", "int", isUnique: true)
        ];

        var service = LogicalFkServiceBuilder.Create().WithColumns(columns).Build();
        var candidates = await service.DetectCandidatesAsync(1);

        var candidate = Assert.Single(candidates);
        Assert.Equal("id", candidate.TargetColumnName);
        Assert.False(candidate.IsAmbiguous);
    }

    [Fact]
    public async Task T09_Service_TwoUniqueTies_AreReturnedAsAmbiguous()
    {
        List<DetectionColumnInfo> columns =
        [
            Col(10, 1, "Orders", "customer_id", "int"),
            Col(20, 2, "Customers", "legacy_id", "int", isUnique: true),
            Col(21, 2, "Customers", "external_id", "int", isUnique: true)
        ];

        var service = LogicalFkServiceBuilder.Create().WithColumns(columns).Build();
        var candidates = await service.DetectCandidatesAsync(1);

        Assert.Equal(2, candidates.Count);
        Assert.All(candidates, c => Assert.True(c.IsAmbiguous));
        Assert.Contains(candidates, c => c.TargetColumnName == "legacy_id");
        Assert.Contains(candidates, c => c.TargetColumnName == "external_id");
    }

    [Fact]
    public async Task Corroboration_Disambiguates_TiedNamingCandidates()
    {
        List<DetectionColumnInfo> columns =
        [
            Col(10, 1, "Orders", "customer_id", "int"),
            Col(20, 2, "Customers", "legacy_id", "int", isUnique: true),
            Col(21, 2, "Customers", "external_id", "int", isUnique: true)
        ];

        List<JoinConditionInfo> joinConditions =
        [
            new JoinConditionInfo
            {
                LeftTable = "Orders",
                LeftColumn = "customer_id",
                RightTable = "Customers",
                RightColumn = "legacy_id"
            }
        ];

        var service = LogicalFkServiceBuilder.Create()
            .WithColumns(columns)
            .WithProcedures(1)
            .WithJoinConditions(joinConditions)
            .Build();
        var candidates = await service.DetectCandidatesAsync(1);

        var candidate = Assert.Single(candidates);
        Assert.Equal("legacy_id", candidate.TargetColumnName);
        Assert.False(candidate.IsAmbiguous);
        Assert.Equal(2, candidate.DiscoveryMethods.Count);
    }

    [Fact]
    public async Task SpParserFailure_FallsBackToNamingOnly()
    {
        List<DetectionColumnInfo> columns =
        [
            Col(10, 1, "Orders", "customer_id", "int"),
            Col(20, 2, "Customers", "id", "int", isPk: true, isUnique: true)
        ];

        var service = LogicalFkServiceBuilder.Create()
            .WithColumns(columns)
            .WithProcedures(1)
            .ThrowOnJoinExtraction()
            .Build();
        var candidates = await service.DetectCandidatesAsync(1);

        var candidate = Assert.Single(candidates);
        Assert.Equal(0.70m, candidate.ConfidenceScore);
        Assert.Equal(["NAME_CONVENTION"], candidate.DiscoveryMethods);
    }

    [Fact]
    public async Task SelfJoinEvidence_IsIgnored()
    {
        List<DetectionColumnInfo> columns =
        [
            Col(10, 1, "Orders", "order_ref_id", "int"),
            Col(11, 1, "Orders", "id", "int", isPk: true, isUnique: true)
        ];

        List<JoinConditionInfo> joinConditions =
        [
            new JoinConditionInfo
            {
                LeftTable = "Orders",
                LeftColumn = "order_ref_id",
                RightTable = "Orders",
                RightColumn = "id"
            }
        ];

        var service = LogicalFkServiceBuilder.Create()
            .WithColumns(columns)
            .WithProcedures(1)
            .WithJoinConditions(joinConditions)
            .Build();
        var candidates = await service.DetectCandidatesAsync(1);

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task BracketedAndSchemaQualifiedNames_AreResolvedForSpJoin()
    {
        List<DetectionColumnInfo> columns =
        [
            Col(10, 1, "Orders", "cust_id", "int"),
            Col(20, 2, "Customers", "id", "int", isPk: true, isUnique: true)
        ];

        List<JoinConditionInfo> joinConditions =
        [
            new JoinConditionInfo
            {
                LeftTable = "[dbo].[Orders]",
                LeftColumn = "[cust_id]",
                RightTable = "[dbo].[Customers]",
                RightColumn = "[id]"
            }
        ];

        var service = LogicalFkServiceBuilder.Create()
            .WithColumns(columns)
            .WithProcedures(1)
            .WithJoinConditions(joinConditions)
            .Build();
        var candidates = await service.DetectCandidatesAsync(1);

        var candidate = Assert.Single(candidates);
        Assert.Equal("Orders", candidate.SourceTableName);
        Assert.Equal("Customers", candidate.TargetTableName);
        Assert.Equal(0.75m, candidate.ConfidenceScore);
    }

}
