using ActoEngine.WebApi.Features.Projects;
using Microsoft.Extensions.Logging;

namespace ActoEngine.WebApi.Features.Notifications;

public interface INotificationService
{
    Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(int userId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> MarkAsReadAsync(int userId, int notificationId, CancellationToken cancellationToken = default);
    Task<int> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default);
    Task CreateForProjectMembersAsync(int projectId, string type, string title, string message, CancellationToken cancellationToken = default);
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

            var successCounter = 0;
            var failureCounter = 0;
            
            foreach (var member in membersList)
            {
                try
                {
                    await repository.CreateAsync(member.UserId, request, cancellationToken);
                    successCounter++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failureCounter++;
                    logger.LogError(ex, "Failed to create project-member notification for user {UserId} in project {ProjectId}", member.UserId, projectId);
                }
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
}
