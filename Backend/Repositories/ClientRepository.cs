using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.Database;
using ActoEngine.WebApi.SqlQueries;

namespace ActoEngine.WebApi.Repositories;

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(int clientId, CancellationToken cancellationToken = default);
    Task<Client?> GetByNameAsync(string clientName, CancellationToken cancellationToken = default);
    Task<IEnumerable<Client>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
    Task<int> CreateAsync(Client client, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Client client, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int clientId, int userId, CancellationToken cancellationToken = default);
}

public class ClientRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<ClientRepository> logger)
    : BaseRepository(connectionFactory, logger), IClientRepository
{
    public async Task<Client?> GetByIdAsync(int clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await QueryFirstOrDefaultAsync<Client>(
                ClientSqlQueries.GetById,
                new { ClientId = clientId },
                cancellationToken);
            return client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving client with ID {ClientId}", clientId);
            throw;
        }
    }

    public async Task<Client?> GetByNameAsync(string clientName, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await QueryFirstOrDefaultAsync<Client>(
                ClientSqlQueries.GetByName,
                new { ClientName = clientName },
                cancellationToken);

            return client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving client with name {ClientName}", clientName);
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

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await ExecuteScalarAsync<int>(
                ClientSqlQueries.GetCount,
                cancellationToken: cancellationToken);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting client count");
            throw;
        }
    }

    public async Task<int> CreateAsync(Client client, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if client already exists (active)
            var existing = await GetByNameAsync(client.ClientName, cancellationToken);

            if (existing != null)
            {
                // Already exists and active
                _logger.LogInformation("Client '{ClientName}' already exists with ID {ClientId}", client.ClientName, existing.ClientId);
                return existing.ClientId;
            }

            // Check if soft-deleted client exists
            var existingAny = await QueryFirstOrDefaultAsync<Client>(
                ClientSqlQueries.GetByNameAny,
                new { client.ClientName },
                cancellationToken);

            if (existingAny != null && !existingAny.IsActive)
            {
                // Reactivate soft-deleted client
                await ExecuteAsync(
                    ClientSqlQueries.Reactivate,
                    new { client.ClientName, UpdatedAt = DateTime.UtcNow, UpdatedBy = client.CreatedBy },
                    cancellationToken);

                _logger.LogInformation("Reactivated client '{ClientName}' with ID {ClientId}", client.ClientName, existingAny.ClientId);
                return existingAny.ClientId;
            }

            // Insert new client
            var parameters = new
            {
                client.ClientName,
                IsActive = true,
                client.CreatedAt,
                client.CreatedBy
            };

            var newClientId = await ExecuteScalarAsync<int>(
                ClientSqlQueries.Insert,
                parameters,
                cancellationToken);

            _logger.LogInformation("Created new client with ID {ClientId}", newClientId);
            return newClientId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating client {ClientName}", client.ClientName);
            throw;
        }
    }

    public async Task<bool> UpdateAsync(Client client, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new
            {
                client.ClientId,
                client.ClientName,
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
                _logger.LogInformation("Updated client {ClientId}", client.ClientId);
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

    public async Task<bool> DeleteAsync(int clientId, int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new
            {
                ClientId = clientId,
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
                _logger.LogInformation("Soft deleted client {ClientId}", clientId);
            }
            else
            {
                _logger.LogWarning("No rows affected when deleting client {ClientId}", clientId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting client {ClientId}", clientId);
            throw;
        }
    }
}