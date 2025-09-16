// Infrastructure/DependencyInjection.cs
using ActoX.Infrastructure.Data;
using ActoX.Infrastructure.HealthChecks;
using ActoX.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Database
        services.AddSingleton<IDbConnectionFactory, SqlServerConnectionFactory>();
        
        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        
        // Database Migration
        services.AddTransient<DatabaseMigrator>();

        // Health Checks
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>(
                name: "database",
                tags: ["database", "sqlserver", "critical"])
            .AddCheck<SystemHealthCheck>(
                name: "system", 
                tags: ["system", "resources"])
            .AddSqlServer(
                configuration.GetConnectionString("DefaultConnection")!,
                name: "sqlserver-connection",
                tags: ["database", "connection"]);

        return services;
    }
}