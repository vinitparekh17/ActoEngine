using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Features.ProjectClients.Dtos;

/// <summary>
/// Request DTO for linking a single client to multiple projects
/// </summary>
public class LinkClientToMultipleProjectsRequest
{
    [Required(ErrorMessage = "Client ID is required")]
    public int ClientId { get; set; }

    [Required(ErrorMessage = "At least one project ID is required")]
    [MinLength(1, ErrorMessage = "At least one project ID is required")]
    public List<int> ProjectIds { get; set; } = [];
}

