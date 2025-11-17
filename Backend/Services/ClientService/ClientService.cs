using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;

namespace ActoEngine.WebApi.Services.ClientService
{
    public interface IClientService
    {
        Task<Client?> GetClientByIdAsync(int clientId);
        Task<Client?> GetClientByNameAsync(string clientName);
        Task<IEnumerable<Client>> GetAllClientsAsync();
        Task<ClientResponse> CreateClientAsync(CreateClientRequest request, int userId);
        Task<bool> UpdateClientAsync(int clientId, Client client, int userId);
        Task<bool> DeleteClientAsync(int clientId, int userId);
    }

    public class ClientService(IClientRepository clientRepository, ILogger<ClientService> logger) : IClientService
    {
        private readonly IClientRepository _clientRepository = clientRepository;
        private readonly ILogger<ClientService> _logger = logger;

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