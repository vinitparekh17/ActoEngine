using ActoEngine.WebApi.Services.Auth;
using ActoEngine.WebApi.Services.PermissionService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
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

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Try to get token from Authorization header first
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        string? token = null;

        if (!string.IsNullOrEmpty(authHeader))
        {
            const string bearerPrefix = "Bearer ";
            token = authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
                ? authHeader[bearerPrefix.Length..].Trim()
                : authHeader.Trim();
        }

        // If no header token, try query parameter (only for SSE endpoints)
        if (string.IsNullOrEmpty(token) &&
            Request.Query.ContainsKey("token") &&
            Request.Path.Value?.EndsWith("/stream", StringComparison.OrdinalIgnoreCase) == true)
        {
            token = Request.Query["token"].FirstOrDefault();
            _logger.LogDebug("Using token from query parameter for SSE endpoint: {Path}", Request.Path);
        }

        if (string.IsNullOrEmpty(token))
        {
            _logger.LogDebug("No Authorization header or token query parameter found for path: {Path}", Request.Path);
            return AuthenticateResult.NoResult();
        }

        using var scope = _scopeFactory.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var principal = authService.ValidateAccessToken(token);
        if (principal == null)
        {
            _logger.LogWarning("Token validation failed for path: {Path}", Request.Path);
            return AuthenticateResult.Fail("Invalid or expired access token");
        }

        var userIdClaim = principal.FindFirst("user_id")?.Value;
        if (userIdClaim != null && int.TryParse(userIdClaim, out var userId))
        {
            Context.Items["UserId"] = userId;

            // LOAD USER PERMISSIONS AND ADD TO CLAIMS
            try
            {
                var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();
                var permissions = await permissionService.GetUserPermissionsAsync(userId);

                var claims = principal.Claims.ToList();
                foreach (var permission in permissions)
                {
                    claims.Add(new Claim("permission", permission));
                }

                var identity = new ClaimsIdentity(claims, "custom_token");
                principal = new ClaimsPrincipal(identity);

                _logger.LogInformation("User authenticated: {UserId} with {PermissionCount} permissions", userId, permissions.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading permissions for user {UserId}", userId);
                // Continue without permissions rather than failing authentication
            }
        }

        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}