namespace ActoEngine.Application.Contracts.Auth;

public class TokenRotationResult
{
    public string SessionToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTime AccessExpiresAt { get; set; }
    public DateTime RefreshExpiresAt { get; set; }
    public int UserId { get; set; }
}

