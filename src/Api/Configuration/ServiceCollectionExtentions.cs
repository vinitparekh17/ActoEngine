using ActoX.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using System.Reflection;

namespace ActoX.Api.Configuration
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
                    var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                        ?? ["http://localhost:3000"];

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