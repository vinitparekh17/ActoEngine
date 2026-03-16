namespace ActoEngine.WebApi.Features.Patcher;

public static class PageMappingConstants
{
    public const string SourceExtension = "extension";
    public const string SourceSqlAnalysis = "sql-analysis";
    public const string SourceManual = "manual";

    public const string StatusCandidate = "candidate";
    public const string StatusApproved = "approved";
    public const string StatusIgnored = "ignored";

    public const string MappingTypePageSpecific = "page_specific";
    public const string MappingTypeShared = "shared";

    public const string BulkActionApprove = "approve";
    public const string BulkActionIgnore = "ignore";

    public static readonly ISet<string> ValidSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        SourceExtension,
        SourceSqlAnalysis,
        SourceManual
    };

    public static readonly ISet<string> ValidStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        StatusCandidate,
        StatusApproved,
        StatusIgnored
    };

    public static readonly ISet<string> ValidMappingTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        MappingTypePageSpecific,
        MappingTypeShared
    };

    public static readonly ISet<string> ValidBulkActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        BulkActionApprove,
        BulkActionIgnore
    };

    public static string NormalizeSource(string value) => value.Trim().ToLowerInvariant();
    public static string NormalizeStatus(string value) => value.Trim().ToLowerInvariant();
    public static string NormalizeMappingType(string value) => value.Trim().ToLowerInvariant();
    public static string NormalizeBulkAction(string value) => value.Trim().ToLowerInvariant();
}

public class MappingDetectionRequest
{
    public required string Page { get; set; }
    public required string DomainName { get; set; }
    public required string StoredProcedure { get; set; }
    public double? Confidence { get; set; }
    public required string Source { get; set; }
}

public class PageMappingDto
{
    public int MappingId { get; set; }
    public int ProjectId { get; set; }
    public required string DomainName { get; set; }
    public required string PageName { get; set; }
    public required string StoredProcedure { get; set; }
    public double? Confidence { get; set; }
    public required string Source { get; set; }
    public required string Status { get; set; }
    public required string MappingType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class UpdateMappingRequest
{
    public string? Status { get; set; }
    public string? MappingType { get; set; }
    public string? DomainName { get; set; }
    public string? PageName { get; set; }
    public string? StoredProcedure { get; set; }
}

public class BulkMappingActionRequest
{
    public required List<int> Ids { get; set; }
    public required string Action { get; set; }
}

public readonly record struct MappingUpsertResult(int Received, int UniqueCount);

public class ApprovedSpDto
{
    public required string StoredProcedure { get; set; }
    public required string MappingType { get; set; }
}
