using ActoEngine.WebApi.Features.Context.Dtos;
using ActoEngine.WebApi.Features.Users;
using ActoEngine.WebApi.Infrastructure.Database;
using ActoEngine.WebApi.Shared;
using Dapper;

namespace ActoEngine.WebApi.Features.Context;

/// <summary>
/// Repository contract for Context operations
/// </summary>
public interface IContextRepository
{
    // Entity Context
    Task<EntityContext?> GetContextAsync(int projectId, string entityType, int entityId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EntityContext>> GetContextBatchAsync(int projectId, List<EntityKey> entities, CancellationToken cancellationToken = default);
    Task<EntityContext?> GetContextByNameAsync(int projectId, string entityType, string entityName, CancellationToken cancellationToken = default);
    Task<EntityContext> UpsertContextAsync(int projectId, string entityType, int entityId, string entityName, SaveContextRequest request, int userId, CancellationToken cancellationToken = default);
    Task MarkContextStaleAsync(int projectId, string entityType, int entityId, CancellationToken cancellationToken = default);
    Task MarkContextFreshAsync(int projectId, string entityType, int entityId, int userId, CancellationToken cancellationToken = default);

    // Entity Experts
    Task<List<EntityExpert>> GetExpertsAsync(int projectId, string entityType, int entityId, CancellationToken cancellationToken = default);
    Task AddExpertAsync(int projectId, string entityType, int entityId, int userId, string expertiseLevel, string? notes, int addedBy, CancellationToken cancellationToken = default);
    Task RemoveExpertAsync(int projectId, string entityType, int entityId, int userId, CancellationToken cancellationToken = default);
    Task<List<UserExpertiseItem>> GetUserExpertiseAsync(int userId, int projectId, CancellationToken cancellationToken = default);

    // Context History
    Task RecordContextChangeAsync(int projectId, string entityType, int entityId, string fieldName, string? oldValue, string? newValue, int changedBy, string? changeReason = null, CancellationToken cancellationToken = default);
    Task<List<ContextHistory>> GetContextHistoryAsync(int projectId, string entityType, int entityId, CancellationToken cancellationToken = default);

    // Statistics
    Task<List<ContextGap>> GetContextGapsAsync(int projectId, int limit, CancellationToken cancellationToken = default);
    Task<List<ContextCoverageStats>> GetContextCoverageAsync(int projectId, CancellationToken cancellationToken = default);
    Task<List<StaleContextEntity>> GetStaleContextEntitiesAsync(int projectId, CancellationToken cancellationToken = default);
    Task<List<TopDocumentedEntity>> GetTopDocumentedEntitiesAsync(int projectId, int limit = 10, CancellationToken cancellationToken = default);
    Task<List<CriticalUndocumentedEntity>> GetCriticalUndocumentedAsync(int projectId, CancellationToken cancellationToken = default);

    // Review Requests
    Task<int> CreateReviewRequestAsync(int projectId, string entityType, int entityId, int requestedBy, int? assignedTo, string? reason, CancellationToken cancellationToken = default);
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
        var context = await QueryFirstOrDefaultAsync<EntityContext>(
            ContextQueries.GetContext,
            new { ProjectId = projectId, EntityType = entityType, EntityId = entityId },
            cancellationToken);
        return context;
    }

