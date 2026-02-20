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
    public required string Reason { get; set; } // Human-readable explanation
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
