using ActoEngine.WebApi.Api;
using ActoEngine.WebApi.Api.ApiModels;
using Microsoft.AspNetCore.Mvc;

namespace ActoEngine.WebApi.Shared.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApiServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // CORS
            services.AddCorsServices(configuration);

            // Swagger
            services.AddCustomSwagger();

            // API Behavior
            services.AddApiBehaviorServices();

            return services;
        }

        private static IServiceCollection AddCorsServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("ReactPolicy", builder =>
                {
                    // Get CORS origins from environment variable or configuration
                    var corsOriginsEnv = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");
                    string[] allowedOrigins;

                    if (!string.IsNullOrWhiteSpace(corsOriginsEnv))
                    {
                        // Parse comma-separated list from environment variable
                        allowedOrigins = corsOriginsEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    }
                    else
                    {
                        // Fallback to configuration or default
                        var configOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
                        allowedOrigins = configOrigins?.Length > 0 
                            ? configOrigins 
                            : ["http://localhost:5173", "http://localhost:3000"];
                    }

                    builder
                        .WithOrigins(allowedOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials()
                        .SetIsOriginAllowedToAllowWildcardSubdomains();
                });
            });

            return services;
        }
        private static IServiceCollection AddApiBehaviorServices(
            this IServiceCollection services)
        {
            services.Configure<ApiBehaviorOptions>(options =>
            {
                // Customize validation error responses
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(e => e.Value?.Errors.Count > 0)
                        .SelectMany(x => x.Value!.Errors)
                        .Select(x => x.ErrorMessage)
                        .ToList();

                    var apiResponse = ApiResponse<object>.Failure(
                        "Validation failed",
                        errors);

                    return new BadRequestObjectResult(apiResponse);
                };
            });

            return services;
        }
    }
}
