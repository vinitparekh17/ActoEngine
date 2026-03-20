using ActoEngine.WebApi.Api.ApiModels;
using ActoEngine.WebApi.Features.Patcher;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ActoEngine.Tests.Patcher;

public class MappingsTests
{
    [Fact]
    public void BuildMergeSql_ContainsCandidateGuard()
    {
        var detections = new List<MappingDetectionRequest>
        {
            new()
            {
                DomainName = "Reports",
                Page = "AutoVsPlanner",
                StoredProcedure = "REPORT_AUTO_VS_PLANNER",
                Source = "extension",
                Confidence = 0.92
            }
        };

        var (sql, _) = PageMappingRepository.BuildMergeSql(11, detections);
        Assert.Contains("WHEN MATCHED AND target.Status = @StatusCandidate THEN", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildGetMappingsSql_WithApprovedStatus_AddsStatusFilter()
    {
        var (sql, parameters) = PageMappingRepository.BuildGetMappingsSql(99, "approved", null, null);

        Assert.Contains("AND Status = @Status", sql, StringComparison.Ordinal);
        Assert.Equal("approved", parameters.Get<string>("Status"));
    }

    [Fact]
    public async Task DeleteCandidateMapping_WhenStatusIsNotCandidate_ReturnsBadRequest()
    {
        var repo = Substitute.For<IPageMappingRepository>();
        var logger = Substitute.For<ILogger<MappingsController>>();
        var controller = new MappingsController(repo, logger);

        repo.GetByIdAsync(7, 21, Arg.Any<CancellationToken>())
            .Returns(new PageMappingDto
            {
                MappingId = 21,
                ProjectId = 7,
                DomainName = "Sales",
                PageName = "Dashboard",
                StoredProcedure = "sp_sales_dashboard",
                Source = "manual",
                Status = "approved",
                MappingType = "shared",
                CreatedAt = DateTime.UtcNow
            });

        var action = await controller.DeleteCandidateMapping(7, 21, CancellationToken.None);

        var result = Assert.IsType<BadRequestObjectResult>(action.Result);
        var payload = Assert.IsType<ApiResponse<object>>(result.Value);
        Assert.False(payload.Status);
        Assert.Equal("Only candidate mappings can be deleted.", payload.Message);
    }

    [Fact]
    public void BuildMergeSql_WithDuplicateKeys_KeepsHighestConfidence()
    {
        var detections = new List<MappingDetectionRequest>
        {
            new() { DomainName = "Reports", Page = "Dashboard", StoredProcedure = "sp_test", Source = "extension", Confidence = 0.8 },
            new() { DomainName = "Reports", Page = "Dashboard", StoredProcedure = "sp_test", Source = "extension", Confidence = 0.95 },
            new() { DomainName = "Reports", Page = "Dashboard", StoredProcedure = "sp_other", Source = "extension", Confidence = 0.7 }
        };

        var unique = PageMappingRepository.DeduplicateDetections(detections);

        Assert.Equal(2, unique.Count);

        var (sql, parameters) = PageMappingRepository.BuildMergeSql(1, unique);

        // Only 2 parameter sets (indices 0 and 1)
        Assert.Equal(0.95, parameters.Get<double?>("Confidence0"));
        Assert.Equal(0.7, parameters.Get<double?>("Confidence1"));
    }

    [Fact]
    public async Task UpsertMappingDetections_WithDuplicates_ReturnsCorrectCounts()
    {
        var repo = Substitute.For<IPageMappingRepository>();
        var logger = Substitute.For<ILogger<MappingsController>>();
        var controller = new MappingsController(repo, logger);

        var detections = new List<MappingDetectionRequest>
        {
            new() { DomainName = "Sales", Page = "Index", StoredProcedure = "sp_get", Source = "extension", Confidence = 0.8 },
            new() { DomainName = "Sales", Page = "Index", StoredProcedure = "sp_get", Source = "extension", Confidence = 0.9 }
        };

        repo.UpsertDetectionsAsync(Arg.Any<int>(), Arg.Any<List<MappingDetectionRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new MappingUpsertResult(2, 1));

        var action = await controller.UpsertMappingDetections(1, detections, CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(action.Result);
        var payload = Assert.IsType<ApiResponse<object>>(result.Value);
        Assert.True(payload.Status);
        Assert.Contains("1 unique detection (", payload.Message);
    }
}
