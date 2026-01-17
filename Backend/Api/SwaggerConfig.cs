using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace ActoEngine.WebApi.Api
{
    public static class SwaggerConfiguration
    {
        public static IServiceCollection AddCustomSwagger(this IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                // Basic API information
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "ActoEngine API",
                    Version = "v1",
                    Description = "ActoEngine API with custom token-based authentication",
                    Contact = new OpenApiContact
                    {
                        Name = "ActoEngine Team",
                        Email = "contact@ActoEngine.com",
                        Url = new Uri("https://ActoEngine.com")
                    },
                    License = new OpenApiLicense
                    {
                        Name = "MIT License",
                        Url = new Uri("https://opensource.org/licenses/MIT")
                    }
                });

                // Security definitions
                AddSecurityDefinitions(c);

                // Apply global security requirement
                AddGlobalSecurity(c);

                // Apply custom filters
                c.OperationFilter<AuthOperationFilter>();

                // Include XML comments
                IncludeXmlComments(c);

                // Custom schema configurations
                ConfigureSchemas(c);
            });

            return services;
        }

        private static void AddSecurityDefinitions(SwaggerGenOptions c)
        {
            // Bearer token authentication
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "Custom Token",
                In = ParameterLocation.Header,
                Description = "Enter your access token below (without 'Bearer ' prefix):\n\n" +
                             "Example: 'abc123def456ghi789'"
            });

            // Cookie authentication for refresh token (documentation purposes)
            c.AddSecurityDefinition("RefreshCookie", new OpenApiSecurityScheme
            {
                Name = "refresh_token",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Cookie,
                Description = "Refresh token automatically sent via HTTP-only cookie"
            });
        }

        private static void AddGlobalSecurity(SwaggerGenOptions c)
        {
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
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

        private static void IncludeXmlComments(SwaggerGenOptions c)
        {
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }
        }

        private static void ConfigureSchemas(SwaggerGenOptions c)
        {
            // Custom schema mappings if needed
            c.MapType<DateTime>(() => new OpenApiSchema
            {
                Type = "string",
                Format = "date-time",
                Example = new Microsoft.OpenApi.Any.OpenApiString(DateTime.UtcNow.ToString("O"))
            });

            // Support for custom types
            c.UseInlineDefinitionsForEnums();
            c.DescribeAllParametersInCamelCase();
        }

        public static IApplicationBuilder UseCustomSwagger(this IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment() || env.IsStaging())
            {
                app.UseSwagger(c =>
                {
                    c.RouteTemplate = "swagger/{documentName}/swagger.json";
                });

                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ActoEngine API V1");
                    c.RoutePrefix = "swagger";
                    c.DocumentTitle = "ActoEngine API Documentation";

                    // UI Configuration
                    c.DisplayRequestDuration();
                    c.EnableTryItOutByDefault();
                    c.EnableDeepLinking();
                    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
                    c.DefaultModelExpandDepth(2);
                    c.DefaultModelsExpandDepth(1);
                    c.ShowExtensions();

                    // Custom styling (optional)
                    c.InjectStylesheet("/swagger-ui/custom.css");
                    c.InjectJavascript("/swagger-ui/custom.js");
                });
            }

            return app;
        }
    }
}