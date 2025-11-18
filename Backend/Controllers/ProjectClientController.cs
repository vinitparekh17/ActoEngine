using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ActoEngine.WebApi.Extensions;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.ProjectClientService;

namespace ActoEngine.WebApi.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class ProjectClientController(IProjectClientService projectClientService) : ControllerBase
    {
        private readonly IProjectClientService _projectClientService = projectClientService;

        /// <summary>
        /// Link a client to a project
        /// </summary>
        [HttpPost("link")]
        public async Task<IActionResult> LinkClientToProject([FromBody] LinkClientToProjectRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));

            var userId = HttpContext.GetUserId();
            if (userId == null)
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

            try
            {
                var result = await _projectClientService.LinkClientToProjectAsync(request.ProjectId, request.ClientId, userId.Value);
                return Ok(ApiResponse<ProjectClientDetailResponse>.Success(result, "Client linked to project successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Failure(ex.Message));
            }
        }

        /// <summary>
        /// Unlink a client from a project
        /// </summary>
        [HttpDelete("project/{projectId}/client/{clientId}")]
        public async Task<IActionResult> UnlinkClientFromProject(int projectId, int clientId)
        {
            var userId = HttpContext.GetUserId();
            if (userId == null)
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

            var result = await _projectClientService.UnlinkClientFromProjectAsync(projectId, clientId, userId.Value);
            if (!result)
                return NotFound(ApiResponse<object>.Failure("Client-project link not found or already inactive"));

            return Ok(ApiResponse<object>.Success(new { }, "Client unlinked from project successfully"));
        }

        /// <summary>
        /// Link multiple clients to a single project
        /// </summary>
        [HttpPost("link/multiple-clients")]
        public async Task<IActionResult> LinkMultipleClientsToProject([FromBody] LinkMultipleClientsRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));

            var userId = HttpContext.GetUserId();
            if (userId == null)
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

            try
            {
                var results = await _projectClientService.LinkMultipleClientsToProjectAsync(request, userId.Value);
                return Ok(ApiResponse<IEnumerable<ProjectClientDetailResponse>>.Success(results, $"Linked {results.Count()} clients to project successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Failure(ex.Message));
            }
        }

        /// <summary>
        /// Link a single client to multiple projects
        /// </summary>
        [HttpPost("link/multiple-projects")]
        public async Task<IActionResult> LinkClientToMultipleProjects([FromBody] LinkClientToMultipleProjectsRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));

            var userId = HttpContext.GetUserId();
            if (userId == null)
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

            try
            {
                var results = await _projectClientService.LinkClientToMultipleProjectsAsync(request, userId.Value);
                return Ok(ApiResponse<IEnumerable<ProjectClientDetailResponse>>.Success(results, $"Linked client to {results.Count()} projects successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Failure(ex.Message));
            }
        }

        /// <summary>
        /// Get all clients linked to a project
        /// </summary>
        [HttpGet("project/{projectId}/clients")]
        public async Task<IActionResult> GetClientsByProject(int projectId)
        {
            var clients = await _projectClientService.GetClientsByProjectAsync(projectId);
            return Ok(ApiResponse<IEnumerable<ProjectClientDetailResponse>>.Success(clients, "Clients retrieved successfully"));
        }

        /// <summary>
        /// Get all projects linked to a client
        /// </summary>
        [HttpGet("client/{clientId}/projects")]
        public async Task<IActionResult> GetProjectsByClient(int clientId)
        {
            var projects = await _projectClientService.GetProjectsByClientAsync(clientId);
            return Ok(ApiResponse<IEnumerable<ProjectClientDetailResponse>>.Success(projects, "Projects retrieved successfully"));
        }

        /// <summary>
        /// Check if a client is linked to a project
        /// </summary>
        [HttpGet("is-linked")]
        public async Task<IActionResult> IsLinked([FromQuery] int projectId, [FromQuery] int clientId)
        {
            var isLinked = await _projectClientService.IsLinkedAsync(projectId, clientId);
            return Ok(ApiResponse<bool>.Success(isLinked, isLinked ? "Client is linked to project" : "Client is not linked to project"));
        }
    }
}
