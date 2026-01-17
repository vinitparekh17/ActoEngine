using ActoEngine.WebApi.Features.Auth;
using ActoEngine.WebApi.Features.Permissions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ActoEngine.WebApi.Infrastructure.Auth;

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
        string? token = null;

        // 1. Try to get token from HttpOnly cookie first (Primary method)
        if (Request.Cookies.TryGetValue("access_token", out var cookieToken))
        {
            token = cookieToken;
        }

        // 2. Fallback to Authorization header (for API clients/testing)
        if (string.IsNullOrEmpty(token))
        {
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                const string bearerPrefix = "Bearer ";
                token = authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
                    ? authHeader[bearerPrefix.Length..].Trim()
                    : authHeader.Trim();
            }
        }

        // 3. Fallback to query parameter (only for SSE endpoints where cookies might not be sent or for specific use cases)
        if (string.IsNullOrEmpty(token) &&
            Request.Query.ContainsKey("token") &&
            Request.Path.Value?.EndsWith("/stream", StringComparison.OrdinalIgnoreCase) == true)
        {
            token = Request.Query["token"].FirstOrDefault();
            _logger.LogDebug("Using token from query parameter for SSE endpoint: {Path}", Request.Path);
        }

        if (string.IsNullOrEmpty(token))
        {
            _logger.LogDebug("No access token found in cookie, header, or query parameter for path: {Path}", Request.Path);
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

            // LOAD USER PERMISSIONS AND ADD TO CLAIMS (with caching)
            try
            {
                var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
                var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();

                var cacheKey = $"user_permissions:{userId}";
                IEnumerable<string> permissions;
                bool cacheHit = false;

                // Try to get permissions from cache
                try
                {
                    var cachedPermissions = await cache.GetStringAsync(cacheKey);
                    if (cachedPermissions != null)
                    {
                        permissions = JsonSerializer.Deserialize<IEnumerable<string>>(cachedPermissions) ?? [];
                        cacheHit = true;
                        _logger.LogDebug("Cache hit for user permissions: {UserId}", userId);
                    }
                    else
                    {
                        // Cache miss - load from database
                        permissions = await permissionService.GetUserPermissionsAsync(userId);

                        // Store in cache with 5-minute TTL
                        var serialized = JsonSerializer.Serialize(permissions);
                        await cache.SetStringAsync(cacheKey, serialized, new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                        });
                        _logger.LogDebug("Cache miss - loaded and cached permissions for user: {UserId}", userId);
                    }
                }
                catch (Exception cacheEx)
                {
                    // Cache failure - fall back to database
                    _logger.LogWarning(cacheEx, "Cache operation failed for user {UserId}, falling back to database", userId);
                    permissions = await permissionService.GetUserPermissionsAsync(userId);
                }

                var claims = principal.Claims.ToList();
                foreach (var permission in permissions)
                {
                    claims.Add(new Claim("permission", permission));
                }

                var authType = principal.Identity?.AuthenticationType ?? "custom_token";
                var identity = new ClaimsIdentity(claims, authType);
                principal = new ClaimsPrincipal(identity);

                _logger.LogInformation("User authenticated: {UserId} with {PermissionCount} permissions (cache: {CacheStatus})",
                    userId, permissions.Count(), cacheHit ? "hit" : "miss");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading permissions for user {UserId}", userId);
#if DEBUG
                return AuthenticateResult.Fail("Permission loading failed - check configuration");
#else
                // Continue without permissions in production to maintain availability
#endif
            }
        }

        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}