namespace ActoEngine.WebApi.Models.Responses.Project;

public class ActivityItem
{
    public required string Type { get; set; }
    public required string Description { get; set; }
    public DateTime Timestamp { get; set; }
    public required string User { get; set; }
}