    public async Task<IEnumerable<EntityContext>> GetContextBatchAsync(int projectId, List<EntityKey> entities, CancellationToken cancellationToken = default)
    {
        if (entities == null || entities.Count == 0)
        {
            return Enumerable.Empty<EntityContext>();
        }

        // Construct dynamic WHERE clause for (EntityType, EntityId) tuples
        // Build parameterized OR clauses to handle multiple entity type-id pairs
        // Example: WHERE ProjectId = @ProjectId AND ((EntityType = @T0 AND EntityId = @I0) OR (EntityType = @T1 AND EntityId = @I1))

        // Get base query without ORDER BY (we'll add it at the end)
        var sql = ContextQueries.GetContextBatchBase.TrimEnd().TrimEnd(';');
        // Remove ORDER BY clause if present
        var orderByIndex = sql.LastIndexOf("ORDER BY", StringComparison.OrdinalIgnoreCase);
        if (orderByIndex >= 0)
        {
            sql = sql.Substring(0, orderByIndex).TrimEnd();
        }

        var parameters = new DynamicParameters();
        parameters.Add("ProjectId", projectId);

        var clauses = new List<string>();
        for (int i = 0; i < entities.Count; i++)
        {
            // SECURITY FIX: Build parameter placeholders as part of SQL string, not through interpolation
            // These are safe string literals that will be bound to parameters by Dapper
            var typeParam = $"T{i}";
            var idParam = $"I{i}";
            clauses.Add($"(EntityType = @{typeParam} AND EntityId = @{idParam})");
            parameters.Add(typeParam, entities[i].EntityType);
            parameters.Add(idParam, entities[i].EntityId);
        }

        if (clauses.Count != 0)
        {
            sql += " AND (" + string.Join(" OR ", clauses) + ")";
        }

        // Add ORDER BY at the end
        sql += " ORDER BY EntityType, EntityId;";

        var contexts = await QueryAsync<EntityContext>(
            sql,
            parameters,
            cancellationToken);

        return contexts;
    }

    public async Task<EntityContext?> GetContextByNameAsync(int projectId, string entityType, string entityName, CancellationToken cancellationToken = default)
    {
        var context = await QueryFirstOrDefaultAsync<EntityContext>(
            ContextQueries.GetContextByName,
            new { ProjectId = projectId, EntityType = entityType, EntityName = entityName },
            cancellationToken);
        return context;
    }

