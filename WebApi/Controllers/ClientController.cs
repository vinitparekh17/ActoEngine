using Microsoft.AspNetCore.Mvc;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.ClientService;

namespace ActoEngine.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientController(IClientService clientService) : ControllerBase
    {
        private readonly IClientService _clientService = clientService;

        [HttpPost]
        public async Task<IActionResult> CreateClient([FromBody] CreateClientRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", [.. ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))]));

            var userId = HttpContext.Items["UserId"] as int?;
            if (userId == null)
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

            var response = await _clientService.CreateClientAsync(request, userId.Value);
            return Ok(ApiResponse<ClientResponse>.Success(response, "Client created successfully"));
        }

        [HttpGet("{clientId}")]
        public async Task<IActionResult> GetClient(int clientId)
        {
            var client = await _clientService.GetClientByIdAsync(clientId);
            if (client == null)
                return NotFound(ApiResponse<object>.Failure("Client not found"));

            var response = new ClientResponse
            {
                ClientId = client.ClientId,
                ClientName = client.ClientName,
                ProjectId = client.ProjectId,
                IsActive = client.IsActive,
                CreatedAt = client.CreatedAt,
                CreatedBy = client.CreatedBy,
                UpdatedAt = client.UpdatedAt,
                UpdatedBy = client.UpdatedBy
            };

            return Ok(ApiResponse<ClientResponse>.Success(response, "Client retrieved successfully"));
        }

        [HttpGet]
        public async Task<IActionResult> GetAllClients()
        {
            var clients = await _clientService.GetAllClientsAsync();
            var responses = clients.Select(client => new ClientResponse
            {
                ClientId = client.ClientId,
                ClientName = client.ClientName,
                ProjectId = client.ProjectId,
                IsActive = client.IsActive,
                CreatedAt = client.CreatedAt,
                CreatedBy = client.CreatedBy,
                UpdatedAt = client.UpdatedAt,
                UpdatedBy = client.UpdatedBy
            });

            return Ok(ApiResponse<IEnumerable<ClientResponse>>.Success(responses, "Clients retrieved successfully"));
        }

        [HttpGet("project/{projectId}")]
        public async Task<IActionResult> GetClientsByProject(int projectId)
        {
            var clients = await _clientService.GetClientsByProjectAsync(projectId);
            var responses = clients.Select(client => new ClientResponse
            {
                ClientId = client.ClientId,
                ClientName = client.ClientName,
                ProjectId = client.ProjectId,
                IsActive = client.IsActive,
                CreatedAt = client.CreatedAt,
                CreatedBy = client.CreatedBy,
                UpdatedAt = client.UpdatedAt,
                UpdatedBy = client.UpdatedBy
            });

            return Ok(ApiResponse<IEnumerable<ClientResponse>>.Success(responses, "Clients retrieved successfully"));
        }

        [HttpPut("{clientId}")]
        public async Task<IActionResult> UpdateClient(int clientId, [FromBody] Client client)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Failure("Invalid request data", ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()));

            var userId = HttpContext.Items["UserId"] as int?;
            if (userId == null)
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

            client.ClientId = clientId;
            var success = await _clientService.UpdateClientAsync(clientId, client, userId.Value);
            if (!success)
                return NotFound(ApiResponse<object>.Failure("Client not found or could not be updated"));

            return Ok(ApiResponse<object>.Success(new { }, "Client updated successfully"));
        }

        [HttpDelete("{clientId}/project/{projectId}")]
        public async Task<IActionResult> DeleteClient(int clientId, int projectId)
        {
            var userId = HttpContext.Items["UserId"] as int?;
            if (userId == null)
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));

            var success = await _clientService.DeleteClientAsync(clientId, projectId, userId.Value);
            if (!success)
                return NotFound(ApiResponse<object>.Failure("Client not found or could not be deleted"));

            return Ok(ApiResponse<object>.Success(new { }, "Client deleted successfully"));
        }
    }
}