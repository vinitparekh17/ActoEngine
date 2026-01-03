namespace ActoEngine.Domain.Entities;

public class Project
{
    public Project()
    {
    }

    public Project(string projectName, string databaseName, DateTime createdAt, int createdBy, string description = "", string databaseType = "SqlServer")
    {
        if (string.IsNullOrWhiteSpace(projectName))
            throw new ArgumentException("Project name cannot be empty.", nameof(projectName));

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));

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

