using System.Linq;
using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Services.Database;
using ActoEngine.WebApi.Sql.Queries;
using Dapper;

namespace ActoEngine.WebApi.Repositories;

/// <summary>
/// Repository contract for Context operations
/// </summary>
public interface IContextRepository
{
    // Entity Context
    Task<EntityContext?> GetContextAsync(int projectId, string entityType, int entityId, CancellationToken cancellationToken = default);
    Task<EntityContext?> GetContextByNameAsync(int projectId, string entityType, string entityName, CancellationToken cancellationToken = default);
    Task<EntityContext> UpsertContextAsync(int projectId, string entityType, int entityId, string entityName, SaveContextRequest request, int userId, CancellationToken cancellationToken = default);
    Task MarkContextStaleAsync(int projectId, string entityType, int entityId, CancellationToken cancellationToken = default);
    Task MarkContextFreshAsync(int projectId, string entityType, int entityId, int userId, CancellationToken cancellationToken = default);

    // Entity Experts
    Task<List<EntityExpert>> GetExpertsAsync(int projectId, string entityType, int entityId, CancellationToken cancellationToken = default);
    Task AddExpertAsync(int projectId, string entityType, int entityId, int userId, string expertiseLevel, string? notes, int addedBy, CancellationToken cancellationToken = default);
    Task RemoveExpertAsync(int projectId, string entityType, int entityId, int userId, CancellationToken cancellationToken = default);
    Task<List<dynamic>> GetUserExpertiseAsync(int userId, int projectId, CancellationToken cancellationToken = default);

    // Context History
    Task RecordContextChangeAsync(string entityType, int entityId, string fieldName, string? oldValue, string? newValue, int changedBy, string? changeReason = null, CancellationToken cancellationToken = default);
    Task<List<ContextHistory>> GetContextHistoryAsync(string entityType, int entityId, CancellationToken cancellationToken = default);

    // Statistics
    Task<List<ContextCoverageStats>> GetContextCoverageAsync(int projectId, CancellationToken cancellationToken = default);
    Task<List<dynamic>> GetStaleContextEntitiesAsync(int projectId, CancellationToken cancellationToken = default);
    Task<List<dynamic>> GetTopDocumentedEntitiesAsync(int projectId, int limit = 10, CancellationToken cancellationToken = default);
    Task<List<dynamic>> GetCriticalUndocumentedAsync(int projectId, CancellationToken cancellationToken = default);

    // Review Requests
    Task<int> CreateReviewRequestAsync(string entityType, int entityId, int requestedBy, int? assignedTo, string? reason, CancellationToken cancellationToken = default);
    Task<List<ContextReviewRequest>> GetPendingReviewRequestsAsync(int? userId = null, CancellationToken cancellationToken = default);
    Task CompleteReviewRequestAsync(int requestId, CancellationToken cancellationToken = default);

    // Smart Suggestions
    Task<List<UserSuggestion>> GetPotentialExpertsAsync(string entityType, int entityId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for Context operations
/// </summary>
public class ContextRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<ContextRepository> logger)
    : BaseRepository(connectionFactory, logger), IContextRepository
{
    #region Entity Context Operations

    public async Task<EntityContext?> GetContextAsync(int projectId, string entityType, int entityId, CancellationToken cancellationToken = default)
    {
        try
        {
            var context = await QueryFirstOrDefaultAsync<EntityContext>(
                ContextQueries.GetContext,
                new { ProjectId = projectId, EntityType = entityType, EntityId = entityId },
                cancellationToken);
            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving context for {EntityType} with ID {EntityId} in project {ProjectId}", entityType, entityId, projectId);
            throw;
        }
    }

    public async Task<EntityContext?> GetContextByNameAsync(int projectId, string entityType, string entityName, CancellationToken cancellationToken = default)
    {
        try
        {
            var context = await QueryFirstOrDefaultAsync<EntityContext>(
                ContextQueries.GetContextByName,
                new { ProjectId = projectId, EntityType = entityType, EntityName = entityName },
                cancellationToken);
            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving context for {EntityType} with name {EntityName} in project {ProjectId}", entityType, entityName, projectId);
            throw;
        }
    }

    public async Task<EntityContext> UpsertContextAsync(
        int projectId,
        string entityType,
        int entityId,
        string entityName,
        SaveContextRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new
            {
                ProjectId = projectId,
                EntityType = entityType,
                EntityId = entityId,
                EntityName = entityName,
                request.Purpose,
                request.BusinessImpact,
                request.DataOwner,
                CriticalityLevel = request.CriticalityLevel ?? 3,
                request.BusinessDomain,
                request.Sensitivity,
                request.DataSource,
                request.ValidationRules,
                request.RetentionPolicy,
                request.DataFlow,
                request.Frequency,
                IsDeprecated = request.IsDeprecated ?? false,
                request.DeprecationReason,
                request.ReplacedBy,
                UserId = userId
            };

            var context = await QueryFirstOrDefaultAsync<EntityContext>(
                ContextQueries.UpsertContext,
                parameters,
                cancellationToken);

            if (context is null)
            {
                _logger.LogError("Upsert returned no context for {EntityType} {EntityId} in project {ProjectId}", entityType, entityId, projectId);
                throw new InvalidOperationException("Failed to upsert context.");
            }

            _logger.LogInformation("Upserted context for {EntityType} {EntityId} in project {ProjectId}", entityType, entityId, projectId);
            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting context for {EntityType} {EntityId} in project {ProjectId}", entityType, entityId, projectId);
            throw;
        }
    }

