// Infrastructure/Data/DatabaseMigrator.cs
using Dapper;
using DbUp;
using Microsoft.Data.SqlClient;
using System.Reflection;

namespace ActoEngine.WebApi.Infrastructure.Database;

public class DatabaseMigrator(IConfiguration configuration, ILogger<DatabaseMigrator> logger)
{
    private static readonly Assembly ExecutingAssembly = Assembly.GetExecutingAssembly();
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
        ExecutingAssembly,
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

    private static string GetEmbeddedScriptOrThrow(string scriptName)
    {
        using var stream = ExecutingAssembly.GetManifestResourceStream(scriptName) ?? throw new InvalidOperationException(
                $"Embedded script '{scriptName}' was not found. " +
                $"Available resources: {string.Join(", ", ExecutingAssembly.GetManifestResourceNames())}");
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
