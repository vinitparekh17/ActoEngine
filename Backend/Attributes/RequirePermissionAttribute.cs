using ActoEngine.WebApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ActoEngine.WebApi.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequirePermissionAttribute(string permissionKey) : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Check if user is authenticated
        if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedObjectResult(
                ApiResponse<object>.Failure("Authentication required"));
            return;
        }

        // Get user permissions from claims
        var permissions = context.HttpContext.User
            .FindAll("permission")
            .Select(c => c.Value)
            .ToList();

        // Check if user has the required permission
        if (!permissions.Contains(permissionKey))
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<RequirePermissionAttribute>>();

            logger.LogWarning(
                "User {UserId} attempted to access {Path} without permission {Permission}",
                context.HttpContext.User.FindFirst("user_id")?.Value,
                context.HttpContext.Request.Path,
                permissionKey);

            context.Result = new ForbiddenObjectResult(
                ApiResponse<object>.Failure($"Permission '{permissionKey}' is required"));
            return;
        }

        // Permission granted
        await Task.CompletedTask;
    }
}

public class ForbiddenObjectResult : ObjectResult
{
    public ForbiddenObjectResult(object value) : base(value)
    {
        StatusCode = StatusCodes.Status403Forbidden;
    }
}
