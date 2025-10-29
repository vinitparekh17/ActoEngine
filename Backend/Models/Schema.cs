namespace ActoEngine.WebApi.Models;

public class ColumnMetadata
{
    public required string ColumnName { get; set; }
    public required string DataType { get; set; }
    public short MaxLength { get; set; }
    public byte Precision { get; set; }
    public byte Scale { get; set; }
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public int ColumnOrder { get; set; }
}

public class ForeignKeyMetadata
{
    public int ForeignKeyId { get; set; }
    public int TableId { get; set; }
    public int ColumnId { get; set; }
    public int ReferencedTableId { get; set; }
    public int ReferencedColumnId { get; set; }
    public string OnDeleteAction { get; set; } = "NO ACTION";
    public string OnUpdateAction { get; set; } = "NO ACTION";
}

public class StoredProcedureMetadata
{
    public required string ProcedureName { get; set; }
    public string? Definition { get; set; }
}

// Represents a scanned foreign key from the target DB using names, not IDs
public class ForeignKeyScanResult
{
    public required string TableName { get; set; }
    public required string ColumnName { get; set; }
    public required string ReferencedTable { get; set; }
    public required string ReferencedColumn { get; set; }
    public string OnDeleteAction { get; set; } = "NO ACTION";
    public string OnUpdateAction { get; set; } = "NO ACTION";
}

public class SyncStatus
{
    public required string Status { get; set; }
    public int SyncProgress { get; set; }
    public DateTime? LastSyncAttempt { get; set; }
}

public class SyncStatusResponse
{
    public required int ProjectId { get; set; }
    public required string Status { get; set; }
    public int SyncProgress { get; set; }
    public DateTime? LastSyncAttempt { get; set; }
}

public class TableSchemaRequest
{
    public int ProjectId { get; set; }
    public required string TableName { get; set; }
}

public class TableSchemaResponse
{
    public required string TableName { get; set; }
    public required string SchemaName { get; set; }
    public required List<ColumnSchema> Columns { get; set; }
    public required List<string> PrimaryKeys { get; set; }
}

public class ColumnSchema
{
    public required string SchemaName { get; set; }
    public required string ColumnName { get; set; }
    public required string DataType { get; set; }
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsIdentity { get; set; }
    public bool IsForeignKey { get; set; }
    public string DefaultValue { get; set; } = string.Empty;

    // Foreign Key Information
    public ForeignKeyInfo? ForeignKeyInfo { get; set; }
}

/// <summary>
/// Foreign key relationship information
/// </summary>
public class ForeignKeyInfo
{
    public required string ReferencedTable { get; set; }
    public required string ReferencedColumn { get; set; }
    public string? DisplayColumn { get; set; } // Column to show in dropdown (nullable for user to set)
    public string OnDeleteAction { get; set; } = "NO ACTION";
    public string OnUpdateAction { get; set; } = "NO ACTION";
}


/// <summary>
/// Represents a node in the database tree structure
/// </summary>
public class TreeNode
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public List<TreeNode>? Children { get; set; }
}

/// <summary>
/// Database tree response containing the complete tree structure
/// </summary>
public class DatabaseTreeResponse
{
    public required TreeNode Root { get; set; }
}
// DTOs for stored metadata
public class TableMetadataDto
{
    public int TableId { get; set; }
    public int ProjectId { get; set; }
    public required string TableName { get; set; }
    public string? SchemaName { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ColumnMetadataDto
{
    public int ColumnId { get; set; }
    public int TableId { get; set; }
    public required string ColumnName { get; set; }
    public required string DataType { get; set; }
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public string? DefaultValue { get; set; }
    public string? Description { get; set; }
    public int? ColumnOrder { get; set; }
}

public class StoredProcedureMetadataDto
{
    public int SpId { get; set; }
    public int ProjectId { get; set; }
    public int ClientId { get; set; }
    public required string ProcedureName { get; set; }
    public required string Definition { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class DatabaseTableInfo
{
    public required string SchemaName { get; set; }
    public required string TableName { get; set; }
    public string? Description { get; set; } // Optional description for UI/metadata
}