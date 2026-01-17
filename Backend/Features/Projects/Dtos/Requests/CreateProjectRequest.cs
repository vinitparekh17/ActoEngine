using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Features.Projects.Dtos.Requests;

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

    [StringLength(50, ErrorMessage = "Database type cannot exceed 50 characters")]
    public string DatabaseType { get; set; } = "SqlServer";
}

