namespace ActoEngine.WebApi.Features.Projects.Dtos.Responses;

/// <summary>
/// Public DTO for Project
/// </summary>
public class PublicProjectDto
{
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string? DatabaseType { get; set; } = "SqlServer";
    public bool IsActive { get; set; }
    public bool IsLinked { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
    public string? SyncStatus { get; set; }
    public int SyncProgress { get; set; }
    public DateTime? LastSyncAttempt { get; set; }
}

