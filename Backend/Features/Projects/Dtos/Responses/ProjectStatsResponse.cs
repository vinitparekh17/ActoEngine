namespace ActoEngine.WebApi.Features.Projects.Dtos.Responses;

public class ProjectStatsResponse
{
    public int TableCount { get; set; }
    public int SpCount { get; set; }
    public DateTime? LastSync { get; set; }
}

