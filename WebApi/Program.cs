using System.Threading.RateLimiting;
using ActoEngine.WebApi.Config;
using ActoEngine.WebApi.Middleware;
using ActoEngine.WebApi.Repositories;
using ActoEngine.WebApi.Services.Auth;
using ActoEngine.WebApi.Services.Database;
using ActoEngine.WebApi.Services.ProjectService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure authentication
builder.Services.AddAuthentication("TokenAuth")
    .AddScheme<AuthenticationSchemeOptions, CustomTokenAuthenticationHandler>("TokenAuth", null);

builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("AuthRateLimit", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});

builder.Services.AddLogging(logging => logging.AddConsole());
builder.Services.AddApiServices(builder.Configuration);


builder.Services.AddScoped<IDbConnectionFactory, SqlServerConnectionFactory>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ITokenRepository, TokenRepository>();
builder.Services.AddScoped<ISchemaSyncRepository, SchemaSyncRepository>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenHasher, TokenHasher>();
builder.Services.AddScoped<IDataSeeder, DatabaseSeeder>();

builder.Services.AddTransient<DatabaseMigrator>();

var app = builder.Build();
using var scope = app.Services.CreateScope();
var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
migrator.MigrateDatabase();

var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
await seeder.SeedAsync();

// Configure middleware/pipeline
app.UseRateLimiter();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCustomSwagger(app.Environment);
app.UseAuthentication();
app.UseTokenAuthentication();
app.UseAuthorization();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.MapControllers();
app.Run();
