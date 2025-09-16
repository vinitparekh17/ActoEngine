// Infrastructure/HealthChecks/DatabaseHealthCheck.cs
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ActoX.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using Dapper;
using ActoX.Infrastructure.Data.Sql;

namespace ActoX.Infrastructure.HealthChecks
{
    public class DatabaseHealthCheck(IDbConnectionFactory connectionFactory, ILogger<DatabaseHealthCheck> logger) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
                
                // Test basic connectivity
                var result = await connection.QuerySingleAsync<int>("SELECT 1");
                
                // Test users table exists
                var tableExists = await connection.QuerySingleAsync<bool>(UserSqlQueries.CheckTableExists);
                
                // Get SQL Server info
                var version = await connection.QuerySingleAsync<string>("SELECT @@VERSION");
                var currentDatabase = await connection.QuerySingleAsync<string>("SELECT DB_NAME()");
                var userCount = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM Users WHERE DeletedAt IS NULL");
                
                stopwatch.Stop();

                var data = new Dictionary<string, object>
                {
                    ["connection_test"] = result == 1 ? "✅ Success" : "❌ Failed",
                    ["users_table_exists"] = tableExists ? "✅ Yes" : "❌ No",
                    ["database"] = currentDatabase,
                    ["sql_server_version"] = ExtractSqlServerVersion(version),
                    ["total_users"] = userCount,
                    ["response_time_ms"] = stopwatch.ElapsedMilliseconds
                };

                if (!tableExists)
                {
                    return HealthCheckResult.Degraded(
                        "Database connected but Users table not found", 
                        data: data);
                }

                return HealthCheckResult.Healthy(
                    "Database is healthy and responsive", 
                    data: data);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database health check failed");
                
                return HealthCheckResult.Unhealthy(
                    "Database health check failed", 
                    ex,
                    new Dictionary<string, object>
                    {
                        ["error"] = ex.Message,
                        ["error_type"] = ex.GetType().Name
                    });
            }
        }

        private static string ExtractSqlServerVersion(string version)
        {
            // Extract version from SQL Server version string
            var lines = version.Split('\n');
            if (lines.Length > 0)
            {
                var firstLine = lines[0];
                if (firstLine.Contains("Microsoft SQL Server"))
                {
                    var parts = firstLine.Split(' ');
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (parts[i] == "Server" && i + 1 < parts.Length)
                        {
                            return parts[i + 1];
                        }
                    }
                }
            }
            return "Unknown";
        }
    }
}