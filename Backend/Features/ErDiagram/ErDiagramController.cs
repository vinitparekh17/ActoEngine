using ActoEngine.WebApi.Api.ApiModels;
using ActoEngine.WebApi.Api.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActoEngine.WebApi.Features.ErDiagram;


// ============================================
// ER DIAGRAM CONTROLLER
// ============================================

[ApiController]
[Authorize]
[Route("api/er-diagram")]
public class ErDiagramController(
    IErDiagramService erDiagramService,
    ILogger<ErDiagramController> logger) : ControllerBase
{
    /// <summary>
    /// Get ER diagram data for a neighborhood around a focus table (2-hop radius).
    /// Returns tables as nodes and physical + logical FKs as edges.
    /// </summary>
    [HttpGet("projects/{projectId}/neighborhood/{tableId}")]
    [RequirePermission("Schema:Read")]
    [ProducesResponseType(typeof(ApiResponse<ErDiagramResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNeighborhood(int projectId, int tableId, [FromQuery] int hops = 2, CancellationToken cancellationToken = default)
    {
        try
        {
            // Cap hops at 3 for performance
            hops = Math.Clamp(hops, 1, 3);

            var result = await erDiagramService.GetNeighborhoodAsync(projectId, tableId, hops, cancellationToken);
            if (result == null)
            {
                return NotFound(ApiResponse<object>.Failure($"Table {tableId} not found in project {projectId}"));
            }

            return Ok(ApiResponse<ErDiagramResponse>.Success(
                result,
                $"ER diagram with {result.Nodes.Count} tables and {result.Edges.Count} relationships"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error building ER diagram for table {TableId} in project {ProjectId}", tableId, projectId);
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred while building the ER diagram"));
        }
    }
}
