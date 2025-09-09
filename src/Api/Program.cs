using ActoX.Api.Configuration;
using ActoX.Infrastructure;
using ActoX.Api.Middleware;
using ActoX.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Custom service registrations
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddLogging(logging => logging.AddConsole());
var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    using var scope = app.Services.CreateScope();
    // var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
    // migrator.MigrateDatabase();
}

app.UseHttpsRedirection();
app.UseCors("ReactPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.MapControllers();
app.Run();