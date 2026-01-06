using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Infrastructure.Database;
using ActoEngine.WebApi.Shared;
using ActoEngine.WebApi.SqlQueries;

namespace ActoEngine.WebApi.Repositories;

/// <summary>
/// Public DTO for Project
/// </summary>
public class PublicProjectDto
{
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string? DatabaseType { get; set; } = "SqlServer";
    public bool IsActive { get; set; }
    public bool IsLinked { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
    public string? SyncStatus { get; set; }
    public int SyncProgress { get; set; }
    public DateTime? LastSyncAttempt { get; set; }
}

public interface IProjectRepository
{
    Task<PublicProjectDto?> GetByIdAsync(int projectId, CancellationToken cancellationToken = default);
    Task<PublicProjectDto?> GetByNameAsync(string projectName, int userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PublicProjectDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> AddOrUpdateProjectAsync(Project project, int userId);
    Task<int> GetCountAsync(int userId, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(Project project, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Project project, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int projectId, int userId, CancellationToken cancellationToken = default);
    Task UpdateSyncStatusAsync(int projectId, string status, int progress, CancellationToken cancellationToken = default);
    Task<SyncStatus?> GetSyncStatusAsync(int projectId, CancellationToken cancellationToken = default);
    Task SyncSchemaMetadataAsync(int projectId, string connectionString, int userId, CancellationToken cancellationToken = default);
    Task UpdateIsLinkedAsync(int projectId, bool isLinked, CancellationToken cancellationToken = default);
}

public class ProjectRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<ProjectRepository> logger)
    : BaseRepository(connectionFactory, logger), IProjectRepository
{
    public async Task<PublicProjectDto?> GetByIdAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var publicDto = await QueryFirstOrDefaultAsync<PublicProjectDto>(
            ProjectSqlQueries.GetById,
            new { ProjectID = projectId },
            cancellationToken);

        return publicDto;
    }

    /// <summary>
    /// Adds a new project or updates an existing project for the specified user.
    /// </summary>
    /// <param name="project">The project entity containing values to insert or update.</param>
    /// <param name="userId">The identifier of the user performing the operation.</param>
    /// <returns>The identifier of the created or updated project.</returns>
    public async Task<int> AddOrUpdateProjectAsync(Project project, int userId)
    {
        var parameters = new
        {
            project.ProjectId,
            project.ProjectName,
            project.Description,
            project.DatabaseName,
            project.IsLinked,
            UserId = userId
        };

        var result = await ExecuteScalarAsync<int>(
            ProjectSqlQueries.AddOrUpdateProject,
            parameters);

        _logger.LogInformation("Added or updated project {ProjectName} for user {UserId}", project.ProjectName, userId);
        return result;
    }

    public async Task<PublicProjectDto?> GetByNameAsync(string projectName, int userId, CancellationToken cancellationToken = default)
    {
        var publicDto = await QueryFirstOrDefaultAsync<PublicProjectDto>(
            ProjectSqlQueries.GetByName,
            new { ProjectName = projectName, CreatedBy = userId },
            cancellationToken);

        return publicDto;
    }

    public async Task<IEnumerable<PublicProjectDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var publicDtos = await QueryAsync<PublicProjectDto>(
            ProjectSqlQueries.GetAll,
            cancellationToken);

        return publicDtos;
    }

    public async Task<int> GetCountAsync(int userId, CancellationToken cancellationToken = default)
    {
        var count = await ExecuteScalarAsync<int>(
            ProjectSqlQueries.GetCount,
            new { CreatedBy = userId },
            cancellationToken);

        return count;
    }

    public async Task<int> CreateAsync(Project project, CancellationToken cancellationToken = default)
    {
        var parameters = new
        {
            project.ProjectName,
            project.Description,
            project.DatabaseName,
            project.IsLinked,
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

    public async Task<bool> UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        var parameters = new
        {
            ProjectID = project.ProjectId,
            project.ProjectName,
            project.Description,
            project.DatabaseName,
            project.IsLinked,
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

    public async Task<bool> DeleteAsync(int projectId, int userId, CancellationToken cancellationToken = default)
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

    /// <summary>
    /// Synchronizes the project's database schema metadata using the provided connection string.
    /// </summary>
    /// <param name="projectId">Identifier of the project whose schema metadata will be synchronized.</param>
    /// <param name="connectionString">Database connection string used to access the project's database.</param>
    /// <param name="userId">Identifier of the user initiating the synchronization.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task SyncSchemaMetadataAsync(int projectId, string connectionString, int userId, CancellationToken cancellationToken = default)
    {
        var parameters = new
        {
            ProjectId = projectId,
            ConnectionString = connectionString,
            UserId = userId
        };

        await ExecuteAsync(
            "EXEC SyncSchemaMetadata @ProjectId, @ConnectionString, @UserId",
            parameters,
            cancellationToken);

        _logger.LogInformation("Successfully synced schema metadata for project {ProjectId}", projectId);
    }

    public async Task UpdateSyncStatusAsync(
            int projectId,
            string status,
            int progress,
            CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(
            SchemaSyncQueries.UpdateSyncStatus,
            new { ProjectId = projectId, Status = status, Progress = progress },
            cancellationToken);

        _logger.LogInformation("Updated sync status for project {ProjectId}: {Status} ({Progress}%)",
            projectId, status, progress);
    }

    public async Task<SyncStatus?> GetSyncStatusAsync(
        int projectId,
        CancellationToken cancellationToken = default)
    {
        return await QueryFirstOrDefaultAsync<SyncStatus>(
            SchemaSyncQueries.GetSyncStatus,
            new { ProjectId = projectId },
            cancellationToken);
    }

    public async Task UpdateIsLinkedAsync(int projectId, bool isLinked, CancellationToken cancellationToken = default)
    {
        const string query = @"
            UPDATE Projects
            SET IsLinked = @IsLinked, UpdatedAt = GETDATE()
            WHERE ProjectId = @ProjectId AND IsActive = 1";

        await ExecuteAsync(
            query,
            new { ProjectId = projectId, IsLinked = isLinked },
            cancellationToken);

        _logger.LogInformation("Updated IsLinked status for project {ProjectId} to {IsLinked}", projectId, isLinked);
    }
}