using ActoEngine.WebApi.Infrastructure.Database;
using ActoEngine.WebApi.Shared;

namespace ActoEngine.WebApi.Features.Clients;

public interface IClientRepository
{
    /// <summary>
    /// Retrieves the client with the specified identifier.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client to retrieve.</param>
    /// <returns>The client with the specified ID, or null if no matching client exists.</returns>
    Task<Client?> GetByIdAsync(int clientId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Fetches a client matching the specified name.
    /// </summary>
    /// <param name="clientName">The client name to look up.</param>
    /// <returns>The <see cref="Client"/> with the given name, or <c>null</c> if no match is found.</returns>
    Task<Client?> GetByNameAsync(string clientName, CancellationToken cancellationToken = default);
    /// <summary>
    /// Retrieves all clients.
    /// </summary>
    /// <returns>An IEnumerable&lt;Client&gt; containing all clients; empty if no clients exist.</returns>
    Task<IEnumerable<Client>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Retrieves the total number of clients.
    /// </summary>
    /// <returns>The total number of clients.</returns>
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Creates a new client, or if a client with the same name already exists returns that client's ID; if a matching soft-deleted client exists, reactivates it and returns its ID.
    /// </summary>
    /// <param name="client">The client to create. When no existing client is found, this client is persisted as active.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The ID of the created, reactivated, or existing client.</returns>
    Task<int> CreateAsync(Client client, CancellationToken cancellationToken = default);
    /// <summary>
    /// Updates an existing client's record in the repository.
    /// </summary>
    /// <param name="client">The client entity containing the updated values; ClientId specifies which record to update and UpdatedAt/UpdatedBy should reflect the update metadata.</param>
    /// <returns>`true` if a record was updated (one or more rows affected), `false` otherwise.</returns>
    Task<bool> UpdateAsync(Client client, CancellationToken cancellationToken = default);
    /// <summary>
    /// Soft-deletes the specified client by marking it inactive and recording who performed the action.
    /// </summary>
    /// <param name="clientId">ID of the client to soft-delete.</param>
    /// <param name="userId">ID of the user performing the deletion.</param>
    /// <returns>`true` if the client record was updated (soft-deleted), `false` otherwise.</returns>
    Task<bool> DeleteAsync(int clientId, int userId, CancellationToken cancellationToken = default);
}

public class ClientRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<ClientRepository> logger)
    : BaseRepository(connectionFactory, logger), IClientRepository
{
    /// <summary>
    /// Fetches the client with the specified identifier.
    /// </summary>
    /// <returns>The client with the specified ID, or null if not found.</returns>
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

    /// <summary>
    /// Fetches a client by its name.
    /// </summary>
    /// <param name="clientName">The name of the client to retrieve.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>`Client` if a client with the specified name exists, `null` otherwise.</returns>
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

    /// <summary>
    /// Retrieves all clients from the data store.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the database query.</param>
    /// <returns>An enumerable of <see cref="Client"/> objects; empty if no clients exist.</returns>
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

    /// <summary>
    /// Retrieves the total number of clients.
    /// </summary>
    /// <returns>The total number of clients.</returns>
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

    /// <summary>
    /// Creates a client if none exists, reactivates a soft-deleted client with the same name, or returns the ID of an existing active client.
    /// </summary>
    /// <param name="client">The client entity containing at minimum ClientName, CreatedAt, and CreatedBy used for insertion or reactivation.</param>
    /// <returns>The ID of the existing, reactivated, or newly created client.</returns>
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

    /// <summary>
    /// Updates an existing client record in the database.
    /// </summary>
    /// <param name="client">Client entity containing updated values; <see cref="Client.ClientId"/> identifies the record to update.</param>
    /// <returns>`true` if the client record was updated (rows affected &gt; 0), `false` otherwise.</returns>
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

    /// <summary>
    /// Marks the specified client as deleted (soft delete) and records who performed the deletion.
    /// </summary>
    /// <param name="clientId">The identifier of the client to soft-delete.</param>
    /// <param name="userId">The identifier of the user performing the deletion (stored as UpdatedBy).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>`true` if a database row was affected and the client was marked deleted, `false` otherwise.</returns>
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