    /// <summary>
    /// Inserts a new context or updates an existing context for the specified entity in a project.
    /// </summary>
    /// <param name="projectId">The project identifier the entity belongs to.</param>
    /// <param name="entityType">The type name of the entity (e.g., table or domain object).</param>
    /// <param name="entityId">The unique identifier of the entity.</param>
    /// <param name="entityName">The human-readable name of the entity.</param>
    /// <param name="request">Payload containing context fields to save (purpose, criticality, owners, etc.).</param>
    /// <param name="userId">Identifier of the user performing the operation.</param>
    /// <param name="cancellationToken">Token to observe for cancellation of the operation.</param>
    /// <returns>The inserted or updated <see cref="EntityContext"/> for the entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the context could not be saved.</exception>
    public async Task<EntityContext> UpsertContextAsync(
        int projectId,
        string entityType,
        int entityId,
        string entityName,
        SaveContextRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        // Check if context already exists
        var existing = await GetContextAsync(projectId, entityType, entityId, cancellationToken);

        var parameters = new
        {
            ProjectId = projectId,
            EntityType = entityType,
            EntityId = entityId,
            EntityName = entityName,
            request.Purpose,
            request.BusinessImpact,
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

        EntityContext? context;

        if (existing != null)
        {
            // Update existing context
            context = await QueryFirstOrDefaultAsync<EntityContext>(
                ContextQueries.UpdateContext,
                parameters,
                cancellationToken);
            _logger.LogInformation("Updated context for {EntityType} {EntityId} in project {ProjectId}", entityType, entityId, projectId);
        }
        else
        {
            // Insert new context
            context = await QueryFirstOrDefaultAsync<EntityContext>(
                ContextQueries.InsertContext,
                parameters,
                cancellationToken);
            _logger.LogInformation("Inserted context for {EntityType} {EntityId} in project {ProjectId}", entityType, entityId, projectId);
        }

        if (context is null)
        {
            _logger.LogError("Failed to save context for {EntityType} {EntityId} in project {ProjectId}", entityType, entityId, projectId);
            throw new InvalidOperationException("Failed to save context.");
        }

        return context;
    }

    public async Task MarkContextStaleAsync(int projectId, string entityType, int entityId, CancellationToken cancellationToken = default)
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

    public async Task MarkContextFreshAsync(int projectId, string entityType, int entityId, int userId, CancellationToken cancellationToken = default)
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

    #endregion

    #region Entity Experts

    public async Task<List<EntityExpert>> GetExpertsAsync(int projectId, string entityType, int entityId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var experts = await connection.QueryAsync<EntityExpert, UserBasicInfo, EntityExpert>(
            ContextQueries.GetExperts,
            (expert, user) =>
            {
                expert.User = user;
                return expert;
            },
            new { ProjectId = projectId, EntityType = entityType, EntityId = entityId },
            splitOn: "UserID");

        return [.. experts];
    }

    /// <summary>
    /// Adds a new expert or updates an existing expert for the specified project entity, setting the expertise level and optional notes and recording who added the entry.
    /// </summary>
    /// <param name="projectId">Identifier of the project containing the entity.</param>
    /// <param name="entityType">Type name of the entity (e.g., "Document", "Issue").</param>
    /// <param name="entityId">Identifier of the entity within the project.</param>
    /// <param name="userId">Identifier of the user being added or updated as an expert.</param>
    /// <param name="expertiseLevel">The user's expertise level for the entity.</param>
    /// <param name="notes">Optional notes about the user's expertise.</param>
    /// <param name="addedBy">Identifier of the user who performed the add or update operation.</param>
    /// <param name="cancellationToken">Token to observe while waiting for the task to complete.</param>
    public async Task AddExpertAsync(int projectId, string entityType, int entityId, int userId, string expertiseLevel, string? notes, int addedBy, CancellationToken cancellationToken = default)
    {
        // Check if expert already exists
        var existing = await QueryFirstOrDefaultAsync<dynamic>(
            ContextQueries.GetExpert,
            new { ProjectId = projectId, EntityType = entityType, EntityId = entityId, UserId = userId },
            cancellationToken);

        var parameters = new
        {
            ProjectId = projectId,
            EntityType = entityType,
            EntityId = entityId,
            UserId = userId,
            ExpertiseLevel = expertiseLevel,
            Notes = notes,
            AddedBy = addedBy
        };

        if (existing != null)
        {
            // Update existing expert
            await ExecuteAsync(ContextQueries.UpdateExpert, parameters, cancellationToken);
            _logger.LogInformation("Updated expert {UserId} for {EntityType} {EntityId} in project {ProjectId}", userId, entityType, entityId, projectId);
        }
        else
        {
            // Insert new expert
            await ExecuteAsync(ContextQueries.InsertExpert, parameters, cancellationToken);
            _logger.LogInformation("Added expert {UserId} for {EntityType} {EntityId} in project {ProjectId}", userId, entityType, entityId, projectId);
        }
    }

    public async Task RemoveExpertAsync(int projectId, string entityType, int entityId, int userId, CancellationToken cancellationToken = default)
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

    public async Task<List<UserExpertiseItem>> GetUserExpertiseAsync(int userId, int projectId, CancellationToken cancellationToken = default)
    {
        var expertise = await QueryAsync<UserExpertiseItem>(
            ContextQueries.GetUserExpertise,
            new { UserId = userId, ProjectId = projectId },
            cancellationToken);
        return [.. expertise];
    }

    #endregion

    #region Context History

    public async Task RecordContextChangeAsync(int projectId, string entityType, int entityId, string fieldName, string? oldValue, string? newValue, int changedBy, string? changeReason = null, CancellationToken cancellationToken = default)
    {
        var affected = await ExecuteAsync(
            ContextQueries.RecordContextChange,
            new
            {
                ProjectId = projectId,
                EntityType = entityType,
                EntityId = entityId,
                FieldName = fieldName,
                OldValue = oldValue,
                NewValue = newValue,
                ChangedBy = changedBy,
                ChangeReason = changeReason
            },
            cancellationToken);

        if (affected == 0)
        {
            _logger.LogError("Failed to record context change for {EntityType} {EntityId} field {FieldName}", entityType, entityId, fieldName);
            throw new InvalidOperationException($"Failed to record context change for {entityType} {entityId}");
        }
    }

    public async Task<List<ContextHistory>> GetContextHistoryAsync(int projectId, string entityType, int entityId, CancellationToken cancellationToken = default)
    {
        var history = await QueryAsync<ContextHistory>(
            ContextQueries.GetContextHistory,
            new { ProjectId = projectId, EntityType = entityType, EntityId = entityId },
            cancellationToken);
        return [.. history];
    }

    #endregion

    #region Statistics

    public async Task<List<ContextGap>> GetContextGapsAsync(int projectId, int limit, CancellationToken cancellationToken = default)
    {
        var gaps = await QueryAsync<ContextGap>(
            ContextQueries.GetContextGaps,
            new { ProjectId = projectId, Limit = limit },
            cancellationToken);
        return [.. gaps];
    }

    public async Task<List<ContextCoverageStats>> GetContextCoverageAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var stats = await QueryAsync<ContextCoverageStats>(
            ContextQueries.GetContextCoverage,
            new { ProjectId = projectId },
            cancellationToken);
        return [.. stats];
    }

