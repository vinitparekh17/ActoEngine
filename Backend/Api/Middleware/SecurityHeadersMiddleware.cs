using System.Text;
namespace ActoEngine.WebApi.Api.Middleware;

public class SecurityHeadersMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Generate a nonce for CSP
        var nonce = Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
        context.Items["csp-nonce"] = nonce;

        // Add security headers
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block"); // Optional for legacy support
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("Content-Security-Policy",
            $"default-src 'self'; script-src 'self' 'nonce-{nonce}'; style-src 'self' 'nonce-{nonce}'; " +
            "img-src 'self' data:; connect-src 'self'; font-src 'self'; object-src 'none'; frame-ancestors 'none';");
        context.Response.Headers.Append("Permissions-Policy", "geolocation=(), camera=(), microphone=()");
        context.Response.Headers.Append("Cross-Origin-Resource-Policy", "same-origin");
        context.Response.Headers.Append("Cross-Origin-Opener-Policy", "same-origin");
        context.Response.Headers.Append("Cross-Origin-Embedder-Policy", "require-corp");

        if (context.Request.IsHttps)
        {
            context.Response.Headers.Append("Strict-Transport-Security",
                "max-age=31536000; includeSubDomains; preload");
        }

        await _next(context);
    }
}