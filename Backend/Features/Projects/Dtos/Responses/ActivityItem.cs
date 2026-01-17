namespace ActoEngine.WebApi.Features.Projects.Dtos.Responses;

public class ActivityItem
{
    public required string Type { get; set; }
    public required string Description { get; set; }
    public DateTime Timestamp { get; set; }
    public required string User { get; set; }
}

