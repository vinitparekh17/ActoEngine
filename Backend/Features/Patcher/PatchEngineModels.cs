namespace ActoEngine.WebApi.Features.Patcher;

public sealed class PatchManifest
{
    public int ProjectId { get; set; }
    public required List<PatchManifestPage> Pages { get; set; }
    public required List<PatchProcedureSnapshot> Procedures { get; set; }
    public required List<PatchTableSnapshot> Tables { get; set; }
    public required List<string> Warnings { get; set; }
    public required List<string> BlockingIssues { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
}

public sealed class PatchManifestPage
{
    public required string DomainName { get; set; }
    public required string PageName { get; set; }
    public bool IsNewPage { get; set; }
}

public sealed class PatchProcedureSnapshot
{
    public int SpId { get; set; }
    public required string ProcedureName { get; set; }
    public string SchemaName { get; set; } = "dbo";
    public required string Definition { get; set; }
    public bool IsShared { get; set; }
    public bool HasDynamicSql { get; set; }
    public List<int> DependencyProcedureIds { get; set; } = [];
    public List<int> TableIds { get; set; } = [];
    public List<int> ColumnIds { get; set; } = [];
}

public sealed class PatchTableSnapshot
{
    public int TableId { get; set; }
    public required string TableName { get; set; }
    public string SchemaName { get; set; } = "dbo";
    public required List<PatchColumnSnapshot> Columns { get; set; }
    public required List<PatchIndexSnapshot> Indexes { get; set; }
    public required List<PatchForeignKeySnapshot> ForeignKeys { get; set; }
    public required List<string> RequiredColumnNames { get; set; }
}

public sealed class PatchColumnSnapshot
{
    public int ColumnId { get; set; }
    public required string ColumnName { get; set; }
    public required string DataType { get; set; }
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsIdentity { get; set; }
    public string? DefaultValue { get; set; }
}

public sealed class PatchIndexSnapshot
{
    public required string IndexName { get; set; }
    public bool IsUnique { get; set; }
    public bool IsPrimaryKey { get; set; }
    public required List<PatchIndexColumnSnapshot> Columns { get; set; }
}

public sealed class PatchIndexColumnSnapshot
{
    public int ColumnId { get; set; }
    public required string ColumnName { get; set; }
    public int ColumnOrder { get; set; }
}

public sealed class PatchForeignKeySnapshot
{
    public required string ColumnName { get; set; }
    public required string ReferencedTableName { get; set; }
    public string ReferencedSchemaName { get; set; } = "dbo";
    public required string ReferencedColumnName { get; set; }
    public string? ForeignKeyName { get; set; }
    public string OnDeleteAction { get; set; } = "NO ACTION";
    public string OnUpdateAction { get; set; } = "NO ACTION";
}
public sealed class PatchArchiveArtifacts
{
    public required string CompatibilitySql { get; set; }
    public required string UpdateSql { get; set; }
    public required string RollbackSql { get; set; }
    public required string ManifestJson { get; set; }
}
