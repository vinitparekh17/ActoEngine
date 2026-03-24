using System.Text.Json.Serialization;

namespace ActoEngine.WebApi.Features.Patcher;

// ============================================
// Request / Response DTOs
// ============================================

/// <summary>
/// A single page-to-SP mapping discovered by the extension
/// </summary>
public class PageSpMapping
{
    /// <summary>Page name derived from JS file URL (e.g., "AutoVsPlanner")</summary>
    public required string PageName { get; set; }

    /// <summary>Domain/area derived from JS file URL path (e.g., "Reports")</summary>
    public required string DomainName { get; set; }

    /// <summary>ServiceName values found in the page's JS (e.g., ["REPORT_AUTO_VS_PLANNER_COMP_SUMMARY"])</summary>
    public required List<string> ServiceNames { get; set; }

    /// <summary>Raw myfilters string per ServiceName (for reference)</summary>
    public string? FiltersRaw { get; set; }

    /// <summary>Whether this is a new page (triggers menu + permission SQL generation)</summary>
    public bool IsNewPage { get; set; } = false;
}

/// <summary>
/// Request to check whether existing patches are stale
/// </summary>
public class PatchStatusRequest
{
    public int ProjectId { get; set; }
    public required List<PageSpMapping> PageMappings { get; set; }
}

/// <summary>
/// Staleness check response for a single page
/// </summary>
public class PagePatchStatus
{
    public required string PageName { get; set; }
    public required string DomainName { get; set; }
    public bool NeedsRegeneration { get; set; }
    public string? Reason { get; set; }
    public DateTime? LastPatchDate { get; set; }
    public DateTime? FileLastModified { get; set; }
}

/// <summary>
/// Request to generate a patch zip
/// </summary>
public class PatchGenerationRequest
{
    public int ProjectId { get; set; }
    public string? PatchName { get; set; }
    public required List<PageSpMapping> PageMappings { get; set; }
}

/// <summary>
/// Patch generation result
/// </summary>
public class PatchGenerationResponse
{
    public int PatchId { get; set; }
    public required string DownloadPath { get; set; }
    public required List<string> FilesIncluded { get; set; }
    public required List<string> Warnings { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class PatchHistoryPageResponse
{
    public required List<PatchHistoryRecord> Items { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

/// <summary>
/// Project patch configuration update
/// </summary>
public class PatchConfigRequest
{
    public string? ProjectRootPath { get; set; }
    public string? ViewDirPath { get; set; }
    public string? ScriptDirPath { get; set; }
    public string? PatchDownloadPath { get; set; }
}

// ============================================
// Internal models
// ============================================



/// <summary>
/// Patch history record from database
/// </summary>
public class PatchHistoryRecord
{
    public int PatchId { get; set; }
    public int ProjectId { get; set; }
    public required string PageName { get; set; }
    public required string DomainName { get; set; }
    public required string SpNames { get; set; }
    public string? PatchName { get; set; }
    public bool IsNewPage { get; set; }
    public string? PatchFilePath { get; set; }
    public string? PatchSignature { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int? GeneratedBy { get; set; }
    public required string Status { get; set; }
    public List<PatchPageEntry> Pages { get; set; } = [];
}

/// <summary>
/// A single page entry in a multi-page patch
/// </summary>
public class PatchPageEntry
{
    public required string DomainName { get; set; }
    public required string PageName { get; set; }
    public bool IsNewPage { get; set; }
}

/// <summary>
/// Flat row returned by the left-join history query (internal use only)
/// </summary>
internal class PatchHistoryFlatRow
{
    public int PatchId { get; set; }
    public int ProjectId { get; set; }
    public required string PageName { get; set; }
    public required string DomainName { get; set; }
    public required string SpNames { get; set; }
    public string? PatchName { get; set; }
    public bool IsNewPage { get; set; }
    public string? PatchFilePath { get; set; }
    public string? PatchSignature { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int? GeneratedBy { get; set; }
    public required string Status { get; set; }
    // Joined columns from PatchHistoryPages
    public string? PageDomain { get; set; }
    public string? PagePage { get; set; }
    public bool PageIsNew { get; set; }
}

internal class PatchHistoryPageFlatRow
{
    public int PatchId { get; set; }
    public required string DomainName { get; set; }
    public required string PageName { get; set; }
    public bool IsNewPage { get; set; }
}

internal class LatestPatchByPageFlatRow
{
    public required string RequestDomainName { get; set; }
    public required string RequestPageName { get; set; }
    public int PatchId { get; set; }
    public int ProjectId { get; set; }
    public required string PageName { get; set; }
    public required string DomainName { get; set; }
    public required string SpNames { get; set; }
    public string? PatchName { get; set; }
    public bool IsNewPage { get; set; }
    public string? PatchFilePath { get; set; }
    public string? PatchSignature { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int? GeneratedBy { get; set; }
    public required string Status { get; set; }
}

public class ApprovedSpByPageRow
{
    public required string DomainName { get; set; }
    public required string PageName { get; set; }
    public required string StoredProcedure { get; set; }
    public required string MappingType { get; set; }
}

/// <summary>
/// Project patch config fields
/// </summary>
public class ProjectPatchConfig
{
    public string? ProjectRootPath { get; set; }
    public string? ViewDirPath { get; set; }
    public string? ScriptDirPath { get; set; }
    public string? PatchDownloadPath { get; set; }
}
