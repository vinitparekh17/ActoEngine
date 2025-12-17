using System.Threading.RateLimiting;
using System.Net;
using ActoEngine.WebApi.Config;
using ActoEngine.WebApi.Middleware;
using ActoEngine.WebApi.Repositories;
using ActoEngine.WebApi.Services.Auth;
using ActoEngine.WebApi.Services.Database;
using ActoEngine.WebApi.Services.ProjectService;
using ActoEngine.WebApi.Services.FormBuilderService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using ActoEngine.WebApi.Services.Schema;
using ActoEngine.WebApi.Services.ClientService;
using ActoEngine.WebApi.Services.ProjectClientService;
using ActoEngine.WebApi.Services.SpBuilder;
using ActoEngine.WebApi.Services.ContextService;
using DotNetEnv;
using ActoEngine.WebApi.Services.ImpactService;
using ActoEngine.WebApi.Services.RoleService;
using ActoEngine.WebApi.Services.PermissionService;
using ActoEngine.WebApi.Services.UserManagementService;
using ActoEngine.WebApi.Services.ValidationService;

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

// Configure forwarded headers for reverse proxy support
// SECURITY: Only trust X-Forwarded-* headers from known proxies to prevent IP spoofing
// and other header injection attacks.
//
// DEPLOYMENT REQUIREMENTS:
// - Development: Trusts localhost and Docker networks by default
// - Production: Configure TRUSTED_PROXY_IPS environment variable with your reverse proxy IPs
//   Example: TRUSTED_PROXY_IPS=10.0.0.1,172.16.0.0/16,192.168.1.100
//
// If deploying behind cloud load balancers (AWS ALB, Azure App Gateway, etc.):
// - Add the load balancer's IP range to TRUSTED_PROXY_IPS
// - Ensure your proxy/load balancer strips client-provided X-Forwarded headers
//
// References:
// - https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer
// - https://cheatsheetseries.owasp.org/cheatsheets/DotNet_Security_Cheat_Sheet.html#forwarded-headers
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    
    // Development defaults: Trust localhost and common Docker networks
    // These are safe for local development but should NOT be used in production
    if (builder.Environment.IsDevelopment())
    {
        options.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
        options.KnownProxies.Add(IPAddress.Parse("::1"));
        options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("172.17.0.0"), 16)); // Docker default bridge
        options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8));     // Common private network
    }
    
    // Production: Load trusted proxy IPs from environment variable
    // Format: Comma-separated IPs or CIDR notation (e.g., "10.0.0.1,172.16.0.0/16")
    var trustedProxies = builder.Configuration["TRUSTED_PROXY_IPS"];
    if (!string.IsNullOrEmpty(trustedProxies))
    {
        foreach (var proxy in trustedProxies.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmedProxy = proxy.Trim();
            
            // Check if it's a CIDR notation (e.g., 172.16.0.0/16)
            if (trimmedProxy.Contains('/'))
            {
                var parts = trimmedProxy.Split('/');
                if (parts.Length == 2 && 
                    IPAddress.TryParse(parts[0], out var networkAddress) && 
                    int.TryParse(parts[1], out var prefixLength))
                {
                    options.KnownNetworks.Add(new IPNetwork(networkAddress, prefixLength));
                }
            }
            // Single IP address
            else if (IPAddress.TryParse(trimmedProxy, out var proxyAddress))
            {
                options.KnownProxies.Add(proxyAddress);
            }
        }
    }
    
    // If no proxies configured in production, log a warning
    if (!builder.Environment.IsDevelopment() && 
        options.KnownProxies.Count == 0 && 
        options.KnownNetworks.Count == 0)
    {
        Console.WriteLine("WARNING: No trusted proxies configured. X-Forwarded headers will not be processed.");
        Console.WriteLine("Set TRUSTED_PROXY_IPS environment variable to configure trusted proxies.");
    }
});

builder.Services.AddRateLimiter(options =>
{
    // Auth-specific limiter (strengthened)
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

// Add distributed memory cache for permission caching
builder.Services.AddDistributedMemoryCache();

builder.Services.AddScoped<IDbConnectionFactory, SqlServerConnectionFactory>();
builder.Services.AddScoped<IDataSeeder, DatabaseSeeder>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITokenRepository, TokenRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ISchemaRepository, SchemaRepository>();
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IProjectClientRepository, ProjectClientRepository>();
builder.Services.AddScoped<IContextRepository, ContextRepository>();
builder.Services.AddScoped<IDependencyRepository, DependencyRepository>();

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
builder.Services.AddScoped<IImpactService, ImpactService>();

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

// Validation Services
builder.Services.AddSingleton<IPasswordValidator, PasswordValidator>();
builder.Services.AddSingleton<IDatabaseIdentifierValidator, DatabaseIdentifierValidator>();

builder.Services.AddTransient<DatabaseMigrator>();

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