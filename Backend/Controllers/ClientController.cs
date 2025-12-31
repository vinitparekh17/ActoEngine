using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ActoEngine.WebApi.Extensions;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Attributes;
using ActoEngine.WebApi.Services.ClientService;

namespace ActoEngine.WebApi.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class ClientController(IClientService clientService) : ControllerBase
    {
        private readonly IClientService _clientService = clientService;

        /// <summary>
        /// Creates a new client from the provided request and returns the created client.
        /// </summary>
        /// <param name="request">The details of the client to create. Must pass model validation.</param>
        /// <returns>
        /// An IActionResult containing:
        /// - 200 OK with the created <see cref="ClientResponse"/> on success;
        /// - 400 BadRequest with validation error messages if the request model is invalid;
        /// - 401 Unauthorized if the caller is not authenticated.
        /// </returns>
        [HttpPost]
        [RequirePermission("Clients:Create")]
        public async Task<IActionResult> CreateClient([FromBody] CreateClientRequest request)
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

            var response = await _clientService.CreateClientAsync(request, userId.Value);
            return Ok(ApiResponse<ClientResponse>.Success(response, "Client created successfully"));
        }

        /// <summary>
        /// Retrieves a client by its identifier and returns a standardized API response.
        /// </summary>
        /// <returns>An IActionResult containing an ApiResponse with the ClientResponse on success; `404 NotFound` ApiResponse if the client does not exist.</returns>
        [HttpGet("{clientId}")]
        [RequirePermission("Clients:Read")]
        public async Task<IActionResult> GetClient(int clientId)
        {
            var client = await _clientService.GetClientByIdAsync(clientId);
            if (client == null)
            {
                return NotFound(ApiResponse<object>.Failure("Client not found"));
            }

            var response = new ClientResponse
            {
                ClientId = client.ClientId,
                ClientName = client.ClientName,
                IsActive = client.IsActive,
                CreatedAt = client.CreatedAt,
                CreatedBy = client.CreatedBy,
                UpdatedAt = client.UpdatedAt,
                UpdatedBy = client.UpdatedBy
            };

            return Ok(ApiResponse<ClientResponse>.Success(response, "Client retrieved successfully"));
        }

        /// <summary>
        /// Retrieve all clients and return them as ClientResponse DTOs.
        /// </summary>
        /// <returns>An ApiResponse containing an IEnumerable&lt;ClientResponse&gt; with the retrieved clients and a success message.</returns>
        [HttpGet]
        [RequirePermission("Clients:Read")]
        public async Task<IActionResult> GetClients()
        {
            var clients = await _clientService.GetAllClientsAsync();
            var responses = clients.Select(client => new ClientResponse
            {
                ClientId = client.ClientId,
                ClientName = client.ClientName,
                IsActive = client.IsActive,
                CreatedAt = client.CreatedAt,
                CreatedBy = client.CreatedBy,
                UpdatedAt = client.UpdatedAt,
                UpdatedBy = client.UpdatedBy
            });

            return Ok(ApiResponse<IEnumerable<ClientResponse>>.Success(responses, "Clients retrieved successfully"));
        }

        /// <summary>
        /// Updates an existing client identified by <paramref name="clientId"/> with the provided payload.
        /// </summary>
        /// <param name="clientId">Identifier of the client to update.</param>
        /// <param name="request">The client data to apply; the method will set its ClientId to <paramref name="clientId"/>.</param>
        /// <returns>
        /// An <see cref="IActionResult"/> that is one of:
        /// - 400 BadRequest with a failure ApiResponse containing validation error messages;
        /// - 401 Unauthorized with a failure ApiResponse when the user is not authenticated;
        /// - 404 NotFound with a failure ApiResponse if the client does not exist or the update failed;
        /// - 200 OK with a success ApiResponse when the client is updated successfully.
        /// </returns>
        [HttpPut("{clientId}")]
        [RequirePermission("Clients:Update")]
        public async Task<IActionResult> UpdateClient(int clientId, [FromBody] UpdateClientRequest request)
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

            // Load existing client to preserve IsActive when not provided
            var existingClient = await _clientService.GetClientByIdAsync(clientId);
            if (existingClient == null)
            {
                return NotFound(ApiResponse<object>.Failure("Client not found"));
            }

            // Convert UpdateClientRequest to Client entity
            var client = new Client
            {
                ClientId = clientId,
                ClientName = request.ClientName,
                IsActive = request.IsActive ?? existingClient.IsActive
            };

            var success = await _clientService.UpdateClientAsync(clientId, client, userId.Value);
            if (!success)
            {
                return NotFound(ApiResponse<object>.Failure("Client not found or could not be updated"));
            }

            return Ok(ApiResponse<object>.Success(new { }, "Client updated successfully"));
        }

        /// <summary>
        /// Deletes the client identified by the given clientId on behalf of the authenticated user.
        /// </summary>
        /// <param name="clientId">The identifier of the client to delete.</param>
        /// <returns>
        /// An IActionResult representing the outcome:
        /// 200 OK with a success ApiResponse when the client was deleted,
        /// 401 Unauthorized with a failure ApiResponse if the user is not authenticated,
        /// 404 NotFound with a failure ApiResponse if the client was not found or could not be deleted.
        /// </returns>
        [HttpDelete("{clientId}")]
        [RequirePermission("Clients:Delete")]
        public async Task<IActionResult> DeleteClient(int clientId)
        {
            var userId = HttpContext.GetUserId();
            if (userId == null)
            {
                return Unauthorized(ApiResponse<object>.Failure("User not authenticated"));
            }

            var success = await _clientService.DeleteClientAsync(clientId, userId.Value);
            if (!success)
            {
                return NotFound(ApiResponse<object>.Failure("Client not found or could not be deleted"));
            }

            return Ok(ApiResponse<object>.Success(new { }, "Client deleted successfully"));
        }
    }
}