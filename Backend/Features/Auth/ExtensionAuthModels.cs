namespace ActoEngine.WebApi.Features.Auth;

public class ExtensionTokenExchangeRequest
{
    public required string Code { get; set; }
    public required string CodeVerifier { get; set; }
    public required string ClientId { get; set; }
    public required string RedirectUri { get; set; }
}

public class ExtensionRefreshRequest
{
    public required string RefreshToken { get; set; }
    public required string ClientId { get; set; }
}

public class ExtensionTokenResponse
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public long ExpiresAt { get; set; }
}

public class ExtensionAuthCode
{
    public int AuthCodeId { get; set; }
    public int UserID { get; set; }
    public required string ClientId { get; set; }
    public required string RedirectUri { get; set; }
    public required string CodeHash { get; set; }
    public required string CodeChallenge { get; set; }
    public required string CodeChallengeMethod { get; set; }
    public string? State { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
}

public class ExtensionTokenSession
{
    public int SessionId { get; set; }
    public int UserID { get; set; }
    public required string ClientId { get; set; }
    public required string AccessToken { get; set; }
    public DateTime AccessExpiresAt { get; set; }
    public required string RefreshToken { get; set; }
    public DateTime RefreshExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
