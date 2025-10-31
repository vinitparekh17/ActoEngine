using ActoEngine.WebApi.Models;
using ActoEngine.WebApi.Repositories;
using ActoEngine.WebApi.Config;
using System.Text.RegularExpressions;

namespace ActoEngine.WebApi.Services.ContextService;

/// <summary>
/// Service for managing entity context and documentation
/// </summary>
public class ContextService
{
    private readonly ContextRepository _contextRepo;
    private readonly SchemaRepository _schemaRepo;
    private readonly UserRepository _userRepo;

    public ContextService(
        ContextRepository contextRepo,
        SchemaRepository schemaRepo,
        UserRepository userRepo)
    {
        _contextRepo = contextRepo;
        _schemaRepo = schemaRepo;
        _userRepo = userRepo;
    }

    #region Get Context

    /// <summary>
    /// Get full context response with all related data
    /// </summary>
    public async Task<ContextResponse?> GetContextAsync(int projectId, string entityType, int entityId)
    {
        var context = await _contextRepo.GetContextAsync(projectId, entityType, entityId);
        
        if (context == null)
        {
            // Return empty context with suggestions
            context = new EntityContext
            {
                ProjectId = projectId,
                EntityType = entityType,
                EntityId = entityId,
                EntityName = await GetEntityNameAsync(projectId, entityType, entityId) ?? "Unknown"
            };
        }

        var experts = await _contextRepo.GetExpertsAsync(projectId, entityType, entityId);
        var suggestions = await GetContextSuggestionsAsync(projectId, entityType, entityId, context);
        var completeness = CalculateCompletenessScore(context);

        return new ContextResponse
        {
            Context = context,
            Experts = experts,
            Suggestions = suggestions,
            CompletenessScore = completeness,
            IsStale = context.IsContextStale,
            DependencyCount = 0 // TODO: Implement with DependencyMap
        };
    }

    /// <summary>
    /// Get entity name from metadata
    /// </summary>
    private async Task<string?> GetEntityNameAsync(int projectId, string entityType, int entityId)
    {
        return entityType switch
        {
            "TABLE" => (await _schemaRepo.GetTableByIdAsync(entityId))?.TableName,
            "COLUMN" => (await _schemaRepo.GetColumnByIdAsync(entityId))?.ColumnName,
            "SP" => (await _schemaRepo.GetSpByIdAsync(entityId))?.ProcedureName,
            _ => null
        };
    }

    #endregion

    #region Save Context

    /// <summary>
    /// Save or update context
    /// </summary>
    public async Task<EntityContext> SaveContextAsync(
        int projectId,
        string entityType,
        int entityId,
        SaveContextRequest request,
        int userId)
    {
        // Validate entity exists
        var entityName = await GetEntityNameAsync(projectId, entityType, entityId);
        if (entityName == null)
            throw new ArgumentException($"{entityType} with ID {entityId} not found");

        // Get old context for history tracking
        var oldContext = await _contextRepo.GetContextAsync(projectId, entityType, entityId);

        // Save context
        var context = await _contextRepo.UpsertContextAsync(
            projectId, entityType, entityId, entityName, request, userId);

        // Record changes in history
        if (oldContext != null)
        {
            await RecordContextChangesAsync(entityType, entityId, oldContext, context, userId);
        }

        // Update experts if provided
        if (request.ExpertUserIds != null && request.ExpertUserIds.Any())
        {
            foreach (var expertUserId in request.ExpertUserIds)
            {
                await _contextRepo.AddExpertAsync(
                    projectId, entityType, entityId, 
                    expertUserId, "EXPERT", null, userId);
            }
        }

        return context;
    }

    /// <summary>
    /// Record field-level changes
    /// </summary>
    private async Task RecordContextChangesAsync(
        string entityType,
        int entityId,
        EntityContext oldContext,
        EntityContext newContext,
        int userId)
    {
        var changes = new List<(string Field, string? OldValue, string? NewValue)>();

        // Compare fields
        if (oldContext.Purpose != newContext.Purpose)
            changes.Add(("Purpose", oldContext.Purpose, newContext.Purpose));

        if (oldContext.BusinessImpact != newContext.BusinessImpact)
            changes.Add(("BusinessImpact", oldContext.BusinessImpact, newContext.BusinessImpact));

        if (oldContext.DataOwner != newContext.DataOwner)
            changes.Add(("DataOwner", oldContext.DataOwner, newContext.DataOwner));

        if (oldContext.CriticalityLevel != newContext.CriticalityLevel)
            changes.Add(("CriticalityLevel", 
                oldContext.CriticalityLevel.ToString(), 
                newContext.CriticalityLevel.ToString()));

        if (oldContext.BusinessDomain != newContext.BusinessDomain)
            changes.Add(("BusinessDomain", oldContext.BusinessDomain, newContext.BusinessDomain));

        if (oldContext.Sensitivity != newContext.Sensitivity)
            changes.Add(("Sensitivity", oldContext.Sensitivity, newContext.Sensitivity));

        // Record each change
        foreach (var (field, oldValue, newValue) in changes)
        {
            await _contextRepo.RecordContextChangeAsync(
                entityType, entityId, field, oldValue, newValue, userId);
        }
    }

