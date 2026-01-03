namespace ActoEngine.WebApi.Models.Responses.Auth;

public class ProtectedResourceResponse
{
    public required string Message { get; set; }
    public int? UserId { get; set; }
    public string? TokenType { get; set; }
    public bool IsAuthenticated { get; set; }
    public DateTime AccessTime { get; set; }
    public required string RequestId { get; set; }
}

