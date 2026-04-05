// Infrastructure/Data/DatabaseMigrator.cs
using Dapper;
using DbUp;
using DbUp.Engine;
using Microsoft.Data.SqlClient;
using System.Reflection;
using System.Text.RegularExpressions;

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

        // Most migrations run inside a DbUp-managed transaction for safety.
        ExecuteMigrationPhase(
            script => script.Contains("Migrations") && GetMigrationVersion(script) != 27,
            withoutTransaction: false,
            phaseLabel: "Main");

        // V027 creates a FULLTEXT CATALOG which cannot run inside a transaction.
        ExecuteMigrationPhase(
            script => GetMigrationVersion(script) == 27,
            withoutTransaction: true,
            phaseLabel: "FTS");

        logger.LogInformation("Database migration completed successfully.");
    }

    private void ExecuteMigrationPhase(Func<string, bool> scriptFilter, bool withoutTransaction, string phaseLabel)
    {
        var scripts = ExecutingAssembly
            .GetManifestResourceNames()
            .Where(scriptFilter)
            .OrderBy(GetMigrationVersion)
            .Select(scriptName => new SqlScript(scriptName, GetEmbeddedScriptOrThrow(scriptName)))
            .ToArray();

        var builder = DeployChanges.To
            .SqlDatabase(_connectionString)
            .WithScripts(scripts);

        var upgrader = withoutTransaction
            ? builder.WithoutTransaction().LogToConsole().Build()
            : builder.WithTransaction().LogToConsole().Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            logger.LogError(result.Error, "Database migration failed ({PhaseLabel})", phaseLabel);
            throw new InvalidOperationException($"Database migration failed. See logs for details. ({phaseLabel})", result.Error);
        }
    }

    private static int GetMigrationVersion(string resourceName)
    {
        var match = Regex.Match(resourceName, @"\bV(?<version>\d+)_", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups["version"].Value, out var version)
            ? version
            : int.MaxValue;
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