    #endregion

    #region Context Suggestions

    /// <summary>
    /// Generate smart suggestions for context fields
    /// </summary>
    public async Task<ContextSuggestions> GetContextSuggestionsAsync(
        int projectId,
        string entityType,
        int entityId,
        EntityContext? existingContext = null)
    {
        var suggestions = new ContextSuggestions();

        // Get entity name
        var entityName = await GetEntityNameAsync(projectId, entityType, entityId) 
            ?? existingContext?.EntityName ?? "Unknown";

        // Suggest business domain from naming patterns
        if (string.IsNullOrEmpty(existingContext?.BusinessDomain))
        {
            suggestions.BusinessDomain = InferBusinessDomain(entityName);
        }

        // Suggest sensitivity for columns
        if (entityType == "COLUMN" && string.IsNullOrEmpty(existingContext?.Sensitivity))
        {
            suggestions.Sensitivity = InferSensitivity(entityName);
        }

        // Suggest potential experts based on recent activity
        var potentialExperts = await _contextRepo.GetPotentialExpertsAsync(entityType, entityId);
        suggestions.PotentialExperts = potentialExperts;

        // Suggest purpose based on naming (basic implementation)
        if (string.IsNullOrEmpty(existingContext?.Purpose))
        {
            suggestions.Purpose = GeneratePurposeSuggestion(entityType, entityName);
        }

        return suggestions;
    }

    /// <summary>
    /// Infer business domain from entity name
    /// </summary>
    private string InferBusinessDomain(string name)
    {
        var lowerName = name.ToLower();

        if (ContainsAny(lowerName, "order", "cart", "checkout"))
            return "ORDERS";
        
        if (ContainsAny(lowerName, "invoice", "payment", "transaction", "billing", "revenue"))
            return "FINANCE";
        
        if (ContainsAny(lowerName, "user", "customer", "account", "profile", "member"))
            return "USERS";
        
        if (ContainsAny(lowerName, "product", "inventory", "stock", "item", "sku"))
            return "INVENTORY";
        
        if (ContainsAny(lowerName, "report", "analytics", "metric", "dashboard"))
            return "REPORTING";
        
        if (ContainsAny(lowerName, "integration", "sync", "webhook", "api"))
            return "INTEGRATION";

        return "GENERAL";
    }

    /// <summary>
    /// Infer sensitivity from column name
    /// </summary>
    private string InferSensitivity(string columnName)
    {
        var lowerName = columnName.ToLower();

        // PII indicators
        if (ContainsAny(lowerName, "ssn", "social", "tax", "dob", "birthdate", "email", 
            "phone", "address", "passport", "license"))
            return "PII";

        // Financial indicators
        if (ContainsAny(lowerName, "salary", "wage", "income", "revenue", "cost", "price", 
            "amount", "balance", "credit", "account"))
            return "FINANCIAL";

        // Sensitive indicators
        if (ContainsAny(lowerName, "password", "secret", "token", "key", "hash", 
            "credential", "pin"))
            return "SENSITIVE";

        // Internal indicators
        if (ContainsAny(lowerName, "internal", "private", "confidential"))
            return "INTERNAL";

        return "PUBLIC";
    }

    /// <summary>
    /// Generate purpose suggestion
    /// </summary>
    private string GeneratePurposeSuggestion(string entityType, string name)
    {
        return entityType switch
        {
            "TABLE" => $"Stores {CleanName(name)} data",
            "COLUMN" => $"Represents {CleanName(name)} value",
            "SP" => $"Handles {CleanName(name)} operation",
            _ => $"Purpose not yet documented for {name}"
        };
    }

    /// <summary>
    /// Clean entity name for suggestions
    /// </summary>
    private string CleanName(string name)
    {
        // Convert PascalCase to words
        var words = Regex.Replace(name, "([A-Z])", " $1").Trim().ToLower();
        
        // Remove common prefixes
        words = Regex.Replace(words, @"^(tbl_|sp_|fn_|vw_)", "");
        
        return words;
    }

    /// <summary>
    /// Helper to check if string contains any of the keywords
    /// </summary>
    private bool ContainsAny(string text, params string[] keywords)
    {
        return keywords.Any(k => text.Contains(k));
    }

    #endregion

    #region Experts Management

    /// <summary>
    /// Add expert to entity
    /// </summary>
    public async Task AddExpertAsync(
        int projectId,
        string entityType,
        int entityId,
        int userId,
        string expertiseLevel,
        string? notes,
        int addedBy)
    {
        // Validate user exists
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null)
            throw new ArgumentException($"User with ID {userId} not found");

        // Validate expertise level
        var validLevels = new[] { "OWNER", "EXPERT", "FAMILIAR", "CONTRIBUTOR" };
        if (!validLevels.Contains(expertiseLevel.ToUpper()))
            throw new ArgumentException($"Invalid expertise level. Must be one of: {string.Join(", ", validLevels)}");

