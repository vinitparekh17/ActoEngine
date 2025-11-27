using System.Threading.RateLimiting;
using ActoEngine.WebApi.Config;
using ActoEngine.WebApi.Middleware;
using ActoEngine.WebApi.Repositories;
using ActoEngine.WebApi.Services.Auth;
using ActoEngine.WebApi.Services.Database;
using ActoEngine.WebApi.Services.ProjectService;
using ActoEngine.WebApi.Services.FormBuilderService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using ActoEngine.WebApi.Services.Schema;
using ActoEngine.WebApi.Services.ClientService;
using ActoEngine.WebApi.Services.ProjectClientService;
using ActoEngine.WebApi.Services.SpBuilder;
using ActoEngine.WebApi.Services.ContextService;
using DotNetEnv;
using ActoEngine.WebApi.Services.DependencyService;
using ActoEngine.WebApi.Services.RoleService;
using ActoEngine.WebApi.Services.PermissionService;
using ActoEngine.WebApi.Services.UserManagementService;

var builder = WebApplication.CreateBuilder(args);

Env.Load();
builder.Configuration.AddEnvironmentVariables();

var dbServer = Environment.GetEnvironmentVariable("DB_SERVER") ?? builder.Configuration["DB_SERVER"] ?? "127.0.0.1";
var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? builder.Configuration["DB_PORT"] ?? "1433";
var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? builder.Configuration["DB_NAME"] ?? "ActoEngine";
var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? builder.Configuration["DB_USER"] ?? "sa";
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? builder.Configuration["DB_PASSWORD"]
    ?? throw new InvalidOperationException("Database password must be set via DB_PASSWORD environment variable");

// Build secure connection string with conditional certificate validation
// Only trust server certificate if explicitly enabled via environment variable
var trustCert = string.Equals(Environment.GetEnvironmentVariable("DB_TRUST_CERT")?.ToLower(), "true");

var connStringBuilder = new SqlConnectionStringBuilder
{
    DataSource = $"{dbServer},{dbPort}",
    InitialCatalog = dbName,
    UserID = dbUser,
    Password = dbPassword,
    MultipleActiveResultSets = true,
    Encrypt = true, // Always enable encryption
    TrustServerCertificate = trustCert // Only trust untrusted certs in development
};

builder.Configuration["ConnectionStrings:DefaultConnection"] = connStringBuilder.ConnectionString;

// Set admin password from environment variables
var adminPassword = Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD") ?? builder.Configuration["SEED_ADMIN_PASSWORD"]
    ?? throw new InvalidOperationException("Admin password must be set via SEED_ADMIN_PASSWORD environment variable");
builder.Configuration["DatabaseSeeding:DefaultPasswords:AdminUser"] = adminPassword;

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure authentication
builder.Services.AddAuthentication("TokenAuth")
    .AddScheme<AuthenticationSchemeOptions, CustomTokenAuthenticationHandler>("TokenAuth", null);

builder.Services.AddAuthorization();

// Configure anti-forgery (CSRF protection)
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false; // Must be false so JavaScript can read it
    // SECURITY FIX: Only require HTTPS in production (allows HTTP in development)
    options.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Configure HSTS for production security
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

builder.Services.AddRateLimiter(options =>
{
    // Auth-specific limiter
    options.AddFixedWindowLimiter("AuthRateLimit", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    }).RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global limiter
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // Use the trusted RemoteIpAddress after ForwardedHeadersMiddleware
        var remoteIp = context.Connection?.RemoteIpAddress?.ToString();

        // Fallback to a deterministic identifier to prevent bypass via "unknown" bucket
        string partitionKey = !string.IsNullOrEmpty(remoteIp)
            ? remoteIp
            : context.Connection?.Id
                ?? context.TraceIdentifier
                ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: partitionKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();
        }

        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Too Many Requests",
            message = "Rate limit exceeded. Please try again later.",
            timestamp = DateTime.UtcNow.ToString("O")
        }, cancellationToken);
    };
});

builder.Services.AddLogging(logging => logging.AddConsole());
builder.Services.AddApiServices(builder.Configuration);

builder.Services.AddScoped<IDbConnectionFactory, SqlServerConnectionFactory>();
builder.Services.AddScoped<IDataSeeder, DatabaseSeeder>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITokenRepository, TokenRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ISchemaRepository, SchemaRepository>();
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IProjectClientRepository, ProjectClientRepository>();
builder.Services.AddScoped<IContextRepository, ContextRepository>();

// Role & Permission Repositories
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IPermissionRepository, PermissionRepository>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISchemaService, SchemaService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ISpBuilderService, SpBuilderService>();
builder.Services.AddScoped<IFormBuilderService, FormBuilderService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IProjectClientService, ProjectClientService>();
builder.Services.AddScoped<IContextService, ContextService>();

// Role & Permission Services
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();

// Form Builder Services
builder.Services.AddScoped<IFormConfigRepository, FormConfigRepository>();
builder.Services.AddScoped<ICodeTemplateRepository, CodeTemplateRepository>();
builder.Services.AddScoped<IGenerationHistoryRepository, GenerationHistoryRepository>();
builder.Services.AddScoped<ITemplateRenderService, TemplateRenderService>();

builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenHasher, TokenHasher>();

builder.Services.AddTransient<DatabaseMigrator>();

var app = builder.Build();
using var scope = app.Services.CreateScope();
var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
migrator.MigrateDatabase();

var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
await seeder.SeedAsync();

// Configure middleware/pipeline

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
app.MapControllers();
app.Run();