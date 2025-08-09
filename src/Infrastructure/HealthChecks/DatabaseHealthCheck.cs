using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Lou.Infrastructure.HealthChecks
{
    public class DatabaseHealthCheck(IDbConnectionFactory connectionFactory, ILogger<DatabaseHealthCheck> logger) : IHealthCheck
    {
        private readonly IDbConnectionFactory _connectionFactory = connectionFactory;
        private readonly ILogger<DatabaseHealthCheck> _logger = logger;

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                
                // Test basic connectivity
                var result = await connection.QuerySingleAsync<int>("SELECT 1");
                
                // Test users table exists
                var tableExists = await connection.QuerySingleAsync<bool>(
                    "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'users')");
                
                // Get connection info
                var version = await connection.QuerySingleAsync<string>("SELECT version()");
                var currentDatabase = await connection.QuerySingleAsync<string>("SELECT current_database()");
                var userCount = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM users WHERE deleted_at IS NULL");
                
                stopwatch.Stop();

                var data = new Dictionary<string, object>
                {
                    ["connection_test"] = result == 1 ? "✅ Success" : "❌ Failed",
                    ["users_table_exists"] = tableExists ? "✅ Yes" : "❌ No",
                    ["database"] = currentDatabase,
                    ["postgres_version"] = version.Split(' ')[1], // Extract just version number
                    ["total_users"] = userCount,
                    ["response_time_ms"] = stopwatch.ElapsedMilliseconds
                };

                if (!tableExists)
                {
                    return HealthCheckResult.Degraded(
                        "Database connected but users table not found", 
                        data: data);
                }

                return HealthCheckResult.Healthy(
                    "Database is healthy and responsive", 
                    data: data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                
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
    }
}