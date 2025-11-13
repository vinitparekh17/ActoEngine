using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ActoEngine.WebApi.Extensions
{
    /// <summary>
    /// Reusable helpers for extracting common values from HttpContext.
    /// </summary>
    public static class HttpContextExtensions
    {
        /// <summary>
        /// Tries to get the current authenticated user's ID from HttpContext.
        /// Looks in HttpContext.Items["UserId"] first (set by middleware),
        /// then falls back to common JWT/claims locations (UserId, NameIdentifier, sub).
        /// Returns null when not available or not an integer.
        /// </summary>
        public static int? GetUserId(this HttpContext? httpContext)
        {
            if (httpContext is null)
                return null;

            // 1) Preferred: set by TokenMiddleware or similar
            if (httpContext.Items.TryGetValue("UserId", out var value))
            {
                if (value is int i && i > 0)
                    return i;
                if (value is string s && int.TryParse(s, out var parsed) && parsed > 0)
                    return parsed;
            }

            // 2) Fallback: check common claim types
            var claim = httpContext.User?.FindFirst("UserId")
                        ?? httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)
                        ?? httpContext.User?.FindFirst("sub");

            if (claim != null && int.TryParse(claim.Value, out var fromClaims) && fromClaims > 0)
                return fromClaims;

            return null;
        }

        /// <summary>
        /// Returns the current user's ID or the provided default when not available.
        /// </summary>
        public static int GetUserIdOrDefault(this HttpContext? httpContext, int defaultValue = 0)
        {
            return GetUserId(httpContext) ?? defaultValue;
        }
    }
}
