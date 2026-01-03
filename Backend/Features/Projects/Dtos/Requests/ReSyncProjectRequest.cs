using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Models.Requests.Project;

public class ReSyncProjectRequest
{
    [Required(ErrorMessage = "Project ID is required")]
    public int ProjectId { get; set; }

    [Required(ErrorMessage = "Connection string is required")]
    [StringLength(1000, ErrorMessage = "Connection string cannot exceed 1000 characters")]
    public string ConnectionString { get; set; } = default!;
}

