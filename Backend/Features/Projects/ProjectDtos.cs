namespace ActoEngine.WebApi.Models;

public class Project
{
    public Project() { }

    public Project(string projectName, string databaseName, DateTime createdAt, int createdBy, string description = "", string databaseType = "SqlServer")
    {
        ProjectName = projectName;
        DatabaseName = databaseName;
        Description = description;
        DatabaseType = databaseType;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        IsActive = true;
    }

    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string? DatabaseType { get; set; } = "SqlServer";
    public bool IsActive { get; set; } = true;
    public bool IsLinked { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public int CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
}

public class ProjectResponse
{
    public int ProjectId { get; set; }
    public required string Message { get; set; }
    public int SyncJobId { get; internal set; }
}

public class ConnectionResponse
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ServerVersion { get; set; }
    public DateTime TestedAt { get; set; } = DateTime.UtcNow;
    public List<string> Errors { get; set; } = [];
}

public class ProjectStatsResponse
{
    public int TableCount { get; set; }
    public int SpCount { get; set; }
    public DateTime? LastSync { get; set; }
}

public class ActivityItem
{
    public required string Type { get; set; }
    public required string Description { get; set; }
    public DateTime Timestamp { get; set; }
    public required string User { get; set; }
}
