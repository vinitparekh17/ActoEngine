using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;

namespace ActoEngine.WebApi.Services.ClientService
{
    public interface IClientService
    {
        /// <summary>
        /// Retrieve a client by its identifier.
        /// </summary>
        /// <returns>The client with the specified identifier, or null if no matching client exists.</returns>
        Task<Client?> GetClientByIdAsync(int clientId);
        /// <summary>
        /// Finds a client by its name.
        /// </summary>
        /// <param name="clientName">The client name to search for.</param>
        /// <returns>The <see cref="Client"/> that matches the given name, or <c>null</c> if no match is found.</returns>
        Task<Client?> GetClientByNameAsync(string clientName);
        /// <summary>
        /// Retrieves all clients.
        /// </summary>
        /// <returns>An enumerable of all Client entities; empty if no clients exist.</returns>
        Task<IEnumerable<Client>> GetAllClientsAsync();
        /// <summary>
        /// Creates a new client from the provided request and records the creating user.
        /// </summary>
        /// <param name="request">Data required to create the client (for example, the client's name and related settings).</param>
        /// <param name="userId">Identifier of the user performing the creation.</param>
        /// <returns>The created client's details including its identifier, name, active state, creation timestamp, and creator identifier.</returns>
        Task<ClientResponse> CreateClientAsync(CreateClientRequest request, int userId);
        /// <summary>
        /// Updates the client identified by <paramref name="clientId"/> using the provided client data and records who performed the update.
        /// </summary>
        /// <param name="clientId">The identifier of the client to update.</param>
        /// <param name="client">The client data to apply to the existing record.</param>
        /// <param name="userId">The identifier of the user performing the update (used for audit fields).</param>
        /// <returns>`true` if the client was successfully updated, `false` otherwise.</returns>
        Task<bool> UpdateClientAsync(int clientId, Client client, int userId);
        /// <summary>
        /// Deletes the client identified by <paramref name="clientId"/> and records the user who performed the deletion.
        /// </summary>
        /// <param name="clientId">The identifier of the client to delete.</param>
        /// <param name="userId">The identifier of the user performing the deletion.</param>
        /// <returns>`true` if the client was deleted, `false` otherwise.</returns>
        Task<bool> DeleteClientAsync(int clientId, int userId);
    }

    public class ClientService(IClientRepository clientRepository, ILogger<ClientService> logger) : IClientService
    {
        private readonly IClientRepository _clientRepository = clientRepository;
        private readonly ILogger<ClientService> _logger = logger;

        /// <summary>
        /// Retrieves a client by its identifier.
        /// </summary>
        /// <param name="clientId">The identifier of the client to retrieve.</param>
        /// <returns>The <see cref="Client"/> with the specified identifier, or <c>null</c> if not found.</returns>
        public async Task<Client?> GetClientByIdAsync(int clientId)
        {
            try
            {
                return await _clientRepository.GetByIdAsync(clientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving client with ID {ClientId}", clientId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a client by its name.
        /// </summary>
        /// <param name="clientName">The name of the client to retrieve.</param>
        /// <returns>The matching Client, or null if no client with the specified name exists.</returns>
        public async Task<Client?> GetClientByNameAsync(string clientName)
        {
            try
            {
                return await _clientRepository.GetByNameAsync(clientName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving client with name {ClientName}", clientName);
                throw;
            }
        }

        /// <summary>
        /// Retrieves all clients.
        /// </summary>
        /// <returns>An enumerable containing all clients; the sequence may be empty.</returns>
        public async Task<IEnumerable<Client>> GetAllClientsAsync()
        {
            try
            {
                return await _clientRepository.GetAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all clients");
                throw;
            }
        }

        /// <summary>
        /// Creates a new client record and returns a summary of the created client.
        /// </summary>
        /// <param name="request">Request containing data for the new client (expects ClientName).</param>
        /// <param name="userId">Identifier of the user creating the client.</param>
        /// <returns>A <see cref="ClientResponse"/> containing the created client's ID, name, active status, creation timestamp, and creator ID.</returns>
        public async Task<ClientResponse> CreateClientAsync(CreateClientRequest request, int userId)
        {
            try
            {
                var client = new Client
                {
                    ClientName = request.ClientName,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                };

                var clientId = await _clientRepository.CreateAsync(client);

                return new ClientResponse
                {
                    ClientId = clientId,
                    ClientName = client.ClientName,
                    IsActive = client.IsActive,
                    CreatedAt = client.CreatedAt,
                    CreatedBy = client.CreatedBy
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating client {ClientName}", request.ClientName);
                throw;
            }
        }

        /// <summary>
        /// Updates the specified client's audit fields and persists the changes.
        /// </summary>
        /// <param name="clientId">Identifier of the client to update.</param>
        /// <param name="client">Client entity containing the updated values.</param>
        /// <param name="userId">Identifier of the user performing the update.</param>
        /// <returns>`true` if the client was updated, `false` otherwise.</returns>
        /// <exception cref="Exception">Any exception thrown while updating is rethrown.</exception>
        public async Task<bool> UpdateClientAsync(int clientId, Client client, int userId)
        {
            try
            {
                client.UpdatedAt = DateTime.UtcNow;
                client.UpdatedBy = userId;

                return await _clientRepository.UpdateAsync(client);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating client {ClientId}", clientId);
                throw;
            }
        }

        /// <summary>
        /// Deletes a client by its identifier and records which user performed the deletion.
        /// </summary>
        /// <param name="clientId">Identifier of the client to delete.</param>
        /// <param name="userId">Identifier of the user performing the deletion.</param>
        /// <returns>True if the client was deleted, false otherwise.</returns>
        public async Task<bool> DeleteClientAsync(int clientId, int userId)
        {
            try
            {
                return await _clientRepository.DeleteAsync(clientId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting client {ClientId}", clientId);
                throw;
            }
        }
    }
}