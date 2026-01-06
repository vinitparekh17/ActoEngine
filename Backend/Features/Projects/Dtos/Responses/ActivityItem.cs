namespace ActoEngine.WebApi.Features.Project.Dtos.Responses;

public class ActivityItem
{
    public required string Type { get; set; }
    public required string Description { get; set; }
    public DateTime Timestamp { get; set; }
    public required string User { get; set; }
}

