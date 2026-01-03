using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ActoEngine.WebApi.Config
{
    /// <summary>
    /// Operation filter to handle authorization requirements in Swagger documentation
    /// </summary>
    public class AuthOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Check if the endpoint allows anonymous access
            var allowAnonymous = HasAttribute<AllowAnonymousAttribute>(context);

            // Check if the endpoint requires authorization
            var requiresAuth = HasAttribute<AuthorizeAttribute>(context);

            if (allowAnonymous)
            {
                // Remove security requirements for anonymous endpoints
                operation.Security?.Clear();
                return;
            }

            // For protected endpoints, ensure security requirements are present
            if (requiresAuth || ShouldRequireAuthByDefault(context))
            {
                operation.Security ??= [];

                if (!operation.Security.Any())
                {
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                            Array.Empty<string>()
                        }
                    });
                }

                // Add standard auth response codes
                AddAuthResponseCodes(operation);
            }
        }

        private static bool HasAttribute<T>(OperationFilterContext context) where T : Attribute
        {
            return context.MethodInfo.GetCustomAttributes(true).OfType<T>().Any() ||
                   context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<T>().Any() == true;
        }

        private static bool ShouldRequireAuthByDefault(OperationFilterContext context)
        {
            // Add logic here if you want to require auth by default for certain controllers
            // For example, all controllers except AuthController
            var controllerName = context.MethodInfo.DeclaringType?.Name;
            return controllerName != "AuthController";
        }

        private static void AddAuthResponseCodes(OpenApiOperation operation)
        {
            operation.Responses.TryAdd("401", new OpenApiResponse
            {
                Description = "Unauthorized - Invalid or missing access token",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.Schema,
                                Id = "ErrorResponse"
                            }
                        }
                    }
                }
            });

            operation.Responses.TryAdd("403", new OpenApiResponse
            {
                Description = "Forbidden - Insufficient permissions",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.Schema,
                                Id = "ErrorResponse"
                            }
                        }
                    }
                }
            });
        }
    }
}