using ActoEngine.Tests.Builders;
using ActoEngine.WebApi.Features.LogicalFk;

namespace ActoEngine.Tests.LogicalFk;

public class RejectionMemoryTests
{
    [Fact]
    public async Task DetectCandidates_SuppressesRejectedFk()
    {
        var columns = BuildSimpleColumns();
        var rejectedKey = "1:10→2:20";

        var service = LogicalFkServiceBuilder.Create()
            .WithColumns(columns)
            .WithLogicalKeys(new HashSet<string> { rejectedKey })
            .Build();

        var candidates = await service.DetectCandidatesAsync(1);

        Assert.DoesNotContain(candidates, c => c.CanonicalKey == rejectedKey);
    }

    [Fact]
    public async Task DetectCandidates_ReturnsCandidateWhenNotRejected()
    {
        var columns = BuildSimpleColumns();

        var service = LogicalFkServiceBuilder.Create()
            .WithColumns(columns)
            .Build();

        var candidates = await service.DetectCandidatesAsync(1);

        Assert.Contains(candidates, c => c.SourceTableName == "Orders" && c.TargetTableName == "Customers");
    }

    [Fact]
    public async Task DetectCandidates_SuppressesPhysicalFkPair()
    {
        var columns = BuildSimpleColumns();
        var physicalKey = "1:10→2:20";

        var service = LogicalFkServiceBuilder.Create()
            .WithColumns(columns)
            .WithPhysicalKeys(new HashSet<string> { physicalKey })
            .Build();

        var candidates = await service.DetectCandidatesAsync(1);

        Assert.DoesNotContain(candidates, c => c.CanonicalKey == physicalKey);
    }

    private static List<DetectionColumnInfo> BuildSimpleColumns() =>
    [
        LogicalFkServiceBuilder.Col(10, 1, "Orders", "customer_id", "int"),
        LogicalFkServiceBuilder.Col(20, 2, "Customers", "id", "int", isPk: true, isUnique: true),
        LogicalFkServiceBuilder.Col(21, 2, "Customers", "Name", "nvarchar")
    ];
}
