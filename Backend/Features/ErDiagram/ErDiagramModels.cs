namespace ActoEngine.WebApi.Features.ErDiagram;

/// <summary>
/// Full ER diagram response: nodes (tables) and edges (relationships)
/// </summary>
public class ErDiagramResponse
{
    public required int FocusTableId { get; set; }
    public required List<ErNode> Nodes { get; set; } = [];
    public required List<ErEdge> Edges { get; set; } = [];
}

/// <summary>
/// A table node in the ER diagram
/// </summary>
public class ErNode
{
    public required int TableId { get; set; }
    public required string TableName { get; set; }
    public string? SchemaName { get; set; }
    public required List<ErColumn> Columns { get; set; } = [];
    public int Depth { get; set; } // Distance from focus table (0 = focus)
}

/// <summary>
/// A column within a table node
/// </summary>
public class ErColumn
{
    public required int ColumnId { get; set; }
    public required string ColumnName { get; set; }
    public required string DataType { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public bool IsNullable { get; set; }
}

/// <summary>
/// A relationship edge between two tables
/// </summary>
public class ErEdge
{
    public required string Id { get; set; }
    public required int SourceTableId { get; set; }
    public required int SourceColumnId { get; set; }
    public required string SourceColumnName { get; set; }
    public required int TargetTableId { get; set; }
    public required int TargetColumnId { get; set; }
    public required string TargetColumnName { get; set; }

    /// <summary>
    /// "PHYSICAL" for real FKs, "LOGICAL" for logical FKs
    /// </summary>
    public required string RelationshipType { get; set; }

    /// <summary>
    /// Only for logical edges: SUGGESTED | CONFIRMED | REJECTED
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Only for logical edges: confidence score 0-1
    /// </summary>
    public decimal? ConfidenceScore { get; set; }

    /// <summary>
    /// Only for logical edges: the LogicalForeignKeyId for confirm/reject actions
    /// </summary>
    public int? LogicalFkId { get; set; }

    // Metadata for logical edges
    public string? DiscoveryMethod { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public int? ConfirmedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class TableInfo
{
    public int TableId { get; set; }
    public string TableName { get; set; } = "";
    public string? SchemaName { get; set; }
}

public class ColumnInfo
{
    public int TableId { get; set; }
    public int ColumnId { get; set; }
    public string ColumnName { get; set; } = "";
    public string DataType { get; set; } = "";
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public bool IsNullable { get; set; }
}

public class RawFkEdge
{
    public int SourceTableId { get; set; }
    public int SourceColumnId { get; set; }
    public string SourceColumnName { get; set; } = "";
    public int TargetTableId { get; set; }
    public int TargetColumnId { get; set; }
    public string TargetColumnName { get; set; } = "";
}

public class RawLogicalFkEdge : RawFkEdge
{
    public int LogicalForeignKeyId { get; set; }
    public string Status { get; set; } = "";
    public decimal ConfidenceScore { get; set; }
    public string DiscoveryMethod { get; set; } = "";
    public DateTime? ConfirmedAt { get; set; }
    public int? ConfirmedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
