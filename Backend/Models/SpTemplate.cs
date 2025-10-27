namespace ActoEngine.WebApi.Models;

public enum SpType
{
    Cud,       // Create, Update, Delete in ONE SP
    Select     // Select with filters / reports
}

public class SpGenerationRequest
{
    public int ProjectId { get; set; }
    public required string TableName { get; set; }
    public SpType Type { get; set; }
    public required List<SpColumnConfig> Columns { get; set; }
    public required CudSpOptions CudOptions { get; set; }
    public required SelectSpOptions SelectOptions { get; set; }
}

public class CudSpOptions
{
    public string SpPrefix { get; set; } = "usp";
    public bool IncludeErrorHandling { get; set; } = true;
    public bool IncludeTransaction { get; set; } = true;
    public string ActionParamName { get; set; } = "Action"; // 'C', 'U', 'D'
}

public class SelectSpOptions
{
    public string SpPrefix { get; set; } = "usp";
    public List<FilterColumn> Filters { get; set; } = [];
    public List<string> OrderByColumns { get; set; } = [];
    public bool IncludePagination { get; set; } = false;
}

public class FilterColumn
{
    public required string ColumnName { get; set; }
    public FilterOperator Operator { get; set; }
    public bool IsOptional { get; set; } = true; // NULL means skip filter
}

public enum FilterOperator
{
    Equals,
    Like,
    GreaterThan,
    LessThan,
    Between,
    In
}

public class SpColumnConfig
{
    public required string ColumnName { get; set; }
    public required string DataType { get; set; }
    public int? MaxLength { get; set; }
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsIdentity { get; set; }
    public bool IncludeInCreate { get; set; } = true;
    public bool IncludeInUpdate { get; set; } = true;
    public required string DefaultValue { get; set; }
}

public class GeneratedSpResponse
{
    public required string TableName { get; set; }
    public SpType Type { get; set; }
    public GeneratedSpItem? StoredProcedure { get; set; }
    public required List<string> Warnings { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class GeneratedSpItem
{
    public required string SpName { get; set; }
    public required string SpType { get; set; } // "CUD" or "Select"
    public required string Code { get; set; }
    public required string FileName { get; set; }
    public required string Description { get; set; }
}

// Quick generate simplified
public class QuickGenerateRequest
{
    public int ProjectId { get; set; }
    public required string TableName { get; set; }
    public SpType Type { get; set; }
    public required CudSpOptions CudOptions { get; set; }
    public required SelectSpOptions SelectOptions { get; set; }
}