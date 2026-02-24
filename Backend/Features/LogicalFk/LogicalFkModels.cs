namespace ActoEngine.WebApi.Features.LogicalFk;

/// <summary>
/// Database entity matching the LogicalForeignKeys table
/// </summary>
public class LogicalForeignKey
{
    public int LogicalFkId { get; set; }
    public int ProjectId { get; set; }
    public int SourceTableId { get; set; }
    public string SourceColumnIds { get; set; } = "[]"; // JSON array
    public int TargetTableId { get; set; }
    public string TargetColumnIds { get; set; } = "[]"; // JSON array
    public string DiscoveryMethod { get; set; } = "MANUAL";
    public decimal ConfidenceScore { get; set; } = 1.0m;
    public string Status { get; set; } = "SUGGESTED";
    public int? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
}

/// <summary>
/// Enriched DTO with table/column names for API responses
/// </summary>
public class LogicalFkDto
{
    public int LogicalFkId { get; set; }
    public int ProjectId { get; set; }

    // Source
    public int SourceTableId { get; set; }
    public string SourceTableName { get; set; } = string.Empty;
    public List<int> SourceColumnIds { get; set; } = [];
    public List<string> SourceColumnNames { get; set; } = [];

    // Target
    public int TargetTableId { get; set; }
    public string TargetTableName { get; set; } = string.Empty;
    public List<int> TargetColumnIds { get; set; } = [];
    public List<string> TargetColumnNames { get; set; } = [];

    // Metadata
    public string DiscoveryMethod { get; set; } = "MANUAL";
    public decimal ConfidenceScore { get; set; } = 1.0m;
    public string Status { get; set; } = "SUGGESTED";
    public int? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Auto-detected candidate before it's persisted
/// </summary>
public class LogicalFkCandidate
{
    public int SourceTableId { get; set; }
    public required string SourceTableName { get; set; }
    public int SourceColumnId { get; set; }
    public required string SourceColumnName { get; set; }
    public required string SourceDataType { get; set; }

    public int TargetTableId { get; set; }
    public required string TargetTableName { get; set; }
    public int TargetColumnId { get; set; }
    public required string TargetColumnName { get; set; }
    public required string TargetDataType { get; set; }

    public decimal ConfidenceScore { get; set; }
    public ConfidenceBand ConfidenceBand { get; set; }
    public required string Reason { get; set; }
    public bool IsAmbiguous { get; set; }

    // Multi-strategy evidence
    public List<string> DiscoveryMethods { get; set; } = [];
    public List<string> SpEvidence { get; set; } = [];
    public int MatchCount { get; set; }

    /// <summary>
    /// Canonical key for dedup: "srcTableId:srcColId→tgtTableId:tgtColId"
    /// </summary>
    public string CanonicalKey => $"{SourceTableId}:{SourceColumnId}\u2192{TargetTableId}:{TargetColumnId}";
}

/// <summary>
/// Request to create a logical FK manually
/// </summary>
public class CreateLogicalFkRequest
{
    public int SourceTableId { get; set; }
    public required List<int> SourceColumnIds { get; set; }
    public int TargetTableId { get; set; }
    public required List<int> TargetColumnIds { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Request to update status (confirm/reject) with optional notes
/// </summary>
public class UpdateLogicalFkStatusRequest
{
    public string? Notes { get; set; }
}

/// <summary>
/// Physical foreign key details
/// </summary>
public class PhysicalFkDto
{
    public required string ForeignKeyName { get; set; }
    public required string SourceTableName { get; set; }
    public required string SourceColumnName { get; set; }
    public required string TargetTableName { get; set; }
    public required string TargetColumnName { get; set; }
    public string OnDeleteAction { get; set; } = "NO ACTION";
    public string OnUpdateAction { get; set; } = "NO ACTION";
}

/// <summary>
/// Per-candidate signal flags consumed by ConfidenceCalculator
/// </summary>
public class DetectionSignals
{
    public bool NamingDetected { get; set; }
    public bool SpJoinDetected { get; set; }
    public bool Corroborated => NamingDetected && SpJoinDetected;
    public bool TypeMatch { get; set; }
    public bool HasIdSuffix { get; set; }
    public int SpCount { get; set; }
}

/// <summary>
/// Configurable scoring parameters (bindable from appsettings.json)
/// </summary>
public class DetectionConfig
{
    // Base scores
    public decimal NamingBaseScore { get; set; } = 0.60m;
    public decimal SpJoinBaseScore { get; set; } = 0.50m;

    // Bonuses/penalties
    public decimal NamingBonus { get; set; } = 0.15m;
    public decimal TypeMatchBonus { get; set; } = 0.10m;
    public decimal TypeMismatchPenalty { get; set; } = -0.10m;
    public decimal RepetitionBonusPerSp { get; set; } = 0.05m;
    public decimal RepetitionBonusCap { get; set; } = 0.20m;
    public decimal CorroborationBonus { get; set; } = 0.25m;

    // Hard caps
    public decimal SpOnlyCap { get; set; } = 0.85m;
    public decimal NamingOnlyCap { get; set; } = 0.80m;
    public decimal TypeMismatchCap { get; set; } = 0.55m;
}

/// <summary>
/// Confidence breakdown for transparency/debugging
/// </summary>
public class ConfidenceResult
{
    public decimal BaseScore { get; set; }
    public decimal NamingBonus { get; set; }
    public decimal TypeBonus { get; set; }
    public decimal RepetitionBonus { get; set; }
    public decimal CorroborationBonus { get; set; }
    public decimal RawConfidence { get; set; }
    public decimal FinalConfidence { get; set; }
    public string[] CapsApplied { get; set; } = [];
}

/// <summary>
/// Detection staleness metadata from Projects table
/// </summary>
public class DetectionMetadata
{
    public DateTime? LastDetectionRunAt { get; set; }
    public string? DetectionAlgorithmVersion { get; set; }
    public DateTime? LastSyncAttempt { get; set; }
}

