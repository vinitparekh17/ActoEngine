using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Lou.Infrastructure.HealthChecks;

public class SystemHealthCheck(ILogger<SystemHealthCheck> logger) : IHealthCheck
{
    private readonly ILogger<SystemHealthCheck> _logger = logger;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var memoryUsageMB = process.WorkingSet64 / 1024 / 1024;
            var cpuTime = process.TotalProcessorTime;

            var data = new Dictionary<string, object>
            {
                ["memory_usage_mb"] = memoryUsageMB,
                ["cpu_time_seconds"] = cpuTime.TotalSeconds,
                ["thread_count"] = process.Threads.Count,
                ["uptime_minutes"] = (DateTime.Now - process.StartTime).TotalMinutes,
                ["environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                ["machine_name"] = Environment.MachineName,
                ["dotnet_version"] = Environment.Version.ToString()
            };

            if (memoryUsageMB > 500)
            {
                _logger.LogWarning("High memory usage detected: {MemoryUsageMB} MB", memoryUsageMB);
                return Task.FromResult(HealthCheckResult.Degraded("High memory usage", data: data));
            }
            else
            {
                return Task.FromResult(HealthCheckResult.Healthy("System is healthy", data: data));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "System health check failed");

            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    "System health check failed",
                    ex,
                    new Dictionary<string, object> { ["error"] = ex.Message }
                )
            );
        }
    }
}