    public async Task MarkContextStaleAsync(int projectId, string entityType, int entityId, CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await ExecuteAsync(
                ContextQueries.MarkContextStale,
                new { ProjectId = projectId, EntityType = entityType, EntityId = entityId },
                cancellationToken);
            if (rows == 0)
            {
                _logger.LogWarning("No rows marked stale for {EntityType} {EntityId} in project {ProjectId}", entityType, entityId, projectId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking context stale for {EntityType} {EntityId} in project {ProjectId}", entityType, entityId, projectId);
            throw;
        }
    }

    public async Task MarkContextFreshAsync(int projectId, string entityType, int entityId, int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await ExecuteAsync(
                ContextQueries.MarkContextFresh,
                new { ProjectId = projectId, EntityType = entityType, EntityId = entityId, UserId = userId },
                cancellationToken);
            if (rows == 0)
            {
                _logger.LogWarning("No rows marked fresh for {EntityType} {EntityId} in project {ProjectId}", entityType, entityId, projectId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking context fresh for {EntityType} {EntityId} in project {ProjectId}", entityType, entityId, projectId);
            throw;
        }
    }

    #endregion

    #region Entity Experts

    public async Task<List<EntityExpert>> GetExpertsAsync(int projectId, string entityType, int entityId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            var experts = await connection.QueryAsync<EntityExpert, User, EntityExpert>(
                ContextQueries.GetExperts,
                (expert, user) =>
                {
                    expert.User = user;
                    return expert;
                },
                new { ProjectId = projectId, EntityType = entityType, EntityId = entityId },
                splitOn: "Username");

            return experts.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving experts for {EntityType} {EntityId} in project {ProjectId}", entityType, entityId, projectId);
            throw;
        }
    }

    public async Task AddExpertAsync(int projectId, string entityType, int entityId, int userId, string expertiseLevel, string? notes, int addedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await ExecuteAsync(
                ContextQueries.AddExpert,
                new
                {
                    ProjectId = projectId,
                    EntityType = entityType,
                    EntityId = entityId,
                    UserId = userId,
                    ExpertiseLevel = expertiseLevel,
                    Notes = notes,
                    AddedBy = addedBy
                },
                cancellationToken);
            if (rows == 0)
            {
                _logger.LogWarning("No changes when adding expert {UserId} for {EntityType} {EntityId} in project {ProjectId}", userId, entityType, entityId, projectId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding expert {UserId} for {EntityType} {EntityId} in project {ProjectId}", userId, entityType, entityId, projectId);
            throw;
        }
    }

    public async Task RemoveExpertAsync(int projectId, string entityType, int entityId, int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await ExecuteAsync(
                ContextQueries.RemoveExpert,
                new { ProjectId = projectId, EntityType = entityType, EntityId = entityId, UserId = userId },
                cancellationToken);
            if (rows == 0)
            {
                _logger.LogWarning("No expert removed for {EntityType} {EntityId} in project {ProjectId} and user {UserId}", entityType, entityId, projectId, userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing expert {UserId} for {EntityType} {EntityId} in project {ProjectId}", userId, entityType, entityId, projectId);
            throw;
        }
    }

    public async Task<List<dynamic>> GetUserExpertiseAsync(int userId, int projectId, CancellationToken cancellationToken = default)
    {
        try
        {
            var expertise = await QueryAsync<dynamic>(
                ContextQueries.GetUserExpertise,
                new { UserId = userId, ProjectId = projectId },
                cancellationToken);
            return expertise.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user expertise for {UserId} in project {ProjectId}", userId, projectId);
            throw;
        }
    }

    #endregion

    #region Context History

    public async Task RecordContextChangeAsync(string entityType, int entityId, string fieldName, string? oldValue, string? newValue, int changedBy, string? changeReason = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await ExecuteAsync(
                ContextQueries.RecordContextChange,
                new
                {
                    EntityType = entityType,
                    EntityId = entityId,
                    FieldName = fieldName,
                    OldValue = oldValue,
                    NewValue = newValue,
                    ChangedBy = changedBy,
                    ChangeReason = changeReason
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording context change for {EntityType} {EntityId} field {FieldName}", entityType, entityId, fieldName);
            throw;
        }
    }

    public async Task<List<ContextHistory>> GetContextHistoryAsync(string entityType, int entityId, CancellationToken cancellationToken = default)
    {
        try
        {
            var history = await QueryAsync<ContextHistory>(
                ContextQueries.GetContextHistory,
                new { EntityType = entityType, EntityId = entityId },
                cancellationToken);
            return history.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving context history for {EntityType} {EntityId}", entityType, entityId);
            throw;
        }
    }

    #endregion

    #region Statistics

    public async Task<List<ContextCoverageStats>> GetContextCoverageAsync(int projectId, CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await QueryAsync<ContextCoverageStats>(
                ContextQueries.GetContextCoverage,
                new { ProjectId = projectId },
                cancellationToken);
            return stats.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving context coverage for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<List<dynamic>> GetStaleContextEntitiesAsync(int projectId, CancellationToken cancellationToken = default)
    {
        try
        {
            var entities = await QueryAsync<dynamic>(
                ContextQueries.GetStaleContextEntities,
                new { ProjectId = projectId },
                cancellationToken);
            return entities.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stale context entities for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<List<dynamic>> GetTopDocumentedEntitiesAsync(int projectId, int limit = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            var entities = await QueryAsync<dynamic>(
                ContextQueries.GetTopDocumentedEntities,
                new { ProjectId = projectId, Limit = limit },
                cancellationToken);
            return entities.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving top documented entities for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<List<dynamic>> GetCriticalUndocumentedAsync(int projectId, CancellationToken cancellationToken = default)
    {
        try
        {
            var entities = await QueryAsync<dynamic>(
                ContextQueries.GetCriticalUndocumented,
                new { ProjectId = projectId },
                cancellationToken);
            return entities.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving critical undocumented entities for project {ProjectId}", projectId);
            throw;
        }
    }

    #endregion

    #region Review Requests

    public async Task<int> CreateReviewRequestAsync(string entityType, int entityId, int requestedBy, int? assignedTo, string? reason, CancellationToken cancellationToken = default)
    {
        try
        {
            var requestId = (await ExecuteScalarAsync<int?>(
                ContextQueries.CreateReviewRequest,
                new
                {
                    EntityType = entityType,
                    EntityId = entityId,
                    RequestedBy = requestedBy,
                    AssignedTo = assignedTo,
                    Reason = reason
                },
                cancellationToken)) ?? 0;

            if (requestId == 0)
            {
                _logger.LogWarning("CreateReviewRequest returned 0 for {EntityType} {EntityId}", entityType, entityId);
            }

            return requestId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating review request for {EntityType} {EntityId}", entityType, entityId);
            throw;
        }
    }

    public async Task<List<ContextReviewRequest>> GetPendingReviewRequestsAsync(int? userId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var requests = await QueryAsync<ContextReviewRequest>(
                ContextQueries.GetPendingReviewRequests,
                new { UserId = userId },
                cancellationToken);
            return requests.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending review requests for user {UserId}", userId);
            throw;
        }
    }

    public async Task CompleteReviewRequestAsync(int requestId, CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await ExecuteAsync(
                ContextQueries.CompleteReviewRequest,
                new { RequestId = requestId },
                cancellationToken);
            if (rows == 0)
            {
                _logger.LogWarning("No review request updated for RequestId {RequestId}", requestId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing review request {RequestId}", requestId);
            throw;
        }
    }

    #endregion

    #region Smart Suggestions

    public async Task<List<UserSuggestion>> GetPotentialExpertsAsync(string entityType, int entityId, CancellationToken cancellationToken = default)
    {
        try
        {
            var suggestions = await QueryAsync<UserSuggestion>(
                ContextQueries.GetPotentialExperts,
                new { EntityType = entityType, EntityId = entityId },
                cancellationToken);
            return suggestions.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving potential experts for {EntityType} {EntityId}", entityType, entityId);
            throw;
        }
    }

    #endregion
}