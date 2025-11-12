using Microsoft.AspNetCore.Mvc;
using ActoEngine.WebApi.Services.ProjectService;
using System.Text;
using System.Text.Json;

namespace ActoEngine.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SyncStatusController(IProjectService projectService, ILogger<SyncStatusController> logger) : ControllerBase
    {
        private readonly IProjectService _projectService = projectService;
        private readonly ILogger<SyncStatusController> _logger = logger;

        /// <summary>
        /// Server-Sent Events endpoint for real-time sync status updates
        /// </summary>
        /// <param name="projectId">The project ID to stream sync status for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>SSE stream of sync status updates</returns>
        [HttpGet("stream/{projectId}")]
        [Produces("text/event-stream")]
        public async Task StreamSyncStatus(int projectId, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting SSE stream for project {ProjectId}", projectId);

            // Set SSE headers
            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");
            Response.Headers.Append("X-Accel-Buffering", "no"); // Disable nginx buffering

            try
            {
                string? lastStatus = null;
                int? lastProgress = null;

                while (!cancellationToken.IsCancellationRequested)
                {
                    // Fetch current sync status
                    var syncStatus = await _projectService.GetSyncStatusAsync(projectId);

                    if (syncStatus != null)
                    {
                        // Only send update if status or progress changed
                        bool hasChanged = syncStatus.Status != lastStatus ||
                                        syncStatus.SyncProgress != lastProgress;

                        if (hasChanged)
                        {
                            lastStatus = syncStatus.Status;
                            lastProgress = syncStatus.SyncProgress;

                            // Serialize status to JSON
                            var statusData = new
                            {
                                projectId = projectId,
                                status = syncStatus.Status,
                                progress = syncStatus.SyncProgress,
                                lastSyncAttempt = syncStatus.LastSyncAttempt,
                                timestamp = DateTime.UtcNow
                            };

                            var jsonData = JsonSerializer.Serialize(statusData);

                            // Send SSE formatted message
                            var sseMessage = $"data: {jsonData}\n\n";
                            await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(sseMessage), cancellationToken);
                            await Response.Body.FlushAsync(cancellationToken);

                            _logger.LogDebug("Sent SSE update for project {ProjectId}: {Status} ({Progress}%)",
                                projectId, syncStatus.Status, syncStatus.SyncProgress);

                            // Close stream if sync is completed or failed
                            if (syncStatus.Status == "Completed" ||
                                syncStatus.Status?.StartsWith("Failed") == true)
                            {
                                _logger.LogInformation("Sync finished for project {ProjectId} with status: {Status}",
                                    projectId, syncStatus.Status);
                                break;
                            }
                        }
                    }

                    // Wait 1 second before next check
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("SSE stream cancelled for project {ProjectId}", projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SSE stream for project {ProjectId}", projectId);

                // Send error message
                var errorData = new { error = "Stream error", message = ex.Message };
                var jsonError = JsonSerializer.Serialize(errorData);
                var sseError = $"data: {jsonError}\n\n";

                try
                {
                    await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(sseError), cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
                catch
                {
                    // Ignore errors during error reporting
                }
            }
        }

        /// <summary>
        /// Heartbeat endpoint to keep connection alive
        /// </summary>
        [HttpGet("heartbeat")]
        public IActionResult Heartbeat()
        {
            return Ok(new { status = "alive", timestamp = DateTime.UtcNow });
        }
    }
}
