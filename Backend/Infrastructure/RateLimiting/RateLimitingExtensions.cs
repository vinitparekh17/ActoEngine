using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace ActoEngine.WebApi.Infrastructure.RateLimiting;

public static class RateLimitingExtensions
{
    public static void AddCustomRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Auth-specific limiter (strengthened)
            options.AddFixedWindowLimiter("AuthRateLimit", opt =>
            {
                opt.PermitLimit = 5;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueLimit = 0;
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            }).RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Global limiter
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                // Exempt SSE streaming endpoints from rate limiting
                // SSE connections are long-lived single requests, not multiple requests
                if (context.Request.Path.Value?.EndsWith("/stream", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return RateLimitPartition.GetNoLimiter<string>("sse");
                }

                // Use the trusted RemoteIpAddress after ForwardedHeadersMiddleware
                var remoteIp = context.Connection?.RemoteIpAddress?.ToString();

                // Fallback to a deterministic identifier to prevent bypass via "unknown" bucket
                string partitionKey = !string.IsNullOrEmpty(remoteIp)
                    ? remoteIp
                    : context.Connection?.Id
                        ?? context.TraceIdentifier
                        ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: partitionKey,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    // Use integer seconds per RFC (ceiling to avoid under-retrying)
                    var retrySeconds = Math.Max(0, (int)Math.Ceiling(retryAfter.TotalSeconds));
                    context.HttpContext.Response.Headers.RetryAfter =
                        retrySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Too Many Requests",
                    message = "Rate limit exceeded. Please try again later.",
                    timestamp = DateTime.UtcNow.ToString("O")
                }, cancellationToken);
            };
        });
    }
}
