namespace ActoEngine.WebApi.Features.Projects.Dtos.Responses;

public class ConnectionResponse
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ServerVersion { get; set; }
    public DateTime TestedAt { get; set; } = DateTime.UtcNow;
    public List<string> Errors { get; set; } = [];

    /// <summary>
    /// Error code for programmatic handling (e.g., "TLS_HANDSHAKE_FAILED", "AUTH_FAILED")
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Help link for troubleshooting the specific error
    /// </summary>
    public string? HelpLink { get; set; }
}

