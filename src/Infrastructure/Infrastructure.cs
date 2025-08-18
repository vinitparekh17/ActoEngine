using Lou.Infrastructure.Data;
using Lou.Infrastructure.HealthChecks;
using Lou.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lou.Infrastructure;
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddSingleton<IDbConnectionFactory, PostgreSqlConnectionFactory>();

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();

        // Database Migration
        services.AddTransient<DatabaseMigrator>();

        // Health Checks
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>(
                name: "database",
                tags: ["database", "postgresql", "critical"])
            .AddCheck<SystemHealthCheck>(
                name: "system",
                tags: ["system", "resources"])
            .AddNpgSql(
                configuration.GetConnectionString("DefaultConnection")!,
                name: "postgresql-connection",
                tags: ["database", "connection"]);

        return services;
    }
}