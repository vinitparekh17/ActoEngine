using System.Collections.Concurrent;
using ActoEngine.WebApi.Features.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ActoEngine.WebApi.Features.LogicalFk;

public interface ILfkThrottleService
{
    /// <summary>
    /// Attempts to queue a background Logical FK detection for the given project.
    /// If a detection was started in the last 5 minutes for this project, it is skipped.
    /// </summary>
    void TryQueueDetection(int projectId);
}

public class LfkThrottleService(
    IServiceScopeFactory scopeFactory,
    ILogger<LfkThrottleService> logger) : ILfkThrottleService
{
    // Tracks the last time a detection successfully STARTED for a given projectId
    private readonly ConcurrentDictionary<int, DateTime> _lastDetectionTimes = new();
    private readonly ConcurrentDictionary<int, byte> _inFlight = new();
    
    // Throttle window (configurable, defaulting to 5 minutes)
    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromMinutes(5);

    public void TryQueueDetection(int projectId)
    {
        if (!_inFlight.TryAdd(projectId, 0))
        {
            logger.LogInformation("Skipping auto LFK detection for project {ProjectId}. Detection already in-flight.", projectId);
            return;
        }

        var now = DateTime.UtcNow;
        try
        {
            if (_lastDetectionTimes.TryGetValue(projectId, out var lastTime) &&
                now - lastTime < ThrottleWindow)
            {
                logger.LogInformation("Skipping auto LFK detection for project {ProjectId}. Last run was {MinutesAgo} minutes ago (throttle window: {Throttle} mins).",
                    projectId, (now - lastTime).TotalMinutes.ToString("F1"), ThrottleWindow.TotalMinutes);
                _inFlight.TryRemove(projectId, out _);
                return;
            }

            _lastDetectionTimes.AddOrUpdate(projectId, now, static (_, _) => DateTime.UtcNow);
            logger.LogInformation("Queueing auto LFK detection for project {ProjectId}.", projectId);

            // Fire and forget in the background
            _ = Task.Run(() => RunDetectionAsync(projectId)).ContinueWith(
                t => logger.LogError(t.Exception, "RunDetectionAsync failed for project {ProjectId}", projectId),
                TaskContinuationOptions.OnlyOnFaulted);
        }
        catch
        {
            _inFlight.TryRemove(projectId, out _);
            throw;
        }
    }

    private async Task RunDetectionAsync(int projectId)
    {
        // Because this runs in a background thread, we must create a new DI scope
        using var scope = scopeFactory.CreateScope();
        var logicalFkService = scope.ServiceProvider.GetRequiredService<ILogicalFkService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        try
        {
            logger.LogInformation("Starting background LFK detection for project {ProjectId}", projectId);
            
            // Execute the heavy detection
            var candidates = await logicalFkService.DetectAndPersistCandidatesAsync(projectId, CancellationToken.None);
            
            logger.LogInformation("Background LFK detection completed for project {ProjectId}. Yielded {Count} candidates.", projectId, candidates.Count);

            // Notify project members
            var title = "Logical FK Detection Complete";
            var message = candidates.Count > 0 
                ? $"Found {candidates.Count} new or updated logical foreign key candidates." 
                : "Detection ran but no new candidates were discovered.";

            await notificationService.CreateForProjectMembersAsync(
                projectId, 
                "LFK_DETECTION_COMPLETE", 
                title, 
                message, 
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background LFK detection failed for project {ProjectId}", projectId);
            
            // On failure, clear the throttle timestamp so the next attempt can try immediately
            _lastDetectionTimes.TryRemove(projectId, out _);
        }
        finally
        {
            _inFlight.TryRemove(projectId, out _);
        }
    }
}
