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

public class StoredProcedureMetadata
{
    public required string ProcedureName { get; set; }
    public string? Definition { get; set; }
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
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsIdentity { get; set; }
    public bool IsForeignKey { get; set; }
    public string DefaultValue { get; set; } = string.Empty;
}