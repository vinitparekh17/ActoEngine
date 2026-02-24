// Infrastructure/Data/DatabaseMigrator.cs
using Dapper;
using DbUp;
using Microsoft.Data.SqlClient;
using System.Reflection;

namespace ActoEngine.WebApi.Infrastructure.Database;

public class DatabaseMigrator(IConfiguration configuration, ILogger<DatabaseMigrator> logger)
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

    public void MigrateDatabase()
    {
        logger.LogInformation("Starting database migration...");
        try
        {
            EnsureDatabase.For.SqlDatabase(_connectionString);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ensure database exists. ConnectionString (masked): {ConnectionString}",
                MaskConnectionString(_connectionString));
            throw;
        }

        var upgrader = DeployChanges.To
    .SqlDatabase(_connectionString)
    .WithScriptsEmbeddedInAssembly(
        Assembly.GetExecutingAssembly(),
        script => script.Contains("Migrations")) // Filter to migrations folder
    .WithTransaction()
    .LogToConsole()
    .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            logger.LogError(result.Error, "Database migration failed");
            throw new InvalidOperationException("Database migration failed. See logs for details.", result.Error);
        }

        logger.LogInformation("Database migration completed successfully.");
    }

    public async Task SeedDataAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting data seeding...");

        var assemblyName = Assembly.GetExecutingAssembly().GetName().Name
            ?? throw new InvalidOperationException("Executing assembly name could not be determined.");

        var seedScripts = new[]
        {
            $"{assemblyName}.Sql.StoredProcedures.PROJECT_SYNC.sql",
        };

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var scriptName in seedScripts)
        {
            try
            {
                var script = GetEmbeddedScriptOrThrow(scriptName);
                await connection.ExecuteAsync(new CommandDefinition(script, cancellationToken: cancellationToken));
                logger.LogInformation("Executed seed script: {Script}", scriptName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to execute seed script: {Script}", scriptName);
                throw;
            }
        }
    }

    private static string GetEmbeddedScriptOrThrow(string scriptName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(scriptName);
        if (stream == null)
        {
            throw new InvalidOperationException(
                $"Embedded script '{scriptName}' was not found. " +
                $"Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string MaskConnectionString(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            if (!string.IsNullOrEmpty(builder.Password)) builder.Password = "******";
            if (!string.IsNullOrEmpty(builder.UserID)) builder.UserID = "******";
            return builder.ConnectionString;
        }
        catch
        {
            return "Invalid Connection String";
        }
    }
}
