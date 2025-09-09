using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ActoX.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController(HealthCheckService healthCheckService, ILogger<HealthController> logger) : ControllerBase
    {
        private readonly HealthCheckService _healthCheckService = healthCheckService;
        private readonly ILogger<HealthController> _logger = logger;

        /// <summary>
        /// Gets detailed health status of all system components.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
        {
            try
            {
                var report = await _healthCheckService.CheckHealthAsync(cancellationToken);
                var response = new HealthCheckResponse
                {
                    Status = report.Status.ToString(),
                    TotalDuration = report.TotalDuration,
                    Timestamp = DateTime.UtcNow,
                    Checks = report.Entries.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new HealthCheckItem
                        {
                            Status = kvp.Value.Status.ToString(),
                            Duration = kvp.Value.Duration,
                            Description = kvp.Value.Description,
                            Data = kvp.Value.Data?.ToDictionary(x => x.Key, x => x.Value?.ToString()),
                            Tags = kvp.Value.Tags?.ToList(),
                            Exception = kvp.Value.Exception?.Message
                        })
                };

                _logger.LogInformation("Health check completed with status: {Status}", report.Status);
                return StatusCode(report.Status is HealthStatus.Healthy ? 200 : 503, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return StatusCode(503, new HealthCheckResponse
                {
                    Status = "Unhealthy",
                    TotalDuration = TimeSpan.Zero,
                    Timestamp = DateTime.UtcNow,
                    Checks = [],
                    Error = "Health check service failed"
                });
            }
        }
    }
}