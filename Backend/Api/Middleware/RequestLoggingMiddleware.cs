using System.Diagnostics;
using System.Text;

namespace ActoEngine.WebApi.Api.Middleware;

public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger, IConfiguration config)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<RequestLoggingMiddleware> _logger = logger;
    private readonly bool _logHeaders = config.GetValue("Logging:LogHeaders", false);
    private readonly bool _logBody = config.GetValue("Logging:LogBody", false);
    private readonly bool _logResponseBody = config.GetValue("Logging:LogResponseBody", false);

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString();
        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (context.RequestAborted.IsCancellationRequested)
                {
                    _logger.LogWarning("Request canceled");
                    return;
                }

                var request = context.Request;
                _logger.LogInformation("[Request] {Method} {Path}{QueryString}",
                    request.Method, request.Path, request.QueryString.HasValue ? request.QueryString.Value : string.Empty);

                if (_logHeaders)
                {
                    _logger.LogDebug("[Headers] {Headers}", GetSafeHeaders(request));
                }

                if (_logBody && request.ContentLength > 0 &&
                    (request.Method == HttpMethods.Post || request.Method == HttpMethods.Put || request.Method == HttpMethods.Patch) &&
                    request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var body = await ReadRequestBodyAsync(request);
                    _logger.LogDebug("[Body] {Body}", body);
                }

                if (_logResponseBody)
                {
                    // Capture the original response body stream
                    var originalBodyStream = context.Response.Body;

                    await using var responseBodyStream = new MemoryStream();
                    context.Response.Body = responseBodyStream;

                    await _next(context);

                    // Read the response
                    responseBodyStream.Seek(0, SeekOrigin.Begin);
                    string responseText;
                    using (var reader = new StreamReader(responseBodyStream, Encoding.UTF8, true, 1024, leaveOpen: true))
                    {
                        responseText = await reader.ReadToEndAsync();
                    }

                    _logger.LogDebug("[Response Body] {Body}", SafeResponseBody(responseText));

                    // Reset and copy back to original stream
                    responseBodyStream.Seek(0, SeekOrigin.Begin);
                    context.Response.Body = originalBodyStream;
                    await responseBodyStream.CopyToAsync(originalBodyStream);
                }
                else
                {
                    await _next(context);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Request canceled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing request");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogInformation("[Response] {StatusCode} in {ElapsedMilliseconds} ms",
                    context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
            }
        }
    }

    private static string GetSafeHeaders(HttpRequest request)
    {
        var sensitiveHeaders = new[] { "Authorization", "Cookie" };
        return string.Join("; ", request.Headers
            .Where(h => !sensitiveHeaders.Contains(h.Key, StringComparer.OrdinalIgnoreCase))
            .Select(h => $"{h.Key}: {h.Value}"));
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request, int maxLength = 1024)
    {
        if (request.ContentLength > maxLength)
        {
            return $"[Truncated: {request.ContentLength}]";
        }

        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return (body.Contains("password", StringComparison.OrdinalIgnoreCase) || body.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase)) ? "[Redacted]" : body;
    }

    private static string SafeResponseBody(string body, int maxLength = 1024)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "[Empty]";
        }

        if (body.Length > maxLength)
        {
            return $"[Truncated: {body.Length}]";
        }

        return body.Contains("password", StringComparison.OrdinalIgnoreCase) ? "[Redacted]" : body;
    }
}

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}