    public async Task<List<StaleContextEntity>> GetStaleContextEntitiesAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var entities = await QueryAsync<StaleContextEntity>(
            ContextQueries.GetStaleContextEntities,
            new { ProjectId = projectId },
            cancellationToken);
        return [.. entities];
    }

    public async Task<List<TopDocumentedEntity>> GetTopDocumentedEntitiesAsync(int projectId, int limit = 10, CancellationToken cancellationToken = default)
    {
        var entities = await QueryAsync<TopDocumentedEntity>(
            ContextQueries.GetTopDocumentedEntities,
            new { ProjectId = projectId, Limit = limit },
            cancellationToken);
        return [.. entities];
    }

    public async Task<List<CriticalUndocumentedEntity>> GetCriticalUndocumentedAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var entities = await QueryAsync<CriticalUndocumentedEntity>(
            ContextQueries.GetCriticalUndocumented,
            new { ProjectId = projectId },
            cancellationToken);
        return [.. entities];
    }

    #endregion

    #region Review Requests

    public async Task<int> CreateReviewRequestAsync(int projectId, string entityType, int entityId, int requestedBy, int? assignedTo, string? reason, CancellationToken cancellationToken = default)
    {
        var requestId = (await ExecuteScalarAsync<int?>(
            ContextQueries.CreateReviewRequest,
            new
            {
                ProjectId = projectId,
                EntityType = entityType,
                EntityId = entityId,
                RequestedBy = requestedBy,
                AssignedTo = assignedTo,
                Reason = reason
            },
            cancellationToken)) ?? 0;

        if (requestId == 0)
        {
            _logger.LogError("CreateReviewRequest failed for {EntityType} {EntityId} in project {ProjectId}", entityType, entityId, projectId);
            throw new InvalidOperationException($"Failed to create review request for {entityType} {entityId} in project {projectId}");
        }

        return requestId;
    }

    public async Task<List<ContextReviewRequest>> GetPendingReviewRequestsAsync(int? userId = null, CancellationToken cancellationToken = default)
    {
        var requests = await QueryAsync<ContextReviewRequest>(
            ContextQueries.GetPendingReviewRequests,
            new { UserId = userId },
            cancellationToken);
        return [.. requests];
    }

    public async Task CompleteReviewRequestAsync(int requestId, CancellationToken cancellationToken = default)
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

    #endregion

    #region Smart Suggestions

    public async Task<List<UserSuggestion>> GetPotentialExpertsAsync(string entityType, int entityId, CancellationToken cancellationToken = default)
    {
        var suggestions = await QueryAsync<UserSuggestion>(
            ContextQueries.GetPotentialExperts,
            new { EntityType = entityType, EntityId = entityId },
            cancellationToken);
        return [.. suggestions];
    }

    #endregion
}