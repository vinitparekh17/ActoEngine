using ActoEngine.WebApi.Api;
using ActoEngine.WebApi.Api.Middleware;
using ActoEngine.WebApi.Infrastructure.Database;
using ActoEngine.WebApi.Infrastructure.RateLimiting;
using ActoEngine.WebApi.Infrastructure.Security;
using ActoEngine.WebApi.Shared.Extensions;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);

Env.Load();
// Support for Docker Secrets (loaded first, can be overridden by env vars)
builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);
// Environment variables override secrets
builder.Configuration.AddEnvironmentVariables();

// 1. Database Configuration
builder.AddDatabaseConfiguration();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 2. Security Configuration (Auth, CSRF, HSTS, Proxy)
builder.Services.AddCustomSecurity(builder.Environment);
builder.Services.ConfigureProxySettings(builder.Configuration, builder.Environment);

// 3. Rate Limiting
builder.Services.AddCustomRateLimiting();

// 4. Service Registration (DI)
builder.Services.AddApplicationServices(builder.Configuration);

var app = builder.Build();

using var scope = app.Services.CreateScope();
var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
migrator.MigrateDatabase();

var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
await seeder.SeedAsync();

// Configure middleware/pipeline

// Apply forwarded headers first to ensure Request.IsHttps is correct for downstream middleware
app.UseForwardedHeaders();

// HSTS (HTTP Strict Transport Security) for production
if (app.Environment.IsProduction())
{
    app.UseHsts();
    // Verify HTTPS is configured in production
    var urls = builder.Configuration["ASPNETCORE_URLS"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "";
    if (!urls.Contains("https://", StringComparison.OrdinalIgnoreCase))
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("SECURITY WARNING: HTTPS is not configured in production! Cookies with Secure flag will not work over HTTP.");
    }
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors("ReactPolicy");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCustomSwagger(app.Environment);
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseTokenAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapControllers();
app.Run();