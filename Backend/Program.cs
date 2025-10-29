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
using ActoEngine.WebApi.Services.Schema;
using ActoEngine.WebApi.Services.ClientService;
using ActoEngine.WebApi.Services.SpBuilder;

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
builder.Services.AddScoped<IDataSeeder, DatabaseSeeder>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITokenRepository, TokenRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ISchemaSyncRepository, SchemaSyncRepository>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISchemaService, SchemaService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ISpBuilderService, SpBuilderService>();
builder.Services.AddScoped<IFormBuilderService, FormBuilderService>();
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IClientService, ClientService>();

// Form Builder Services
builder.Services.AddScoped<FormConfigRepository>();
builder.Services.AddScoped<CodeTemplateRepository>();
builder.Services.AddScoped<GenerationHistoryRepository>();
builder.Services.AddScoped<TemplateRenderService>();

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

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors("ReactPolicy");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCustomSwagger(app.Environment);
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseTokenAuthentication();
app.UseAuthorization();
app.UseMiddleware<RequestLoggingMiddleware>();
app.MapControllers();
app.Run();