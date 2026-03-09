using ActoEngine.WebApi.Features.Projects;
using Microsoft.Extensions.Logging;

namespace ActoEngine.WebApi.Features.Notifications;

public interface INotificationService
{
    Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(int userId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> MarkAsReadAsync(int userId, int notificationId, CancellationToken cancellationToken = default);
    Task<int> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default);
    Task CreateForUserAsync(int userId, CreateNotificationRequest request, CancellationToken cancellationToken = default);
    Task CreateForProjectMembersAsync(int projectId, string type, string title, string message, CancellationToken cancellationToken = default);
    Task CleanupOldNotificationsAsync(int retentionDays = 30, CancellationToken cancellationToken = default);
}

public class NotificationService(
    INotificationRepository repository,
    IProjectRepository projectRepository,
    INotificationFailureTracker failureTracker,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(int userId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
    {
        return await repository.GetByUserAsync(userId, limit, offset, cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await repository.GetUnreadCountAsync(userId, cancellationToken);
    }

    public async Task<bool> MarkAsReadAsync(int userId, int notificationId, CancellationToken cancellationToken = default)
    {
        return await repository.MarkAsReadAsync(userId, notificationId, cancellationToken);
    }

    public async Task<int> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await repository.MarkAllAsReadAsync(userId, cancellationToken);
    }

    public async Task CreateForUserAsync(int userId, CreateNotificationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            await repository.CreateAsync(userId, request, cancellationToken);
            failureTracker.Reset();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            var failures = failureTracker.RecordFailure();
            logger.LogError(ex, "Failed to create notification for user {UserId}", userId);

            if (failures >= failureTracker.FailureThreshold)
            {
                logger.LogCritical("Notification creation failure threshold reached ({FailureCount})", failures);
                throw;
            }

            // Propagate the failure so callers can observe the error
            throw;
        }
    }

    public async Task CreateForProjectMembersAsync(int projectId, string type, string title, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var membersList = (await projectRepository.GetProjectMembersAsync(projectId, cancellationToken)).ToList();
            
            var request = new CreateNotificationRequest
            {
                ProjectId = projectId,
                Type = type,
                Title = title,
                Message = message
            };

            const int chunkSize = 25;
            const int maxConcurrency = 5;
            using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            var successCounter = 0;
            var failureCounter = 0;
            for (var i = 0; i < membersList.Count; i += chunkSize)
            {
                var chunk = membersList.Skip(i).Take(chunkSize).ToList();
                var tasks = chunk.Select(async member =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        await CreateForUserAsync(member.UserId, request, cancellationToken);
                        Interlocked.Increment(ref successCounter);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failureCounter);
                        logger.LogError(ex, "Failed to create project-member notification for user {UserId} in project {ProjectId}", member.UserId, projectId);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                await Task.WhenAll(tasks);
            }
            
            logger.LogInformation(
                "Created {Type} notification for project {ProjectId}: {Delivered} delivered, {Failed} failed out of {Total} members",
                type, projectId, successCounter, failureCounter, membersList.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create project-wide notification for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task CleanupOldNotificationsAsync(int retentionDays = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            var deleted = await repository.CleanupOldReadNotificationsAsync(retentionDays, cancellationToken);
            logger.LogInformation("Cleaned up {Count} old read notifications", deleted);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cleanup old read notifications");
        }
    }
}
