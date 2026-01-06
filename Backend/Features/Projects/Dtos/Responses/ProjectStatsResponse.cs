namespace ActoEngine.WebApi.Features.Project.Dtos.Responses;

public class ProjectStatsResponse
{
    public int TableCount { get; set; }
    public int SpCount { get; set; }
    public DateTime? LastSync { get; set; }
}

