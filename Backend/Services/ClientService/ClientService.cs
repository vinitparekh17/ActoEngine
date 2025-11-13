using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;

namespace ActoEngine.WebApi.Services.ClientService
{
    public interface IClientService
    {
        Task<Client?> GetClientByIdAsync(int clientId, int projectId);
        Task<Client?> GetClientByNameAsync(string clientName, int projectId);
        Task<IEnumerable<Client>> GetAllClientsAsync();
        Task<IEnumerable<Client>> GetClientsByProjectAsync(int projectId);
        Task<ClientResponse> CreateClientAsync(CreateClientRequest request, int userId);
        Task<bool> UpdateClientAsync(int clientId, Client client, int userId);
        Task<bool> DeleteClientAsync(int clientId, int projectId, int userId);
    }

    public class ClientService(IClientRepository clientRepository, ILogger<ClientService> logger) : IClientService
    {
        private readonly IClientRepository _clientRepository = clientRepository;
        private readonly ILogger<ClientService> _logger = logger;

        public async Task<Client?> GetClientByIdAsync(int clientId, int projectId)
        {
            try
            {
                return await _clientRepository.GetByIdAsync(clientId, projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving client with ID {ClientId} for project {ProjectId}", clientId, projectId);
                throw;
            }
        }

        public async Task<Client?> GetClientByNameAsync(string clientName, int projectId)
        {
            try
            {
                return await _clientRepository.GetByNameAsync(clientName, projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving client with name {ClientName} for project {ProjectId}", clientName, projectId);
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

        public async Task<IEnumerable<Client>> GetClientsByProjectAsync(int projectId)
        {
            try
            {
                return await _clientRepository.GetAllByProjectAsync(projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving clients for project {ProjectId}", projectId);
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
                    ProjectId = request.ProjectId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                };

                var clientId = await _clientRepository.CreateAsync(client);

                return new ClientResponse
                {
                    ClientId = clientId,
                    ClientName = client.ClientName,
                    ProjectId = client.ProjectId,
                    IsActive = client.IsActive,
                    CreatedAt = client.CreatedAt,
                    CreatedBy = client.CreatedBy
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating client {ClientName} for project {ProjectId}", request.ClientName, request.ProjectId);
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

        public async Task<bool> DeleteClientAsync(int clientId, int projectId, int userId)
        {
            try
            {
                return await _clientRepository.DeleteAsync(clientId, projectId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting client {ClientId} for project {ProjectId}", clientId, projectId);
                throw;
            }
        }
    }
}