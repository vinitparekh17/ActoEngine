namespace ActoEngine.WebApi.Features.ProjectClients.Dtos;

/// <summary>
/// Response DTO for project-client link operations
/// </summary>
public class ProjectClientResponse
{
    public int ProjectClientId { get; set; }
    public int ProjectId { get; set; }
    public int ClientId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
}

