using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ActoEngine.WebApi.Api.ApiModels;
using ActoEngine.WebApi.Api.Attributes;
using ActoEngine.WebApi.Shared.Extensions;

namespace ActoEngine.WebApi.Features.LogicalFk;

/// <summary>
/// API endpoints for managing logical foreign keys
/// </summary>
[ApiController]
[Authorize]
[Route("api/logical-fks/{projectId}")]
public class LogicalFkController(
    ILogicalFkService logicalFkService,
    ILogger<LogicalFkController> logger) : ControllerBase
{
    /// <summary>
    /// List all logical FKs for a project, optionally filtered by status
    /// </summary>
    [HttpGet]
    [RequirePermission("Schema:Read")]
    [ProducesResponseType(typeof(ApiResponse<List<LogicalFkDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByProject(
        int projectId,
        [FromQuery] string? status = null)
    {
        try
        {
            var results = await logicalFkService.GetByProjectAsync(projectId, status);
            return Ok(ApiResponse<List<LogicalFkDto>>.Success(results, "Logical FKs retrieved"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting logical FKs for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred while retrieving logical FKs"));
        }
    }

    /// <summary>
    /// Get logical FKs related to a specific table (as source or target)
    /// </summary>
    [HttpGet("table/{tableId:int}")]
    [RequirePermission("Schema:Read")]
    [ProducesResponseType(typeof(ApiResponse<List<LogicalFkDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByTable(int projectId, int tableId)
    {
        try
        {
            var results = await logicalFkService.GetByTableAsync(projectId, tableId);
            return Ok(ApiResponse<List<LogicalFkDto>>.Success(results, "Table logical FKs retrieved"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting logical FKs for table {TableId}", tableId);
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred"));
        }
    }
    /// <summary>
    /// Get physical FKs related to a specific table
    /// </summary>
    [HttpGet("table/{tableId:int}/physical")]
    [RequirePermission("Schema:Read")]
    [ProducesResponseType(typeof(ApiResponse<List<PhysicalFkDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPhysicalByTable(int projectId, int tableId)
    {
        try
        {
            var results = await logicalFkService.GetPhysicalFksByTableAsync(projectId, tableId);
            return Ok(ApiResponse<List<PhysicalFkDto>>.Success(results, "Table physical FKs retrieved"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting physical FKs for table {TableId}", tableId);
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred"));
        }
    }

    /// <summary>
    /// Run name convention detection and return candidates (without persisting)
    /// </summary>
    [HttpGet("detect-candidates")]
    [RequirePermission("Schema:Read")]
    [ProducesResponseType(typeof(ApiResponse<List<LogicalFkCandidate>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DetectCandidates(int projectId)
    {
        try
        {
            var candidates = await logicalFkService.DetectCandidatesAsync(projectId);
            return Ok(ApiResponse<List<LogicalFkCandidate>>.Success(
                candidates,
                $"Detected {candidates.Count} candidate(s)"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error detecting logical FK candidates for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred during detection"));
        }
    }

    /// <summary>
    /// Manually create a logical FK (auto-confirmed)
    /// </summary>
    [HttpPost]
    [RequirePermission("Schema:Update")]
    [ProducesResponseType(typeof(ApiResponse<LogicalFkDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        int projectId,
        [FromBody] CreateLogicalFkRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.Failure("Invalid request data",
                    [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));
            }

            var userId = HttpContext.GetUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
            }

            var result = await logicalFkService.CreateManualAsync(projectId, request, userId.Value);
            return StatusCode(201, ApiResponse<LogicalFkDto>.Success(result, "Logical FK created"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Failure(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<object>.Failure(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating logical FK for project {ProjectId}", projectId);
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred"));
        }
    }

    /// <summary>
    /// Confirm a suggested logical FK
    /// </summary>
    [HttpPut("{id:int}/confirm")]
    [RequirePermission("Schema:Update")]
    [ProducesResponseType(typeof(ApiResponse<LogicalFkDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Confirm(
        int id,
        [FromBody] UpdateLogicalFkStatusRequest? request = null)
    {
        try
        {
            var userId = HttpContext.GetUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
            }

            var result = await logicalFkService.ConfirmAsync(id, userId.Value, request?.Notes);
            return Ok(ApiResponse<LogicalFkDto>.Success(result, "Logical FK confirmed"));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Failure($"Logical FK {id} not found"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error confirming logical FK {LogicalFkId}", id);
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred"));
        }
    }

    /// <summary>
    /// Reject a suggested logical FK
    /// </summary>
    [HttpPut("{id:int}/reject")]
    [RequirePermission("Schema:Update")]
    [ProducesResponseType(typeof(ApiResponse<LogicalFkDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(
        int id,
        [FromBody] UpdateLogicalFkStatusRequest? request = null)
    {
        try
        {
            var userId = HttpContext.GetUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
            }

            var result = await logicalFkService.RejectAsync(id, userId.Value, request?.Notes);
            return Ok(ApiResponse<LogicalFkDto>.Success(result, "Logical FK rejected"));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Failure($"Logical FK {id} not found"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error rejecting logical FK {LogicalFkId}", id);
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred"));
        }
    }

    /// <summary>
    /// Delete a logical FK
    /// </summary>
    [HttpDelete("{id:int}")]
    [RequirePermission("Schema:Update")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await logicalFkService.DeleteAsync(id);
            return Ok(ApiResponse<object>.Success(new { }, "Logical FK deleted"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting logical FK {LogicalFkId}", id);
            return StatusCode(500, ApiResponse<object>.Failure("An error occurred"));
        }
    }
}
