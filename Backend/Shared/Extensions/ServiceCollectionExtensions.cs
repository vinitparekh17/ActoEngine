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
                    // Read from IConfiguration (populated from env vars + .env via AddEnvironmentVariables)
                    var corsOriginsConfig = configuration["CORS_ALLOWED_ORIGINS"];
                    string[] allowedOrigins;

                    var parsedOrigins = string.IsNullOrWhiteSpace(corsOriginsConfig)
                        ? []
                        : corsOriginsConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    if (parsedOrigins.Length > 0)
                    {
                        // Use the env-var list only when it contains at least one valid entry;
                        // an all-commas value like ",,," would produce an empty array and
                        // should fall through to the configured / default fallback below.
                        allowedOrigins = parsedOrigins;
                    }
                    else
                    {
                        // Fallback to configuration section or default
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
                        .WithExposedHeaders("X-New-Access-Token")
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
