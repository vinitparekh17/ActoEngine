using ActoX.Application.Interfaces;
using ActoX.Domain.Interfaces;
using ActoX.Infrastructure.Data;
using ActoX.Infrastructure.HealthChecks;
using ActoX.Infrastructure.Repositories;
using ActoX.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddScoped<IDbConnectionFactory, SqlServerConnectionFactory>();

        // Repositories (Domain interfaces implemented in Infrastructure)
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITokenRepository, TokenRepository>();

        // Infrastructure Services (Domain interfaces implemented in Infrastructure)
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenHasher, TokenHasher>();

        // Database Migration
        services.AddTransient<DatabaseMigrator>();

        // Data Seeding
        services.AddScoped<IDataSeeder, DatabaseSeeder>();

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