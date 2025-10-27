using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Models
{
    public class Project
    {
        public Project()
        {
        }

        public Project(string projectName, string databaseName, string connectionString, DateTime createdAt, int createdBy, string description = "", string databaseType = "SqlServer")
        {
            ProjectName = projectName;
            DatabaseName = databaseName;
            ConnectionString = connectionString;
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
        public string ConnectionString { get; set; } = string.Empty;
        public string? DatabaseType { get; set; } = "SqlServer";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }
    }

    public class VerifyConnectionRequest
    {
        [Required(ErrorMessage = "Server is required")]
        [StringLength(255, ErrorMessage = "Server name cannot exceed 255 characters")]
        public string Server { get; set; } = default!;

        [Required(ErrorMessage = "Database name is required")]
        [StringLength(128, ErrorMessage = "Database name cannot exceed 128 characters")]
        public string DatabaseName { get; set; } = default!;

        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, ErrorMessage = "Username cannot exceed 50 characters")]
        public string Username { get; set; } = default!;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, ErrorMessage = "Password cannot exceed 100 characters")]
        public string Password { get; set; } = default!;

        public int Port { get; set; } = 1433; // Default SQL Server port

        [StringLength(50, ErrorMessage = "Database type cannot exceed 50 characters")]
        public string DatabaseType { get; set; } = "SqlServer";
    }

    public class LinkProjectRequest
    {
        public int ProjectId { get; set; }

        [Required(ErrorMessage = "Project name is required")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Project name must be between 3 and 100 characters")]
        public string ProjectName { get; set; } = default!;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Database name is required")]
        [StringLength(128, ErrorMessage = "Database name cannot exceed 128 characters")]
        public string DatabaseName { get; set; } = default!;

        [Required(ErrorMessage = "Connection string is required")]
        [StringLength(1000, ErrorMessage = "Connection string cannot exceed 1000 characters")]
        public string ConnectionString { get; set; } = default!;

        [StringLength(50, ErrorMessage = "Database type cannot exceed 50 characters")]
        public string DatabaseType { get; set; } = "SqlServer";
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

    public class CreateProjectRequest
    {
        [Required(ErrorMessage = "Project name is required")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Project name must be between 3 and 100 characters")]
        public string ProjectName { get; set; } = default!;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Database name is required")]
        [StringLength(128, ErrorMessage = "Database name cannot exceed 128 characters")]
        public string DatabaseName { get; set; } = default!;

        [Required(ErrorMessage = "Connection string is required")]
        [StringLength(1000, ErrorMessage = "Connection string cannot exceed 1000 characters")]
        public string ConnectionString { get; set; } = default!;

        [StringLength(50, ErrorMessage = "Database type cannot exceed 50 characters")]
        public string DatabaseType { get; set; } = "SqlServer";
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
}