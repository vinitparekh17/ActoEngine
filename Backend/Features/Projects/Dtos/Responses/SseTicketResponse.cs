namespace ActoEngine.WebApi.Features.Projects.Dtos.Responses;

/// <summary>
/// Response containing a short-lived one-time ticket for SSE connections
/// </summary>
public class SseTicketResponse
{
    /// <summary>
    /// Base64URL-encoded one-time ticket
    /// </summary>
    public required string Ticket { get; set; }

    /// <summary>
    /// Ticket expiry time in seconds (typically 30)
    /// </summary>
    public int ExpiresIn { get; set; }
}