        await _contextRepo.AddExpertAsync(
            projectId, entityType, entityId, userId, 
            expertiseLevel.ToUpper(), notes, addedBy);
    }

    /// <summary>
    /// Remove expert from entity
    /// </summary>
    public async Task RemoveExpertAsync(int projectId, string entityType, int entityId, int userId)
    {
        await _contextRepo.RemoveExpertAsync(projectId, entityType, entityId, userId);
    }

    /// <summary>
    /// Get all entities user is an expert on
    /// </summary>
    public async Task<List<dynamic>> GetUserExpertiseAsync(int userId, int projectId)
    {
        return await _contextRepo.GetUserExpertiseAsync(userId, projectId);
    }

    #endregion

    #region Statistics & Insights

    /// <summary>
    /// Get context coverage statistics
    /// </summary>
    public async Task<List<ContextCoverageStats>> GetContextCoverageAsync(int projectId)
    {
        var stats = await _contextRepo.GetContextCoverageAsync(projectId);
        
        // Calculate coverage percentages
        foreach (var stat in stats)
        {
            if (stat.Total > 0)
            {
                stat.CoveragePercentage = Math.Round((decimal)stat.Documented / stat.Total * 100, 2);
            }
        }

        return stats;
    }

    /// <summary>
    /// Get entities with stale context
    /// </summary>
    public async Task<List<dynamic>> GetStaleContextEntitiesAsync(int projectId)
    {
        return await _contextRepo.GetStaleContextEntitiesAsync(projectId);
    }

    /// <summary>
    /// Get top documented entities
    /// </summary>
    public async Task<List<dynamic>> GetTopDocumentedEntitiesAsync(int projectId, int limit = 10)
    {
        return await _contextRepo.GetTopDocumentedEntitiesAsync(projectId, limit);
    }

    /// <summary>
    /// Get critical undocumented entities
    /// </summary>
    public async Task<List<dynamic>> GetCriticalUndocumentedAsync(int projectId)
    {
        return await _contextRepo.GetCriticalUndocumentedAsync(projectId);
    }

    /// <summary>
    /// Calculate completeness score for context
    /// </summary>
    public int CalculateCompletenessScore(EntityContext context)
    {
        var score = 0;
        var maxScore = 0;

        // Purpose (30 points)
        maxScore += 30;
        if (!string.IsNullOrWhiteSpace(context.Purpose))
            score += 30;

        // Data Owner (20 points)
        maxScore += 20;
        if (!string.IsNullOrWhiteSpace(context.DataOwner))
            score += 20;

        // Business Domain (15 points)
        maxScore += 15;
        if (!string.IsNullOrWhiteSpace(context.BusinessDomain))
            score += 15;

        // Business Impact (20 points - important for "what breaks")
        maxScore += 20;
        if (!string.IsNullOrWhiteSpace(context.BusinessImpact))
            score += 20;

        // Type-specific scoring
        if (context.EntityType == "COLUMN")
        {
            // Sensitivity (15 points)
            maxScore += 15;
            if (!string.IsNullOrWhiteSpace(context.Sensitivity))
                score += 15;
        }
        else if (context.EntityType == "SP")
        {
            // Data Flow (15 points)
            maxScore += 15;
            if (!string.IsNullOrWhiteSpace(context.DataFlow))
                score += 15;
        }

        return maxScore > 0 ? (int)Math.Round((double)score / maxScore * 100) : 0;
    }

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Bulk import context entries
    /// </summary>
    public async Task<List<BulkImportResult>> BulkImportContextAsync(
        int projectId,
        List<BulkContextEntry> entries,
        int userId)
    {
        var results = new List<BulkImportResult>();

        foreach (var entry in entries)
        {
            try
            {
                await SaveContextAsync(
                    projectId,
                    entry.EntityType,
                    entry.EntityId,
                    entry.Context,
                    userId);

                results.Add(new BulkImportResult
                {
                    EntityName = entry.EntityName,
                    Success = true
                });
            }
            catch (Exception ex)
            {
                results.Add(new BulkImportResult
                {
                    EntityName = entry.EntityName,
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        return results;
    }

    #endregion

    #region Review Management

    /// <summary>
    /// Create review request for stale context
    /// </summary>
    public async Task<int> CreateReviewRequestAsync(
        string entityType,
        int entityId,
        int requestedBy,
        int? assignedTo,
        string? reason)
    {
        return await _contextRepo.CreateReviewRequestAsync(
            entityType, entityId, requestedBy, assignedTo, reason);
    }

    /// <summary>
    /// Mark context as reviewed
    /// </summary>
    public async Task MarkContextFreshAsync(int projectId, string entityType, int entityId, int userId)
    {
        await _contextRepo.MarkContextFreshAsync(projectId, entityType, entityId, userId);
    }

    /// <summary>
    /// Get pending review requests
    /// </summary>
    public async Task<List<ContextReviewRequest>> GetPendingReviewRequestsAsync(int? userId = null)
    {
        return await _contextRepo.GetPendingReviewRequestsAsync(userId);
    }

    #endregion
}