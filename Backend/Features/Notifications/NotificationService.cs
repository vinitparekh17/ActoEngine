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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create notification for user {UserId}", userId);
            // Fire and forget usually, don't throw to break main flows
        }
    }

    public async Task CreateForProjectMembersAsync(int projectId, string type, string title, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get all members of the project
            // Assuming ProjectRepository has a way to get members. Let's fetch the project to ensure it exists, 
            // then perhaps there's a GetProjectMembersAsync or similar.
            // Let me check IProjectRepository first.
            var members = await projectRepository.GetProjectMembersAsync(projectId, cancellationToken);
            
            var request = new CreateNotificationRequest
            {
                ProjectId = projectId,
                Type = type,
                Title = title,
                Message = message
            };

            foreach (var member in members)
            {
                await repository.CreateAsync(member.UserId, request, cancellationToken);
            }
            
            logger.LogInformation("Created {Type} notification for {Count} members of project {ProjectId}", type, members.Count(), projectId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create project-wide notification for project {ProjectId}", projectId);
        }
    }

    public async Task CleanupOldNotificationsAsync(int retentionDays = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            var deleted = await repository.CleanupOldReadNotificationsAsync(retentionDays, cancellationToken);
            logger.LogInformation("Cleaned up {Count} old read notifications", deleted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cleanup old read notifications");
        }
    }
}
