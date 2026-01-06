using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Features.Projects;
using ActoEngine.WebApi.Repositories;
using ActoEngine.WebApi.Attributes;
using ActoEngine.WebApi.Extensions;
using System.Text;
using System.Text.Json;
using ActoEngine.WebApi.Features.Project.Dtos.Requests;
using ActoEngine.WebApi.Infrastructure.Security;
namespace ActoEngine.WebApi.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class ProjectController(IProjectService projectService, ILogger<ProjectController> logger) : ControllerBase
    {
        private readonly IProjectService _projectService = projectService;
        private readonly ILogger<ProjectController> _logger = logger;

        [HttpPost("verify")]
        [RequirePermission("Projects:Link")]
        [ProducesResponseType(typeof(ApiResponse<ConnectionResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> VerifyConnection([FromBody] VerifyConnectionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));
            }

            var connectionResponse = await _projectService.VerifyConnectionAsync(request);

            if (connectionResponse)
            {
                var successResponse = new ConnectionResponse { Message = "Connection successful", IsValid = true };
                return Ok(ApiResponse<ConnectionResponse>.Success(successResponse, "Database connection verified"));
            }

            var failureResponse = new ConnectionResponse { Message = "Connection failed" };
            return BadRequest(ApiResponse<ConnectionResponse>.Failure("Database connection failed"));
        }

        [HttpPost("link")]
        [RequirePermission("Projects:Link")]
        [ProducesResponseType(typeof(ApiResponse<ProjectResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> LinkProject([FromBody] LinkProjectRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));
            }

            var userId = HttpContext.GetUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
            }

            try
            {
                var response = await _projectService.LinkProjectAsync(request, userId.Value);
                return Ok(ApiResponse<ProjectResponse>.Success(response, "Project linked successfully"));
            }
            catch (Exception ex)
            {
                var redactedMessage = SecurityHelper.RedactConnectionString(ex.Message);
                return BadRequest(ApiResponse<object>.Failure($"Failed to link project: {redactedMessage}"));
            }
        }

        [HttpPost("resync")]
        [RequirePermission("Schema:Sync")]
        [ProducesResponseType(typeof(ApiResponse<ProjectResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ReSyncProject([FromBody] ReSyncProjectRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));
            }

            var userId = HttpContext.GetUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
            }

            try
            {
                var response = await _projectService.ReSyncProjectAsync(request, userId.Value);
                return Ok(ApiResponse<ProjectResponse>.Success(response, "Project re-sync started successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ApiResponse<object>.Failure(ex.Message));
            }
        }

        [HttpPost]
        [RequirePermission("Projects:Create")]
        [ProducesResponseType(typeof(ApiResponse<ProjectResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));
            }

            var userId = HttpContext.GetUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
            }

            try
            {
                var response = await _projectService.CreateProjectAsync(request, userId.Value);
                return Ok(ApiResponse<ProjectResponse>.Success(response, "Project created successfully"));
            }
            catch (Exception ex)
            {
                var redactedMessage = SecurityHelper.RedactConnectionString(ex.Message);
                return BadRequest(ApiResponse<object>.Failure($"Failed to create project: {redactedMessage}"));
            }
        }

        [HttpGet("{projectId}/sync-status")]
        [RequirePermission("Projects:Read")]
        public async Task<IActionResult> GetSyncStatus(int projectId)
        {
            var status = await _projectService.GetSyncStatusAsync(projectId);
            if (status == null)
            {
                return NotFound(ApiResponse<SyncStatusResponse>.Failure("Project not found"));
            }

            if (status.Status == null)
            {
                return NotFound(ApiResponse<SyncStatusResponse>.Failure("Sync status not available"));
            }

            var response = new SyncStatusResponse
            {
                ProjectId = projectId,
                Status = status.Status,
                SyncProgress = status.SyncProgress,
                LastSyncAttempt = status.LastSyncAttempt
            };
            return Ok(ApiResponse<SyncStatusResponse>.Success(response, "Sync status retrieved successfully"));
        }

        /// <summary>
        /// Server-Sent Events endpoint for real-time sync status updates
        /// </summary>
        /// <param name="projectId">The project ID to stream sync status for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>SSE stream of sync status updates</returns>
        [HttpGet("{projectId}/sync-status/stream")]
        [RequirePermission("Projects:Read")]
        [Produces("text/event-stream")]
        public async Task StreamSyncStatus(int projectId, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting SSE stream for project {ProjectId}", projectId);

            // Set SSE headers
            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");
            Response.Headers.Append("X-Accel-Buffering", "no"); // Disable nginx buffering

            try
            {
                string? lastStatus = null;
                int? lastProgress = null;

                while (!cancellationToken.IsCancellationRequested)
                {
                    // Fetch current sync status
                    var syncStatus = await _projectService.GetSyncStatusAsync(projectId);

                    if (syncStatus != null)
                    {
                        // Only send update if status or progress changed
                        bool hasChanged = syncStatus.Status != lastStatus ||
                                        syncStatus.SyncProgress != lastProgress;

                        if (hasChanged)
                        {
                            lastStatus = syncStatus.Status;
                            lastProgress = syncStatus.SyncProgress;

                            // Serialize status to JSON with local time
                            var statusData = new
                            {
                                projectId,
                                status = syncStatus.Status,
                                progress = syncStatus.SyncProgress,
                                lastSyncAttempt = syncStatus.LastSyncAttempt,
                                timestamp = DateTime.UtcNow
                            };

                            var jsonData = JsonSerializer.Serialize(statusData);

                            // Send SSE formatted message
                            var sseMessage = $"data: {jsonData}\n\n";
                            await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(sseMessage), cancellationToken);
                            await Response.Body.FlushAsync(cancellationToken);

                            _logger.LogDebug("Sent SSE update for project {ProjectId}: {Status} ({Progress}%)",
                                projectId, syncStatus.Status, syncStatus.SyncProgress);

                            // Close stream if sync is completed or failed
                            if (syncStatus.Status == "Completed" ||
                                syncStatus.Status?.StartsWith("Failed") == true)
                            {
                                _logger.LogInformation("Sync finished for project {ProjectId} with status: {Status}",
                                    projectId, syncStatus.Status);
                                break;
                            }
                        }
                    }

                    // Wait 1 second before next check
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("SSE stream cancelled for project {ProjectId}", projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SSE stream for project {ProjectId}", projectId);

                // Send error message
                var errorData = new { error = "Stream error", message = ex.Message };
                var jsonError = JsonSerializer.Serialize(errorData);
                var sseError = $"data: {jsonError}\n\n";

                try
                {
                    await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(sseError), cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
                catch
                {
                    // Ignore errors during error reporting
                }
            }
        }

        [HttpGet("{projectId}")]
        [RequirePermission("Projects:Read")]
        public async Task<IActionResult> GetProject(int projectId)
        {

            var project = await _projectService.GetProjectByIdAsync(projectId);
            if (project == null)
            {
                return NotFound(ApiResponse<object>.Failure("Project not found"));
            }

            return Ok(ApiResponse<PublicProjectDto>.Success(project, "Project retrieved successfully"));
        }

        [HttpGet]
        [RequirePermission("Projects:Read")]
        public async Task<IActionResult> GetAllProjects()
        {
            var projects = await _projectService.GetAllProjectsAsync();
            // Sync status is now included in the query - no N+1 problem
            return Ok(ApiResponse<IEnumerable<PublicProjectDto>>.Success(projects, "Projects retrieved successfully"));
        }

        [HttpGet("{projectId}/stats")]
        [RequirePermission("Projects:Read")]
        public async Task<IActionResult> GetProjectStats(int projectId)
        {
            var stats = await _projectService.GetProjectStatsAsync(projectId);
            if (stats == null)
            {
                return NotFound(ApiResponse<object>.Failure("Project not found"));
            }

            return Ok(ApiResponse<ProjectStatsResponse>.Success(stats, "Project stats retrieved successfully"));
        }

        [HttpPut("{projectId}")]
        [RequirePermission("Projects:Update")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateProject(int projectId, [FromBody] ActoEngine.WebApi.Models.Project project)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));
            }

            var userId = HttpContext.GetUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
            }

            var success = await _projectService.UpdateProjectAsync(projectId, project, userId.Value);
            if (!success)
            {
                return NotFound(ApiResponse<object>.Failure("Project not found or could not be updated"));
            }

            return Ok(ApiResponse<object>.Success(new { }, "Project updated successfully"));
        }

        [HttpDelete("{projectId}")]
        [RequirePermission("Projects:Delete")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteProject(int projectId)
        {
            var userId = HttpContext.GetUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
            }

            var success = await _projectService.DeleteProjectAsync(projectId, userId.Value);
            if (!success)
            {
                return NotFound(ApiResponse<object>.Failure("Project not found or could not be deleted"));
            }

            return Ok(ApiResponse<object>.Success(new { }, "Project deleted successfully"));
        }
    }
}