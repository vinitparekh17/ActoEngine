using System.Data;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.Database;
using ActoEngine.WebApi.SqlQueries;
using Dapper;

namespace ActoEngine.WebApi.Repositories;

public interface IProjectClientRepository
{
    /// <summary>
    /// Retrieves the project-client link with the specified identifier.
    /// </summary>
    /// <param name="projectClientId">The identifier of the project-client link to retrieve.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The project-client link with the specified ID, or null if no matching link is found.</returns>
    Task<ProjectClient?> GetByIdAsync(int projectClientId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Retrieves the project-client association for the specified project and client.
    /// </summary>
    /// <param name="projectId">The project's identifier.</param>
    /// <param name="clientId">The client's identifier.</param>
    /// <returns>The matching <see cref="ProjectClient"/> if found, otherwise <c>null</c>.</returns>
    Task<ProjectClient?> GetByProjectAndClientAsync(int projectId, int clientId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Retrieves the client details associated with the specified project.
    /// </summary>
    /// <param name="projectId">The identifier of the project whose linked clients should be returned.</param>
    /// <returns>An enumerable of ProjectClientDetailResponse for clients linked to the specified project; empty if no clients are linked.</returns>
    Task<IEnumerable<ProjectClientDetailResponse>> GetClientsByProjectAsync(int projectId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Retrieves the projects linked to the specified client.
    /// </summary>
    /// <param name="clientId">The identifier of the client whose linked projects to retrieve.</param>
    /// <returns>An enumerable of <see cref="ProjectClientDetailResponse"/> representing the projects associated with the client; an empty collection if none are found.</returns>
    Task<IEnumerable<ProjectClientDetailResponse>> GetProjectsByClientAsync(int clientId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Retrieves all project-client links.
    /// </summary>
    /// <returns>An enumerable of ProjectClient representing every project-client link.</returns>
    Task<IEnumerable<ProjectClient>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Determines whether the specified project and client are linked.
    /// </summary>
    /// <param name="projectId">The project's identifier.</param>
    /// <param name="clientId">The client's identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>`true` if a link exists between the project and client, `false` otherwise.</returns>
    Task<bool> IsLinkedAsync(int projectId, int clientId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Checks whether a link exists between the specified project and client using the supplied database transaction.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="transaction">The active database transaction to use for the check; must have an associated connection.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>`true` if a link exists for the given project and client within the transaction, `false` otherwise.</returns>
    Task<bool> IsLinkedAsync(int projectId, IDbTransaction transaction, int clientId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Creates an association between the specified project and client or reactivates a previously soft-deleted association.
    /// </summary>
    /// <param name="projectId">The ID of the project to link.</param>
    /// <param name="clientId">The ID of the client to link.</param>
    /// <param name="userId">The ID of the user performing the operation (set as creator/updater).</param>
    /// <returns>The ProjectClientId of the existing or newly created link.</returns>
    Task<int> LinkAsync(int projectId, int clientId, int userId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Create a link between a project and a client, or reactivate a previously soft-deleted link, using the provided database transaction.
    /// </summary>
    /// <param name="projectId">Identifier of the project to link.</param>
    /// <param name="transaction">Active database transaction whose connection will be used for the operation.</param>
    /// <param name="clientId">Identifier of the client to link.</param>
    /// <param name="userId">Identifier of the user performing the operation (used for CreatedBy/UpdatedBy).</param>
    /// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
    /// <returns>The ProjectClient record ID of the existing, reactivated, or newly created link.</returns>
    Task<int> LinkAsync(int projectId, IDbTransaction transaction, int clientId, int userId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Softly unlinks a client from a project by marking the project-client association inactive.
    /// </summary>
    /// <param name="projectId">The ID of the project.</param>
    /// <param name="clientId">The ID of the client.</param>
    /// <param name="userId">The ID of the user performing the unlink operation.</param>
    /// <returns>`true` if the association was marked inactive (rows were affected), `false` otherwise.</returns>
    Task<bool> UnlinkAsync(int projectId, int clientId, int userId, CancellationToken cancellationToken = default);
}

public class ProjectClientRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<ProjectClientRepository> logger)
    : BaseRepository(connectionFactory, logger), IProjectClientRepository
{
    /// <summary>
    /// Fetches the project-client link identified by its ID.
    /// </summary>
    /// <param name="projectClientId">The identifier of the project-client link.</param>
    /// <returns>The matching <see cref="ProjectClient"/>, or null if no link exists for the given ID.</returns>
    public async Task<ProjectClient?> GetByIdAsync(int projectClientId, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectClient = await QueryFirstOrDefaultAsync<ProjectClient>(
                ProjectClientSqlQueries.GetById,
                new { ProjectClientId = projectClientId },
                cancellationToken);
            return projectClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project-client link with ID {ProjectClientId}", projectClientId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves the project-client link for the specified project and client.
    /// </summary>
    /// <returns>The matching <see cref="ProjectClient"/> if found, `null` otherwise.</returns>
    public async Task<ProjectClient?> GetByProjectAndClientAsync(int projectId, int clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectClient = await QueryFirstOrDefaultAsync<ProjectClient>(
                ProjectClientSqlQueries.GetByProjectAndClient,
                new { ProjectId = projectId, ClientId = clientId },
                cancellationToken);
            return projectClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project-client link for ProjectId {ProjectId} and ClientId {ClientId}", projectId, clientId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves detailed client associations for the specified project.
    /// </summary>
    /// <param name="projectId">The identifier of the project whose linked clients should be returned.</param>
    /// <returns>A collection of ProjectClientDetailResponse representing clients linked to the given project.</returns>
    public async Task<IEnumerable<ProjectClientDetailResponse>> GetClientsByProjectAsync(int projectId, CancellationToken cancellationToken = default)
    {
        try
        {
            var clients = await QueryAsync<ProjectClientDetailResponse>(
                ProjectClientSqlQueries.GetClientsByProject,
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

    /// <summary>
    /// Retrieves project details associated with the specified client.
    /// </summary>
    /// <param name="clientId">The identifier of the client whose projects to retrieve.</param>
    /// <returns>An enumerable of ProjectClientDetailResponse containing project details linked to the client.</returns>
    public async Task<IEnumerable<ProjectClientDetailResponse>> GetProjectsByClientAsync(int clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            var projects = await QueryAsync<ProjectClientDetailResponse>(
                ProjectClientSqlQueries.GetProjectsByClient,
                new { ClientId = clientId },
                cancellationToken);
            return projects;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving projects for client {ClientId}", clientId);
            throw;
        }
    }

    /// <summary>
    /// Retrieve all project-client link records.
    /// </summary>
    /// <returns>A collection of all <see cref="ProjectClient"/> records.</returns>
    public async Task<IEnumerable<ProjectClient>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var projectClients = await QueryAsync<ProjectClient>(
                ProjectClientSqlQueries.GetAll,
                new { },
                cancellationToken);
            return projectClients;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all project-client links");
            throw;
        }
    }

    /// <summary>
    /// Checks whether a link exists between the specified project and client.
    /// </summary>
    /// <returns>`true` if a link exists between the project and client, `false` otherwise.</returns>
    public async Task<bool> IsLinkedAsync(int projectId, int clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            var isLinked = await QueryFirstOrDefaultAsync<bool>(
                ProjectClientSqlQueries.IsLinked,
                new { ProjectId = projectId, ClientId = clientId },
                cancellationToken);
            return isLinked;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if project {ProjectId} is linked to client {ClientId}", projectId, clientId);
            throw;
        }
    }

    /// <summary>
    /// Checks whether the specified project is linked to the specified client using the provided database transaction.
    /// </summary>
    /// <param name="projectId">The project identifier to check.</param>
    /// <param name="transaction">The active database transaction to use; must have a non-null Connection.</param>
    /// <param name="clientId">The client identifier to check.</param>
    /// <param name="cancellationToken">Cancellation token (not used by the implementation but accepted for API consistency).</param>
    /// <returns>`true` if a link exists between the project and client, `false` otherwise.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the provided transaction has no associated connection.</exception>
    public async Task<bool> IsLinkedAsync(int projectId, IDbTransaction transaction, int clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = transaction.Connection ?? throw new InvalidOperationException("Transaction has no connection");
            var isLinked = await connection.ExecuteScalarAsync<bool>(
                ProjectClientSqlQueries.IsLinked,
                new { ProjectId = projectId, ClientId = clientId },
                transaction);
            return isLinked;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if project {ProjectId} is linked to client {ClientId}", projectId, clientId);
            throw;
        }
    }

    /// <summary>
    /// Creates a link between the specified project and client or reactivates a previously soft-deleted link, returning the link's identifier.
    /// </summary>
    /// <returns>The ProjectClientId of the created or reactivated link.</returns>
    public async Task<int> LinkAsync(int projectId, int clientId, int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if link already exists
            var existing = await QueryFirstOrDefaultAsync<ProjectClient>(
                ProjectClientSqlQueries.GetByProjectAndClient,
                new { ProjectId = projectId, ClientId = clientId },
                cancellationToken);

            if (existing != null)
            {
                // Already exists and active
                _logger.LogInformation("Link already exists between project {ProjectId} and client {ClientId}", projectId, clientId);
                return existing.ProjectClientId;
            }

            // Check if soft-deleted link exists
            var existingAny = await QueryFirstOrDefaultAsync<ProjectClient>(
                ProjectClientSqlQueries.GetByProjectAndClientAny,
                new { ProjectId = projectId, ClientId = clientId },
                cancellationToken);

            if (existingAny != null && !existingAny.IsActive)
            {
                // Reactivate soft-deleted link
                await ExecuteAsync(
                    ProjectClientSqlQueries.Reactivate,
                    new { ProjectId = projectId, ClientId = clientId, UpdatedAt = DateTime.UtcNow, UpdatedBy = userId },
                    cancellationToken);

                _logger.LogInformation("Reactivated link between project {ProjectId} and client {ClientId}", projectId, clientId);
                return existingAny.ProjectClientId;
            }

            // Insert new link
            var parameters = new
            {
                ProjectId = projectId,
                ClientId = clientId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };

            var projectClientId = await QueryFirstOrDefaultAsync<int>(
                ProjectClientSqlQueries.Insert,
                parameters,
                cancellationToken);

            _logger.LogInformation("Linked client {ClientId} to project {ProjectId} with ID {ProjectClientId}", clientId, projectId, projectClientId);
            return projectClientId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking client {ClientId} to project {ProjectId}", clientId, projectId);
            throw;
        }
    }

    /// <summary>
    /// Create a link between a project and a client within the provided transaction, or reactivate an existing soft-deleted link; if an active link already exists, returns its id.
    /// </summary>
    /// <param name="projectId">The project's identifier.</param>
    /// <param name="transaction">The database transaction to use; must have an open <see cref="IDbTransaction.Connection"/>.</param>
    /// <param name="clientId">The client's identifier.</param>
    /// <param name="userId">The user id to record as the creator or updater of the link.</param>
    /// <param name="cancellationToken">Token to observe for cancellation (optional).</param>
    /// <returns>The `ProjectClientId` of the existing, reactivated, or newly created link.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the provided transaction has no associated connection.</exception>
    public async Task<int> LinkAsync(int projectId, IDbTransaction transaction, int clientId, int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = transaction.Connection ?? throw new InvalidOperationException("Transaction has no connection");

            // Check if link already exists
            var existing = await connection.QueryFirstOrDefaultAsync<ProjectClient>(
                ProjectClientSqlQueries.GetByProjectAndClient,
                new { ProjectId = projectId, ClientId = clientId },
                transaction);

            if (existing != null)
            {
                // Already exists and active
                _logger.LogInformation("Link already exists between project {ProjectId} and client {ClientId}", projectId, clientId);
                return existing.ProjectClientId;
            }

            // Check if soft-deleted link exists
            var existingAny = await connection.QueryFirstOrDefaultAsync<ProjectClient>(
                ProjectClientSqlQueries.GetByProjectAndClientAny,
                new { ProjectId = projectId, ClientId = clientId },
                transaction);

            if (existingAny != null && !existingAny.IsActive)
            {
                // Reactivate soft-deleted link
                await connection.ExecuteAsync(
                    ProjectClientSqlQueries.Reactivate,
                    new { ProjectId = projectId, ClientId = clientId, UpdatedAt = DateTime.UtcNow, UpdatedBy = userId },
                    transaction);

                _logger.LogInformation("Reactivated link between project {ProjectId} and client {ClientId}", projectId, clientId);
                return existingAny.ProjectClientId;
            }

            // Insert new link
            var parameters = new
            {
                ProjectId = projectId,
                ClientId = clientId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };

            var projectClientId = await connection.ExecuteScalarAsync<int>(
                ProjectClientSqlQueries.Insert,
                parameters,
                transaction);

            _logger.LogInformation("Linked client {ClientId} to project {ProjectId} with ID {ProjectClientId}", clientId, projectId, projectClientId);
            return projectClientId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking client {ClientId} to project {ProjectId}", clientId, projectId);
            throw;
        }
    }

    /// <summary>
    /// Performs a soft delete to unlink a client from a project by marking the project-client link inactive and recording the updater and timestamp.
    /// </summary>
    /// <param name="projectId">The ID of the project.</param>
    /// <param name="clientId">The ID of the client to unlink from the project.</param>
    /// <param name="userId">The ID of the user performing the unlink operation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>`true` if a row was updated (the link was marked inactive), `false` otherwise.</returns>
    public async Task<bool> UnlinkAsync(int projectId, int clientId, int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new
            {
                ProjectId = projectId,
                ClientId = clientId,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = userId
            };

            var rowsAffected = await ExecuteAsync(
                ProjectClientSqlQueries.SoftDelete,
                parameters,
                cancellationToken);

            var success = rowsAffected > 0;
            if (success)
            {
                _logger.LogInformation("Unlinked client {ClientId} from project {ProjectId}", clientId, projectId);
            }
            else
            {
                _logger.LogWarning("No rows affected when unlinking client {ClientId} from project {ProjectId}", clientId, projectId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking client {ClientId} from project {ProjectId}", clientId, projectId);
            throw;
        }
    }
}