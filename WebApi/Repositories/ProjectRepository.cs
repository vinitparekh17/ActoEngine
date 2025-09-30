using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.Database;
using ActoEngine.WebApi.Sql.Queries;

namespace ActoEngine.WebApi.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(int projectId, int userId, CancellationToken cancellationToken = default);
    Task<int> AddOrUpdateProjectAsync(Project project, int userId);
    Task<Project?> GetByNameAsync(string projectName, int userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Project>> GetAllAsync(int userId, int offset = 0, int limit = 50, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(int userId, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(Project project, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Project project, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int projectId, int userId, CancellationToken cancellationToken = default);
    Task UpdateSyncStatusAsync(int projectId, string status, int progress, CancellationToken cancellationToken = default);
    Task<SyncStatus?> GetSyncStatusAsync(int projectId, CancellationToken cancellationToken = default);
    Task SyncSchemaMetadataAsync(int projectId, string connectionString, int userId, CancellationToken cancellationToken = default);
}

public class ProjectRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<ProjectRepository> logger)
    : BaseRepository(connectionFactory, logger), IProjectRepository
{
    public async Task<Project?> GetByIdAsync(int projectId, int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDto = await QueryFirstOrDefaultAsync<ProjectDto>(
                ProjectSqlQueries.GetById,
                new { ProjectID = projectId },
                cancellationToken);

            return projectDto?.ToDomain();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project with ID {ProjectId} for user {UserId}", projectId, userId);
            throw;
        }
    }

    public async Task<int> AddOrUpdateProjectAsync(Project project, int userId)
    {
        try
        {
            var parameters = new
            {
                ProjectId = project.ProjectId,
                ProjectName = project.ProjectName,
                Description = project.Description,
                DatabaseName = project.DatabaseName,
                ConnectionString = project.ConnectionString,
                UserId = userId
            };

            var result = await ExecuteScalarAsync<int>(
                ProjectSqlQueries.AddOrUpdateProject,
                parameters);

            _logger.LogInformation("Added or updated project {ProjectName} for user {UserId}", project.ProjectName, userId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding or updating project {ProjectName} for user {UserId}", project.ProjectName, userId);
            throw;
        }
    }

    public async Task<Project?> GetByNameAsync(string projectName, int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDto = await QueryFirstOrDefaultAsync<ProjectDto>(
                ProjectSqlQueries.GetByName,
                new { ProjectName = projectName, CreatedBy = userId },
                cancellationToken);

            return projectDto?.ToDomain();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project with name {ProjectName} for user {UserId}", projectName, userId);
            throw;
        }
    }

    public async Task<IEnumerable<Project>> GetAllAsync(int userId, int offset = 0, int limit = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectDtos = await QueryAsync<ProjectDto>(
                ProjectSqlQueries.GetAll,
                new { CreatedBy = userId, Offset = offset, Limit = limit },
                cancellationToken);

            return projectDtos.Select(dto => dto.ToDomain());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving projects for user {UserId}", userId);
            throw;
        }
    }

    public async Task<int> GetCountAsync(int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await ExecuteScalarAsync<int>(
                ProjectSqlQueries.GetCount,
                new { CreatedBy = userId },
                cancellationToken);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project count for user {UserId}", userId);
            throw;
        }
    }

    public async Task<int> CreateAsync(Project project, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new
            {
                project.ProjectName,
                project.ClientId,
                project.Description,
                project.DatabaseName,
                project.ConnectionString,
                IsActive = true,
                project.CreatedAt,
                project.CreatedBy
            };

            var newProjectId = await ExecuteScalarAsync<int>(
                ProjectSqlQueries.Insert,
                parameters,
                cancellationToken);

            _logger.LogInformation("Created new project with ID {ProjectId} for user {UserId}", newProjectId, project.CreatedBy);
            return newProjectId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project {ProjectName} for user {UserId}", project.ProjectName, project.CreatedBy);
            throw;
        }
    }

    public async Task<bool> UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new
            {
                ProjectID = project.ProjectId,
                project.ProjectName,
                project.Description,
                project.DatabaseName,
                project.ConnectionString,
                project.UpdatedAt,
                project.UpdatedBy,
                project.CreatedBy
            };

            var rowsAffected = await ExecuteAsync(
                ProjectSqlQueries.Update,
                parameters,
                cancellationToken);

            var success = rowsAffected > 0;
            if (success)
            {
                _logger.LogInformation("Updated project {ProjectId} for user {UserId}", project.ProjectId, project.UpdatedBy);
            }
            else
            {
                _logger.LogWarning("No rows affected when updating project {ProjectId}", project.ProjectId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project {ProjectId}", project.ProjectId);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int projectId, int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new
            {
                ProjectID = projectId,
                CreatedBy = userId,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = userId
            };

            var rowsAffected = await ExecuteAsync(
                ProjectSqlQueries.SoftDelete,
                parameters,
                cancellationToken);

            var success = rowsAffected > 0;
            if (success)
            {
                _logger.LogInformation("Soft deleted project {ProjectId} for user {UserId}", projectId, userId);
            }
            else
            {
                _logger.LogWarning("No rows affected when deleting project {ProjectId} for user {UserId}", projectId, userId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project {ProjectId} for user {UserId}", projectId, userId);
            throw;
        }
    }

    public async Task SyncSchemaMetadataAsync(int projectId, string connectionString, int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new
            {
                ProjectId = projectId,
                ConnectionString = connectionString,
                userId = userId
            };

            await ExecuteAsync(
                "EXEC SyncSchemaMetadata @ProjectId, @ConnectionString, @UserId",
                parameters,
                cancellationToken);

            _logger.LogInformation("Successfully synced schema metadata for project {ProjectId}", projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing schema metadata for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task UpdateSyncStatusAsync(
            int projectId,
            string status,
            int progress,
            CancellationToken cancellationToken = default)
    {
        try
        {
            await ExecuteAsync(
                SchemaSyncQueries.UpdateSyncStatus,
                new { ProjectId = projectId, Status = status, Progress = progress },
                cancellationToken);

            _logger.LogInformation("Updated sync status for project {ProjectId}: {Status} ({Progress}%)",
                projectId, status, progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating sync status for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<SyncStatus?> GetSyncStatusAsync(
        int projectId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await QueryFirstOrDefaultAsync<SyncStatus>(
                SchemaSyncQueries.GetSyncStatus,
                new { ProjectId = projectId },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sync status for project {ProjectId}", projectId);
            throw;
        }
    }
}

internal class ProjectDto
{
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }

    internal Project ToDomain()
    {
        return new Project
        {
            ProjectId = ProjectId,
            ProjectName = ProjectName,
            Description = Description,
            DatabaseName = DatabaseName,
            DatabaseType = "SqlServer", // Default since not stored in DB
            ConnectionString = ConnectionString,
            IsActive = IsActive,
            CreatedAt = CreatedAt,
            CreatedBy = CreatedBy,
            UpdatedAt = UpdatedAt,
            UpdatedBy = UpdatedBy
        };
    }
}