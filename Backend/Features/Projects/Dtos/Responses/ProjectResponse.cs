namespace ActoEngine.WebApi.Features.Projects.Dtos.Responses;

public class ProjectResponse
{
    public int ProjectId { get; set; }
    public required string Message { get; set; }
    public int SyncJobId { get; internal set; }
}

