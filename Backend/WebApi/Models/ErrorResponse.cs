namespace ActoEngine.WebApi.Models;

public class ErrorResponse
{
    public string Error { get; set; } = "Unauthorized";
    public string Message { get; set; } = default!;
    public string Timestamp { get; set; } = default!;
    public string Path { get; set; } = default!;
}
