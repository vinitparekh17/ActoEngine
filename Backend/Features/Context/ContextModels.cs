using System.ComponentModel.DataAnnotations;
using ActoEngine.Domain.Entities;

namespace ActoEngine.WebApi.Models;

// ==================== DTOs ====================


/// <summary>
/// Request to save/update context
/// </summary>
public class SaveContextRequest
{
    public string? Purpose { get; set; }
    public string? BusinessImpact { get; set; }
    public string? DataOwner { get; set; }

    [Range(1, 5)]
    public int? CriticalityLevel { get; set; }

    public string? BusinessDomain { get; set; }
    public string? Sensitivity { get; set; }
    public string? DataSource { get; set; }
    public string? ValidationRules { get; set; }
    public string? RetentionPolicy { get; set; }

    // SP-specific
    public string? DataFlow { get; set; }
    public string? Frequency { get; set; }
    public bool? IsDeprecated { get; set; }
    public string? DeprecationReason { get; set; }
    public string? ReplacedBy { get; set; }

    // Experts
    public List<int>? ExpertUserIds { get; set; }
}

/// <summary>
/// Context response with all related data
/// </summary>
public class ContextResponse
{
    public EntityContext? Context { get; set; }
    public List<EntityExpert> Experts { get; set; } = [];
    public ContextSuggestions? Suggestions { get; set; }
    public int CompletenessScore { get; set; }
    public bool IsStale { get; set; }
    public int DependencyCount { get; set; }
}

/// <summary>
/// Smart suggestions for context
/// </summary>
public class ContextSuggestions
{
    public string? Purpose { get; set; }
    public string? BusinessDomain { get; set; }
    public string? Sensitivity { get; set; }
    public string? ValidationRules { get; set; }
    public List<UserSuggestion> PotentialOwners { get; set; } = [];
    public List<UserSuggestion> PotentialExperts { get; set; } = [];
}

public class UserSuggestion
{
    public int UserId { get; set; }
    public required string Username { get; set; }
    public string? FullName { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Request to add expert
/// </summary>
public class AddExpertRequest
{
    [Required]
    public int UserId { get; set; }

    [Required]
    public required string ExpertiseLevel { get; set; } // 'OWNER', 'EXPERT', 'FAMILIAR'

    public string? Notes { get; set; }
}

/// <summary>
/// Bulk context entry for seeding
/// </summary>
public class BulkContextEntry
{
    [Required]
    public required string EntityType { get; set; }

    [Required]
    public int EntityId { get; set; }

    [Required]
    public required string EntityName { get; set; }

    [Required]
    public required SaveContextRequest Context { get; set; }
}

public class BulkImportResult
{
    public required string EntityName { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Request model for creating a review request for an entity's context
/// </summary>
public class CreateReviewRequestModel
{
    [Required]
    public required string EntityType { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int EntityId { get; set; }

    /// <summary>
    /// Optional: Assign the review to a specific user
    /// </summary>
    public int? AssignedTo { get; set; }

    /// <summary>
    /// Optional: Reason or context for the review
    /// </summary>
    [StringLength(500)]
    public string? Reason { get; set; }
}

/// <summary>
/// Context coverage statistics
/// </summary>
public class ContextCoverageStats
{
    public required string EntityType { get; set; }
    public int Total { get; set; }
    public int Documented { get; set; }
    public decimal CoveragePercentage { get; set; }
    public decimal? AvgCompleteness { get; set; }
}

/// <summary>
/// Minimal request for quick context entry
/// </summary>
public class QuickSaveRequest
{
    [Required]
    public required string EntityType { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int EntityId { get; set; }

    [Required]
    [StringLength(1000, MinimumLength = 10)]
    public required string Purpose { get; set; }

    [Range(1, 5)]
    public int? CriticalityLevel { get; set; }
}

public class QuickSaveResponse
{
    public required EntityContext Context { get; set; }
    public int CompletenessScore { get; set; }
    public required string Message { get; set; }
}

public class ContextGap
{
    public required string EntityType { get; set; }
    public int EntityId { get; set; }
    public required string EntityName { get; set; }
    [Range(1, 5)]
    public int Priority { get; set; } // 1-5 (based on references, criticality)
    public string? Reason { get; set; }
    public int? DependencyCount { get; set; }
}

/// <summary>
/// User expertise item showing entities where a user has expertise
/// </summary>
public class UserExpertiseItem
{
    public required string EntityType { get; set; }
    public int EntityId { get; set; }
    public required string ExpertiseLevel { get; set; }
    public required string EntityName { get; set; }
    public string? Purpose { get; set; }
    public string? BusinessDomain { get; set; }
}

/// <summary>
/// Entity with stale context that needs review
/// </summary>
public class StaleContextEntity
{
    public required string EntityType { get; set; }
    public int EntityId { get; set; }
    public required string EntityName { get; set; }
    public DateTime? LastContextUpdate { get; set; }
    public DateTime? LastReviewedAt { get; set; }
    public int DaysSinceUpdate { get; set; }
}

/// <summary>
/// Top documented entity with completeness metrics
/// </summary>
public class TopDocumentedEntity
{
    public required string EntityType { get; set; }
    public int EntityId { get; set; }
    public required string EntityName { get; set; }
    public string? Purpose { get; set; }
    public string? BusinessDomain { get; set; }
    public string? DataOwner { get; set; }
    public int CriticalityLevel { get; set; }
    public int CompletenessScore { get; set; }
    public int ExpertCount { get; set; }
}

/// <summary>
/// Critical undocumented entity that needs attention
/// </summary>
public class CriticalUndocumentedEntity
{
    public required string EntityType { get; set; }
    public int EntityId { get; set; }
    public required string EntityName { get; set; }
    public required string Reason { get; set; }
    public int ReferenceCount { get; set; }
}