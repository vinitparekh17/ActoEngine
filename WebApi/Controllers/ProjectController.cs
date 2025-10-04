using Microsoft.AspNetCore.Mvc;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.ProjectService;

namespace ActoEngine.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectController(IProjectService projectService) : ControllerBase
    {
        private readonly IProjectService _projectService = projectService;

        [HttpPost("verify")]
        public async Task<IActionResult> VerifyConnection([FromBody] VerifyConnectionRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));

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
        public async Task<IActionResult> LinkProject([FromBody] LinkProjectRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()));

            var userId = HttpContext.Items["UserId"] as int?;
            if (userId == null)
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
            var response = await _projectService.LinkProjectAsync(request, userId.Value);
            return Ok(ApiResponse<ProjectResponse>.Success(response, "Project linked successfully"));
        }

        [HttpPost]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()));

            var userId = HttpContext.Items["UserId"] as int?;
            if (userId == null)
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

            var response = await _projectService.CreateProjectAsync(request, userId.Value);
            return Ok(ApiResponse<ProjectResponse>.Success(response, "Project created successfully"));
        }

        [HttpGet("{projectId}/sync-status")]
        public async Task<ApiResponse<SyncStatusResponse>> GetSyncStatus(int projectId)
        {
            var status = await _projectService.GetSyncStatusAsync(projectId);
            if (status == null)
                return ApiResponse<SyncStatusResponse>.Failure("Project not found");
            if (status.Status == null)
                return ApiResponse<SyncStatusResponse>.Failure("Sync status not available");
            var response = new SyncStatusResponse
            {
                ProjectId = projectId,
                Status = status.Status,
                SyncProgress = status.SyncProgress,
                LastSyncAttempt = status.LastSyncAttempt
            };
            return ApiResponse<SyncStatusResponse>.Success(response, "Sync status retrieved successfully");
        }

        [HttpGet("{projectId}")]
        public async Task<IActionResult> GetProject(int projectId)
        {

            var project = await _projectService.GetProjectByIdAsync(projectId);
            if (project == null)
                return NotFound(ApiResponse<object>.Failure("Project not found"));

            return Ok(ApiResponse<Project>.Success(project, "Project retrieved successfully"));
        }

        [HttpGet]
        public async Task<IActionResult> GetAllProjects()
        {
            var userId = HttpContext.Items["UserId"] as int?;
            if (userId == null)
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

            var projects = await _projectService.GetAllProjectsAsync();
            return Ok(ApiResponse<IEnumerable<Project>>.Success(projects, "Projects retrieved successfully"));
        }

        [HttpPut("{projectId}")]
        public async Task<IActionResult> UpdateProject(int projectId, [FromBody] Project project)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()));

            var userId = HttpContext.Items["UserId"] as int?;
            if (userId == null)
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

            var success = await _projectService.UpdateProjectAsync(projectId, project, userId.Value);
            if (!success)
                return NotFound(ApiResponse<object>.Failure("Project not found or could not be updated"));

            return Ok(ApiResponse<object>.Success(new { }, "Project updated successfully"));
        }

        [HttpDelete("{projectId}")]
        public async Task<IActionResult> DeleteProject(int projectId)
        {
            var userId = HttpContext.Items["UserId"] as int?;
            if (userId == null)
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

            var success = await _projectService.DeleteProjectAsync(projectId, userId.Value);
            if (!success)
                return NotFound(ApiResponse<object>.Failure("Project not found or could not be deleted"));

            return Ok(ApiResponse<object>.Success(new { }, "Project deleted successfully"));
        }
    }
}