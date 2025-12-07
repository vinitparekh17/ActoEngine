using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.ImpactService;
using ActoEngine.WebApi.Attributes;

namespace ActoEngine.WebApi.Controllers;

/// <summary>
/// API endpoints for impact analysis
/// </summary>
[ApiController]
[Authorize]
[Route("api/projects/{projectId}/impact")]
public class ImpactController(
    IImpactService impactService,
    ILogger<ImpactController> logger) : ControllerBase
{
    /// <summary>
    /// Analyze impact of changing an entity
    /// </summary>
    [HttpGet("{entityType}/{entityId}")]
    [RequirePermission("Contexts:Read")] // Re-using Contexts permission for now
    [ProducesResponseType(typeof(ImpactAnalysisResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetImpactAnalysis(
        int projectId,
        string entityType,
        int entityId,
        [FromQuery] string changeType = "MODIFY")
    {
        try
        {
            var analysis = await impactService.GetImpactAnalysisAsync(projectId, entityType, entityId, changeType);
            return Ok(ApiResponse<ImpactAnalysisResponse>.Success(analysis, "Impact analysis completed"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing impact for {EntityType} {EntityId}", entityType, entityId);
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred during impact analysis"));
        }
    }
}
