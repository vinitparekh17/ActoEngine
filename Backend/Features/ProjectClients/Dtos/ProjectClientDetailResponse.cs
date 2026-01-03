namespace ActoEngine.WebApi.Models.Responses.ProjectClient;

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

