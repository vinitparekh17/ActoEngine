using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.Database;
using ActoEngine.WebApi.SqlQueries;

namespace ActoEngine.WebApi.Repositories;

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(int clientId, int projectId, CancellationToken cancellationToken = default);
    Task<Client?> GetByNameAsync(string clientName, int projectId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Client>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Client>> GetAllByProjectAsync(int projectId, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(int projectId, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(Client client, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Client client, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int clientId, int projectId, int userId, CancellationToken cancellationToken = default);
}

public class ClientRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<ClientRepository> logger)
    : BaseRepository(connectionFactory, logger), IClientRepository
{
    public async Task<Client?> GetByIdAsync(int clientId, int projectId, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await QueryFirstOrDefaultAsync<Client>(
                ClientSqlQueries.GetById,
                new { ClientId = clientId, ProjectId = projectId },
                cancellationToken);
            return client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving client with ID {ClientId} for project {ProjectId}", clientId, projectId);
            throw;
        }
    }

    public async Task<Client?> GetByNameAsync(string clientName, int projectId, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await QueryFirstOrDefaultAsync<Client>(
                ClientSqlQueries.GetByName,
                new { ClientName = clientName, ProjectId = projectId },
                cancellationToken);

            return client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving client with name {ClientName} for project {ProjectId}", clientName, projectId);
            throw;
        }
    }

    public async Task<IEnumerable<Client>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var clients = await QueryAsync<Client>(
                ClientSqlQueries.GetAll,
                cancellationToken);

            return clients;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving clients");
            throw;
        }
    }

    public async Task<IEnumerable<Client>> GetAllByProjectAsync(int projectId, CancellationToken cancellationToken = default)
    {
        try
        {
            var clients = await QueryAsync<Client>(
                ClientSqlQueries.GetAllByProject,
                new { ProjectId = projectId },
                cancellationToken);

            return clients;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving clients for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<int> GetCountAsync(int projectId, CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await ExecuteScalarAsync<int>(
                ClientSqlQueries.GetCount,
                new { ProjectId = projectId },
                cancellationToken);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting client count for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<int> CreateAsync(Client client, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new
            {
                client.ClientName,
                client.ProjectId,
                IsActive = true,
                client.CreatedAt,
                client.CreatedBy
            };

            var newClientId = await ExecuteScalarAsync<int>(
                ClientSqlQueries.Insert,
                parameters,
                cancellationToken);

            _logger.LogInformation("Created new client with ID {ClientId} for project {ProjectId}", newClientId, client.ProjectId);
            return newClientId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating client {ClientName} for project {ProjectId}", client.ClientName, client.ProjectId);
            throw;
        }
    }

    public async Task<bool> UpdateAsync(Client client, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new
            {
                ClientId = client.ClientId,
                client.ClientName,
                client.ProjectId,
                client.UpdatedAt,
                client.UpdatedBy
            };

            var rowsAffected = await ExecuteAsync(
                ClientSqlQueries.Update,
                parameters,
                cancellationToken);

            var success = rowsAffected > 0;
            if (success)
            {
                _logger.LogInformation("Updated client {ClientId} for project {ProjectId}", client.ClientId, client.ProjectId);
            }
            else
            {
                _logger.LogWarning("No rows affected when updating client {ClientId}", client.ClientId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating client {ClientId}", client.ClientId);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int clientId, int projectId, int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new
            {
                ClientId = clientId,
                ProjectId = projectId,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = userId
            };

            var rowsAffected = await ExecuteAsync(
                ClientSqlQueries.SoftDelete,
                parameters,
                cancellationToken);

            var success = rowsAffected > 0;
            if (success)
            {
                _logger.LogInformation("Soft deleted client {ClientId} for project {ProjectId}", clientId, projectId);
            }
            else
            {
                _logger.LogWarning("No rows affected when deleting client {ClientId} for project {ProjectId}", clientId, projectId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting client {ClientId} for project {ProjectId}", clientId, projectId);
            throw;
        }
    }
}