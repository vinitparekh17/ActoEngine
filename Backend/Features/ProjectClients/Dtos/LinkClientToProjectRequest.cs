using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Features.ProjectClients.Dtos;

/// <summary>
/// Request DTO for linking a client to a project
/// </summary>
public class LinkClientToProjectRequest
{
    [Required(ErrorMessage = "Project ID is required")]
    public int ProjectId { get; set; }

    [Required(ErrorMessage = "Client ID is required")]
    public int ClientId { get; set; }
}

