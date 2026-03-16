using ActoEngine.WebApi.Features.ImpactAnalysis;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ActoEngine.Tests.ImpactAnalysis;

public class DependencyAnalysisServiceTests
{
    private readonly ILogger<DependencyAnalysisService> _logger = Substitute.For<ILogger<DependencyAnalysisService>>();

    [Fact]
    public void ExtractDependencies_IncludesProcedureTableAndColumnDependencies()
    {
        var service = new DependencyAnalysisService(_logger);

        var definition = """
            CREATE PROCEDURE [dbo].[sp_report]
            AS
            BEGIN
                SELECT o.CustomerId, c.Id
                FROM dbo.Orders o
                INNER JOIN dbo.Customers c ON o.CustomerId = c.Id;
                EXEC dbo.sp_child;
            END
            """;

        var dependencies = service.ExtractDependencies(definition, 100, "SP");

        Assert.Contains(dependencies, d => d.TargetType == "TABLE" && d.TargetName == "dbo.Orders");
        Assert.Contains(dependencies, d => d.TargetType == "TABLE" && d.TargetName == "dbo.Customers");
        Assert.Contains(dependencies, d => d.TargetType == "SP" && d.TargetName == "dbo.sp_child");
        Assert.Contains(dependencies, d => d.TargetType == "COLUMN" && d.TargetName == "dbo.Orders.CustomerId");
        Assert.Contains(dependencies, d => d.TargetType == "COLUMN" && d.TargetName == "dbo.Customers.Id");
    }
}
