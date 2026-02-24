using ActoEngine.WebApi.Api.ApiModels;
using ActoEngine.WebApi.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActoEngine.WebApi.Features.ProjectClients
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class ProjectClientController(IProjectClientService projectClientService) : ControllerBase
    {
        private readonly IProjectClientService _projectClientService = projectClientService;

        /// <summary>
        /// Links a client to a project using the provided request data.
        /// </summary>
        /// <param name="request">Request containing ProjectId and ClientId to link.</param>
        /// <returns>Ok with ApiResponse&lt;ProjectClientDetailResponse&gt; on success; BadRequest with ApiResponse&lt;object&gt; for validation errors or invalid operation; Unauthorized with ApiResponse&lt;object&gt; if the user is not authenticated.</returns>
        [HttpPost("link")]
        public async Task<IActionResult> LinkClientToProject([FromBody] LinkClientToProjectRequest request)
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
                var result = await _projectClientService.LinkClientToProjectAsync(request.ProjectId, request.ClientId, userId.Value);
                return Ok(ApiResponse<ProjectClientDetailResponse>.Success(result, "Client linked to project successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Failure(ex.Message));
            }
        }

        /// <summary>
        /// Unlinks the specified client from the specified project on behalf of the authenticated user.
        /// </summary>
        /// <param name="projectId">The identifier of the project.</param>
        /// <param name="clientId">The identifier of the client to unlink.</param>
        /// <returns>
        /// An IActionResult containing an ApiResponse:
        /// - 200 OK with success message when the client was unlinked.
        /// - 401 Unauthorized when no authenticated user is present.
        /// - 404 NotFound when the client-project link does not exist or is already inactive.
        /// </returns>
        [HttpDelete("project/{projectId}/client/{clientId}")]
        public async Task<IActionResult> UnlinkClientFromProject(int projectId, int clientId)
        {
            var userId = HttpContext.GetUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
            }

            var result = await _projectClientService.UnlinkClientFromProjectAsync(projectId, clientId, userId.Value);
            if (!result)
            {
                return NotFound(ApiResponse<object>.Failure("Client-project link not found or already inactive"));
            }

            return Ok(ApiResponse<object>.Success(new { }, "Client unlinked from project successfully"));
        }

        /// <summary>
        /// Links multiple clients to a single project using the provided request data.
        /// </summary>
        /// <param name="request">Request containing the target project identifier and the client identifiers to link.</param>
        /// <returns>
        /// An IActionResult that is:
        /// - Ok with ApiResponse&lt;IEnumerable&lt;ProjectClientDetailResponse&gt;&gt; containing linked client details on success;
        /// - BadRequest with ApiResponse&lt;object&gt; when the request is invalid or an operation error occurs;
        /// - Unauthorized with ApiResponse&lt;object&gt; when the user is not authenticated.
        /// </returns>
        [HttpPost("link/multiple-clients")]
        public async Task<IActionResult> LinkMultipleClientsToProject([FromBody] LinkMultipleClientsRequest request)
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
                var results = await _projectClientService.LinkMultipleClientsToProjectAsync(request, userId.Value);
                return Ok(ApiResponse<IEnumerable<ProjectClientDetailResponse>>.Success(results, $"Linked {results.Count()} clients to project successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Failure(ex.Message));
            }
        }

        /// <summary>
        /// Links a single client to multiple projects for the authenticated user.
        /// </summary>
        /// <param name="request">Request containing the client identifier and the target project identifiers.</param>
        /// <returns>An <see cref="IActionResult"/> containing:
        ///  - 200 OK with <c>ApiResponse&lt;IEnumerable&lt;ProjectClientDetailResponse&gt;&gt;</c> on success;
        ///  - 400 Bad Request with <c>ApiResponse&lt;object&gt;.Failure</c> for invalid input or business errors;
        ///  - 401 Unauthorized with <c>ApiResponse&lt;object&gt;.Failure</c> if the user is not authenticated.</returns>
        [HttpPost("link/multiple-projects")]
        public async Task<IActionResult> LinkClientToMultipleProjects([FromBody] LinkClientToMultipleProjectsRequest request)
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
                var results = await _projectClientService.LinkClientToMultipleProjectsAsync(request, userId.Value);
                return Ok(ApiResponse<IEnumerable<ProjectClientDetailResponse>>.Success(results, $"Linked client to {results.Count()} projects successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Failure(ex.Message));
            }
        }

        /// <summary>
        /// Retrieves all clients linked to the specified project.
        /// </summary>
        /// <param name="projectId">The identifier of the project whose linked clients to retrieve.</param>
        /// <returns>An IActionResult containing an ApiResponse with the collection of linked ProjectClientDetailResponse objects.</returns>
        [HttpGet("project/{projectId}/clients")]
        public async Task<IActionResult> GetClientsByProject(int projectId)
        {
            var clients = await _projectClientService.GetClientsByProjectAsync(projectId);
            return Ok(ApiResponse<IEnumerable<ProjectClientDetailResponse>>.Success(clients, "Clients retrieved successfully"));
        }

        /// <summary>
        /// Retrieves all projects linked to the specified client.
        /// </summary>
        /// <param name="clientId">The identifier of the client whose linked projects to retrieve.</param>
        /// <returns>A collection of ProjectClientDetailResponse representing projects linked to the client.</returns>
        [HttpGet("client/{clientId}/projects")]
        public async Task<IActionResult> GetProjectsByClient(int clientId)
        {
            var projects = await _projectClientService.GetProjectsByClientAsync(clientId);
            return Ok(ApiResponse<IEnumerable<ProjectClientDetailResponse>>.Success(projects, "Projects retrieved successfully"));
        }

        /// <summary>
        /// Checks whether a client is linked to a project.
        /// </summary>
        /// <param name="projectId">ID of the project to check.</param>
        /// <param name="clientId">ID of the client to check.</param>
        /// <returns>`true` if the client is linked to the project, `false` otherwise.</returns>
        [HttpGet("is-linked")]
        public async Task<IActionResult> IsLinked([FromQuery] int projectId, [FromQuery] int clientId)
        {
            var isLinked = await _projectClientService.IsLinkedAsync(projectId, clientId);
            return Ok(ApiResponse<bool>.Success(isLinked, isLinked ? "Client is linked to project" : "Client is not linked to project"));
        }
    }
}
