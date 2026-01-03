using System.Text.Json;
using ActoEngine.WebApi.Models;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                logger.LogWarning("Cannot handle exception for {Path}; response has already started.", context.Request.Path);
                logger.LogError(ex, "Unhandled exception occurred");
                // Rethrowing might terminate the connection abruptly
                return;
            }

            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            logger.LogWarning("Cannot handle exception for {Path}; response has already started.", context.Request.Path);
            return;
        }

        logger.LogError(exception, "An unhandled exception occurred for path: {Path}", context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse("Internal Server Error")
        {
            Message = exception.Message,
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