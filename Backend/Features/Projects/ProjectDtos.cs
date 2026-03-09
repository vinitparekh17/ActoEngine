using System.Text.Json.Serialization;

namespace ActoEngine.WebApi.Features.Projects;

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

 [JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResyncEntityType
{
    TABLE,
    SP
}

public class ResyncEntityItem
{
    public ResyncEntityType EntityType { get; set; }
    public required string SchemaName { get; set; }
    public required string EntityName { get; set; }
}

public class ResyncEntitiesRequest
{
    public int ProjectId { get; set; }
    public required string ConnectionString { get; set; }
    public required List<ResyncEntityItem> Entities { get; set; }
}

public class ProjectResponse
{
    public int ProjectId { get; set; }
    public required string Message { get; set; }
    public int SyncJobId { get; internal set; }
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

public class SchemaDiffResponse
{
    public EntityDiffCategory Tables { get; set; } = new();
    public EntityDiffCategory StoredProcedures { get; set; } = new();
}

public class EntityDiffCategory
{
    public List<DiffEntityItem> Added { get; set; } = [];
    public List<DiffEntityItem> Removed { get; set; } = [];
    public List<DiffEntityItem> Modified { get; set; } = [];
}

public class DiffEntityItem
{
    public string SchemaName { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? Reason { get; set; } // e.g., "definition_changed"
}

public class ApplyDiffRequest
{
    public int ProjectId { get; set; }
    public required string ConnectionString { get; set; }
    public List<ResyncEntityItem> AddEntities { get; set; } = [];
    public List<ResyncEntityItem> RemoveEntities { get; set; } = [];
    public List<ResyncEntityItem> UpdateEntities { get; set; } = [];
}
