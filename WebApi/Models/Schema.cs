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