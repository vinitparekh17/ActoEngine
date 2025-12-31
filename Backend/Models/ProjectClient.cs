using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Models
{
    /// <summary>
    /// Junction table entity representing the many-to-many relationship between Projects and Clients
    /// </summary>
    public class ProjectClient
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectClient"/> class with default property values.
        /// </summary>
        public ProjectClient()
        {
        }

        /// <summary>
        /// Initializes a new ProjectClient linking the specified project and client and recording creation metadata.
        /// </summary>
        /// <param name="projectId">The identifier of the project to link.</param>
        /// <param name="clientId">The identifier of the client to link.</param>
        /// <param name="createdBy">The identifier of the user who created the link.</param>
        public ProjectClient(int projectId, int clientId, int createdBy)
        {
            ProjectId = projectId;
            ClientId = clientId;
            CreatedBy = createdBy;
            CreatedAt = DateTime.UtcNow;
            IsActive = true;
        }

        public int ProjectClientId { get; set; }
        public int ProjectId { get; set; }
        public int ClientId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }
    }

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

    /// <summary>
    /// Response DTO with detailed project and client information
    /// </summary>
    public class ProjectClientDetailResponse
    {
        public int ProjectClientId { get; set; }
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public int ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}