namespace ActoEngine.WebApi.Features.Projects.Dtos.Responses;

public class ConnectionResponse
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ServerVersion { get; set; }
    public DateTime TestedAt { get; set; } = DateTime.UtcNow;
    public List<string> Errors { get; set; } = [];
}

