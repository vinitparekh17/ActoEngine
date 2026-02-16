using System.Text.Json;
using ActoEngine.WebApi.Api.ApiModels;
using ActoEngine.WebApi.Features.Auth;

namespace ActoEngine.WebApi.Api.Middleware
{
    public class TokenMiddleware(RequestDelegate next, ILogger<TokenMiddleware> logger, IServiceScopeFactory scopeFactory)
    {
        private readonly RequestDelegate _next = next;
        private readonly ILogger<TokenMiddleware> _logger = logger;
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly string[] _excludedPaths = [
            "/api/auth/login",
            "/api/auth/register",
            "/api/auth/refresh",
            "/api/auth/logout",
            "/health",
            "/swagger",
            "/favicon.ico"
        ];

        public async Task InvokeAsync(HttpContext context)
        {
            if (ShouldSkipAuthentication(context.Request.Path))
            {
                await _next(context);
                return;
            }

            if (context.User.Identity?.IsAuthenticated != true)
            {
                await TryRefreshTokens(context);
            }

            await _next(context);
        }

        private async Task TryRefreshTokens(HttpContext context)
        {
            var refreshToken = context.Request.Cookies["refresh_token"];
            if (string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogDebug("No refresh token found in cookies for path: {Path}", context.Request.Path);
                await WriteUnauthorizedResponse(context, "Refresh token required");
                return;
            }

            var accessToken = ExtractAccessToken(context.Request) ?? string.Empty;

            using var scope = _scopeFactory.CreateScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

            var result = await authService.RotateTokensAsync(accessToken, refreshToken);
            if (result == null || string.IsNullOrEmpty(result.SessionToken))
            {
                ClearRefreshTokenCookie(context);
                _logger.LogWarning("Token refresh failed for path: {Path}", context.Request.Path);
                await WriteUnauthorizedResponse(context, "Token refresh failed");
                return;
            }

            context.Response.Headers["X-New-Access-Token"] = result.SessionToken;
            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                SetRefreshTokenCookie(context, result.RefreshToken, result.RefreshExpiresAt);
            }

            var principal = await authService.ValidateAccessTokenAsync(result.SessionToken);
            if (principal != null)
            {
                context.User = principal;

                // Set UserId in context items for controllers to use
                var userIdClaim = principal.FindFirst("user_id")?.Value;
                if (userIdClaim != null && int.TryParse(userIdClaim, out var userId))
                {
                    context.Items["UserId"] = userId;
                }

                _logger.LogInformation("Tokens rotated successfully for user {UserId}", result.UserId);
                // Re-run the pipeline to re-authenticate
                await _next(context);
            }
            else
            {
                _logger.LogWarning("Failed to validate new access token after refresh for path: {Path}", context.Request.Path);
                await WriteUnauthorizedResponse(context, "Invalid new access token");
            }
        }

        private static string? ExtractAccessToken(HttpRequest request)
        {
            var authHeader = request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader))
            {
                return null;
            }

            const string bearerPrefix = "Bearer ";
            return authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
                ? authHeader[bearerPrefix.Length..].Trim()
                : authHeader.Trim();
        }

        private bool ShouldSkipAuthentication(PathString path)
        {
            return _excludedPaths.Any(excludedPath =>
                path.StartsWithSegments(excludedPath, StringComparison.OrdinalIgnoreCase));
        }

        private static void SetRefreshTokenCookie(HttpContext context, string refreshToken, DateTime expiresAt)
        {
            context.Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = expiresAt,
                Path = "/",
                IsEssential = true
            });
        }

        private static void ClearRefreshTokenCookie(HttpContext context)
        {
            context.Response.Cookies.Delete("refresh_token", new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });
        }

        private static async Task WriteUnauthorizedResponse(HttpContext context, string message)
        {
            if (context.Response.HasStarted)
            {
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";

            var response = new ErrorResponse("Unauthorized")
            {
                Message = message,
                Timestamp = DateTime.UtcNow.ToString("O"),
                Path = context.Request.Path.Value ?? string.Empty
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
        }
    }

    public static class TokenMiddlewareExtensions
    {
        public static IApplicationBuilder UseTokenAuthentication(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TokenMiddleware>();
        }
    }
}
