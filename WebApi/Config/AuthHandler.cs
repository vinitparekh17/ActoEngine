using ActoEngine.WebApi.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

namespace ActoEngine.WebApi.Config;

public class CustomTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IServiceScopeFactory scopeFactory) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<CustomTokenAuthenticationHandler> _logger = logger.CreateLogger<CustomTokenAuthenticationHandler>();

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader))
        {
            _logger.LogDebug("No Authorization header found for path: {Path}", Request.Path);
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        const string bearerPrefix = "Bearer ";
        var token = authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? authHeader[bearerPrefix.Length..].Trim()
            : authHeader.Trim();

        using var scope = _scopeFactory.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var principal = authService.ValidateAccessToken(token);
        if (principal == null)
        {
            _logger.LogWarning("Token validation failed for path: {Path}", Request.Path);
            return Task.FromResult(AuthenticateResult.Fail("Invalid or expired access token"));
        }

        var userIdClaim = principal.FindFirst("user_id")?.Value;
        if (userIdClaim != null && int.TryParse(userIdClaim, out var userId))
        {
            Context.Items["UserId"] = userId;
            _logger.LogInformation("User authenticated: {UserId}", userId);
        }

        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}