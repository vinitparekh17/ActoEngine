// Infrastructure/Data/DatabaseMigrator.cs
using System.Reflection;
using DbUp;
using Microsoft.Data.SqlClient;
using Dapper;

namespace ActoEngine.WebApi.Infrastructure.Database;

public class DatabaseMigrator(IConfiguration configuration, ILogger<DatabaseMigrator> logger)
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")!;

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
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(result.Error);
            Console.ResetColor();
        }

        logger.LogInformation("Database migration completed successfully.");
    }

    public async Task SeedDataAsync()
    {
        logger.LogInformation("Starting data seeding...");

        var seedScripts = new[]
        {
            "Sql.StoredProcedures.PROJECT_SYNC.sql",
        };

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var scriptName in seedScripts)
        {
            try
            {
                var script = GetEmbeddedScript(scriptName);
                if (!string.IsNullOrEmpty(script))
                {
                    await connection.ExecuteAsync(script);
                    logger.LogInformation("Executed seed script: {Script}", scriptName);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to execute seed script: {Script}", scriptName);
            }
        }
    }
    private static string? GetEmbeddedScript(string scriptName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(scriptName);
        if (stream == null)
        {
            return null;
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