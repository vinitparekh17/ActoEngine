using ActoEngine.WebApi.Features.Users;

namespace ActoEngine.WebApi.Features.Context;

/// <summary>
/// Represents context information for database entities (Tables, Columns, SPs)
/// </summary>
public class EntityContext
{
    public int ContextId { get; set; }
    public int ProjectId { get; set; }
    public required string EntityType { get; set; } // 'TABLE', 'COLUMN', 'SP', 'FUNCTION', 'VIEW'
    public int EntityId { get; set; }
    public required string EntityName { get; set; }

    // Core Context Fields
    public string? Purpose { get; set; }
    public string? BusinessImpact { get; set; }

    public int CriticalityLevel { get; set; } = 3;
    public string? BusinessDomain { get; set; }
    public string? Sensitivity { get; set; }
    public string? DataSource { get; set; }
    public string? ValidationRules { get; set; }
    public string? RetentionPolicy { get; set; }

    // SP-specific
    public string? DataFlow { get; set; }
    public string? Frequency { get; set; }
    public bool IsDeprecated { get; set; }
    public string? DeprecationReason { get; set; }
    public string? ReplacedBy { get; set; }

    // Metadata
    public DateTime? LastContextUpdate { get; set; }
    public int? ContextUpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Entity experts - who knows what
/// </summary>
public class EntityExpert
{
    public int ExpertId { get; set; }
    public int ProjectId { get; set; }
    public required string EntityType { get; set; }
    public int EntityId { get; set; }
    public int UserId { get; set; }
    public required string ExpertiseLevel { get; set; } // 'OWNER', 'EXPERT', 'FAMILIAR', 'CONTRIBUTOR'
    public string? Notes { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public int? AddedBy { get; set; }

    // Navigation
    public UserBasicInfo? User { get; set; }
}

/// <summary>
/// Track context changes for audit
/// </summary>
public class ContextHistory
{
    public int HistoryId { get; set; }
    public int ProjectId { get; set; }
    public required string EntityType { get; set; }
    public int EntityId { get; set; }
    public required string FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public int ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? ChangeReason { get; set; }
}

