using ActoEngine.WebApi.Infrastructure.Database;
using ActoEngine.WebApi.Shared;
using Dapper;

namespace ActoEngine.WebApi.Features.Notifications;

public interface INotificationRepository
{
    Task<int> CreateAsync(int userId, CreateNotificationRequest request, CancellationToken cancellationToken = default);
    Task<IEnumerable<NotificationDto>> GetByUserAsync(int userId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> MarkAsReadAsync(int userId, int notificationId, CancellationToken cancellationToken = default);
    Task<int> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default);
}

public class NotificationRepository(IDbConnectionFactory connectionFactory, ILogger<NotificationRepository> logger)
    : BaseRepository(connectionFactory, logger), INotificationRepository
{
    public async Task<int> CreateAsync(int userId, CreateNotificationRequest request, CancellationToken cancellationToken = default)
    {
        var parameters = new
        {
            UserId = userId,
            request.ProjectId,
            request.Type,
            request.Title,
            request.Message
        };

        return await ExecuteScalarAsync<int>(NotificationQueries.Insert, parameters, cancellationToken);
    }

    public async Task<IEnumerable<NotificationDto>> GetByUserAsync(int userId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
    {
        return await QueryAsync<NotificationDto>(NotificationQueries.GetByUser, new { UserId = userId, Limit = limit, Offset = offset }, cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await ExecuteScalarAsync<int>(NotificationQueries.GetUnreadCount, new { UserId = userId }, cancellationToken);
    }

    public async Task<bool> MarkAsReadAsync(int userId, int notificationId, CancellationToken cancellationToken = default)
    {
        var affected = await ExecuteAsync(NotificationQueries.MarkAsRead, new { UserId = userId, NotificationId = notificationId }, cancellationToken);
        return affected > 0;
    }

    public async Task<int> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(NotificationQueries.MarkAllAsRead, new { UserId = userId }, cancellationToken);
    }
}
