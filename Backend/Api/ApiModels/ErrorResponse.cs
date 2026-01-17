namespace ActoEngine.WebApi.Api.ApiModels;

public class ErrorResponse(string error)
{
    public string Error { get; set; } = error;
    public string? Message { get; set; }
    public string? Timestamp { get; set; }
    public string? Path { get; set; }
}
