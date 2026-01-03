namespace ActoEngine.Domain.Entities;

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

