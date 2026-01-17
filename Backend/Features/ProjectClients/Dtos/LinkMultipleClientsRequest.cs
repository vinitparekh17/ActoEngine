using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Features.ProjectClients.Dtos;

/// <summary>
/// Request DTO for linking multiple clients to a single project
/// </summary>
public class LinkMultipleClientsRequest
{
    [Required(ErrorMessage = "Project ID is required")]
    public int ProjectId { get; set; }

    [Required(ErrorMessage = "At least one client ID is required")]
    [MinLength(1, ErrorMessage = "At least one client ID is required")]
    public List<int> ClientIds { get; set; } = [];
}

