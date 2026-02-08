using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using System.Collections.Generic;
using System.Text.Json;
using ActoEngine.WebApi.Infrastructure.Security;
using ActoEngine.WebApi.Features.Schema;
using ActoEngine.WebApi.Shared.Extensions;
using ActoEngine.WebApi.Api.ApiModels;
using ActoEngine.WebApi.Features.Projects.Dtos.Requests;
using ActoEngine.WebApi.Features.Projects.Dtos.Responses;
using ActoEngine.WebApi.Api.Attributes;
namespace ActoEngine.WebApi.Features.Projects
{
    [ApiController]
    [Authorize]
    [Route("api/projects")]
    public class ProjectController(
        IProjectService projectService, 
        ILogger<ProjectController> logger,
        SseConnectionManager sseConnectionManager) : ControllerBase
    {
        private readonly IProjectService _projectService = projectService;
        private readonly ILogger<ProjectController> _logger = logger;
        private readonly SseConnectionManager _sseConnectionManager = sseConnectionManager;

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

            if (connectionResponse.IsValid)
            {
                return Ok(ApiResponse<ConnectionResponse>.Success(connectionResponse, "Database connection verified"));
            }

            // Return the detailed error response from the service with full ConnectionResponse data
            // This includes ErrorCode and HelpLink properties for the client
            return BadRequest(ApiResponse<ConnectionResponse>.Failure(
                connectionResponse.Message, 
                connectionResponse.Errors,
                connectionResponse));
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
        /// <param name="ticket">Optional one-time ticket for authentication (alternative to cookie)</param>
        /// <param name="sseTicketService">SSE ticket service (injected)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>SSE stream of sync status updates</returns>
        [HttpGet("{projectId}/sync-status/stream")]
        [Produces("text/event-stream")]
        public async Task StreamSyncStatus(
            int projectId, 
            [FromQuery] string? ticket,
            [FromServices] ISseTicketService sseTicketService,
            CancellationToken cancellationToken)
        {
            int? userId = HttpContext.GetUserId();

            // If no cookie authentication, try ticket authentication
            if (userId == null && !string.IsNullOrWhiteSpace(ticket))
            {
                var ticketMetadata = await sseTicketService.ValidateAndConsumeTicketAsync(ticket);
                if (ticketMetadata != null && ticketMetadata.ProjectId == projectId)
                {
                    userId = ticketMetadata.UserId;
                    _logger.LogInformation("SSE stream authenticated via ticket for user {UserId}, project {ProjectId}", 
                        userId, projectId);
                }
                else
                {
                    _logger.LogWarning("SSE stream ticket validation failed for project {ProjectId}", projectId);
                }
            }

            if (userId == null)
            {
                Response.StatusCode = StatusCodes.Status401Unauthorized;
                _logger.LogWarning("SSE stream authentication failed for project {ProjectId} - no valid cookie or ticket", projectId);
                return;
            }

            _logger.LogInformation("Starting SSE stream for project {ProjectId}, user {UserId}", projectId, userId);

            // Register this connection and get a linked cancellation token
            // This will cancel any existing connection for the same user/project (handles multiple tabs)
            var connectionHandle = _sseConnectionManager.RegisterConnection(userId.Value, projectId, cancellationToken);
            var managedToken = connectionHandle.Token;

            // Set SSE headers
            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");
            Response.Headers.Append("X-Accel-Buffering", "no"); // Disable nginx buffering

            try
            {
                string? lastStatus = null;
                int? lastProgress = null;

                while (!managedToken.IsCancellationRequested)
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
                            await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(sseMessage), managedToken);
                            await Response.Body.FlushAsync(managedToken);

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
                    await Task.Delay(1000, managedToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("SSE stream cancelled for project {ProjectId}, user {UserId}", projectId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SSE stream for project {ProjectId}, user {UserId}", projectId, userId);

                // Send error message
                var errorData = new { error = "Stream error", message = ex.Message };
                var jsonError = JsonSerializer.Serialize(errorData);
                var sseError = $"data: {jsonError}\n\n";

                try
                {
                    await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(sseError), CancellationToken.None);
                    await Response.Body.FlushAsync(CancellationToken.None);
                }
                catch
                {
                    // Ignore errors during error reporting
                }
            }
            finally
            {
                // Unregister connection when it closes
                _sseConnectionManager.UnregisterConnection(connectionHandle);
            }
        }

        /// <summary>
        /// Exchange a valid session token for a short-lived one-time SSE ticket
        /// </summary>
        /// <param name="projectId">The project ID for the SSE stream</param>
        /// <param name="sseTicketService">SSE ticket service (injected)</param>
        /// <returns>Ticket and expiry information</returns>
        [HttpPost("{projectId}/sync-status/ticket")]
        [RequirePermission("Projects:Read")]
        [ProducesResponseType(typeof(ApiResponse<SseTicketResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSseTicket(int projectId, [FromServices] ISseTicketService sseTicketService)
        {
            var userId = HttpContext.GetUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
            }

            // Verify user has access to this project
            var project = await _projectService.GetProjectByIdAsync(projectId);
            if (project == null)
            {
                return NotFound(ApiResponse<object>.Failure("Project not found"));
            }

            // Generate one-time ticket
            var ticket = await sseTicketService.GenerateTicketAsync(userId.Value, projectId);

            var response = new SseTicketResponse
            {
                Ticket = ticket,
                ExpiresIn = 30 // seconds
            };

            return Ok(ApiResponse<SseTicketResponse>.Success(response, "SSE ticket generated"));
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

        [HttpGet("{projectId}/users")]
        [RequirePermission("Projects:Read")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ProjectMemberDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetProjectUsers(int projectId, CancellationToken cancellationToken)
        {
            var members = await _projectService.GetProjectMembersAsync(projectId, cancellationToken);
            return Ok(ApiResponse<IEnumerable<ProjectMemberDto>>.Success(members, "Project members retrieved successfully"));
        }
        [HttpPost("{projectId}/members")]
        [RequirePermission("Projects:Update")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> AddProjectMember(int projectId, [FromBody] int userId)
        {
            var adminId = HttpContext.GetUserId();
            if (adminId == null)
            {
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
            }

            await _projectService.AddProjectMemberAsync(projectId, userId, adminId.Value);
            return Ok(ApiResponse<object>.Success(new { }, "Member added successfully"));
        }

        [HttpDelete("{projectId}/members/{userId}")]
        [RequirePermission("Projects:Update")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> RemoveProjectMember(int projectId, int userId)
        {
            await _projectService.RemoveProjectMemberAsync(projectId, userId);
            return Ok(ApiResponse<object>.Success(new { }, "Member removed successfully"));
        }

        [HttpGet("user/{userId}")]
        [RequirePermission("Projects:Read")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<int>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUserProjectMemberships(int userId)
        {
            // IDOR Prevention: Only allow users to query their own memberships
            // unless they have elevated permissions
            var callerId = HttpContext.GetUserId();
            if (callerId == null)
            {
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
            }

            // If caller is not querying their own memberships, they need Projects:ReadAll permission
            if (callerId.Value != userId)
            {
                // Check if user has elevated permission to read all project memberships
                // For now, we simply forbid cross-user queries unless you're querying yourself
                // TODO: Add "Projects:ReadAll" permission check here if needed for admin users
                return Forbid();
            }

            var projectIds = await _projectService.GetUserProjectMembershipsAsync(userId);
            return Ok(ApiResponse<IEnumerable<int>>.Success(projectIds, "User project memberships retrieved successfully"));
        }

        [HttpPut("{projectId}")]
        [RequirePermission("Projects:Update")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateProject(int projectId, [FromBody] Project project)
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
