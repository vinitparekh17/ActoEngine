// Middleware/ExceptionHandlingMiddleware.cs
using System.Text.Json;
using Lou.Application.Common;
using Lou.Domain.Exceptions;

namespace Lou.WebApi.Middleware;
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = exception switch
        {
            NotFoundException => new
            {
                StatusCode = 404,
                exception.Message
            },
            ValidationException => new
            {
                StatusCode = 400,
                exception.Message
            },
            _ => new
            {
                StatusCode = 500,
                Message = "An internal server error occurred"
            }
        };

        context.Response.StatusCode = response.StatusCode;

        var jsonResponse = JsonSerializer.Serialize(
            ApiResponse<object>.Failure(response.Message)
        );

        await context.Response.WriteAsync(jsonResponse);
    }
}