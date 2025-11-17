using System.Data;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.Database;
using ActoEngine.WebApi.SqlQueries;
using Dapper;

namespace ActoEngine.WebApi.Repositories;

public interface IProjectClientRepository
{
    Task<ProjectClient?> GetByIdAsync(int projectClientId, CancellationToken cancellationToken = default);
    Task<ProjectClient?> GetByProjectAndClientAsync(int projectId, int clientId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProjectClientDetailResponse>> GetClientsByProjectAsync(int projectId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProjectClientDetailResponse>> GetProjectsByClientAsync(int clientId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProjectClient>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> IsLinkedAsync(int projectId, int clientId, CancellationToken cancellationToken = default);
    Task<bool> IsLinkedAsync(int projectId, IDbTransaction transaction, int clientId, CancellationToken cancellationToken = default);
    Task<int> LinkAsync(int projectId, int clientId, int userId, CancellationToken cancellationToken = default);
    Task<int> LinkAsync(int projectId, IDbTransaction transaction, int clientId, int userId, CancellationToken cancellationToken = default);
    Task<bool> UnlinkAsync(int projectId, int clientId, int userId, CancellationToken cancellationToken = default);
}

public class ProjectClientRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<ProjectClientRepository> logger)
    : BaseRepository(connectionFactory, logger), IProjectClientRepository
{
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