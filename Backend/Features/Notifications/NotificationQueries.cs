namespace ActoEngine.WebApi.Features.Notifications;

public static class NotificationQueries
{
    public const string Insert = @"
        INSERT INTO Notifications (UserId, ProjectId, Type, Title, Message, IsRead, CreatedAt)
        OUTPUT inserted.NotificationId
        VALUES (@UserId, @ProjectId, @Type, @Title, @Message, 0, GETUTCDATE())";

    public const string GetByUser = @"
        SELECT 
            NotificationId, UserId, ProjectId, Type, Title, Message, IsRead, CreatedAt, ReadAt
        FROM Notifications
        WHERE UserId = @UserId
        ORDER BY IsRead ASC, CreatedAt DESC
        OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY";

    public const string GetUnreadCount = @"
        SELECT COUNT(1) 
        FROM Notifications 
        WHERE UserId = @UserId AND IsRead = 0";

    public const string MarkAsRead = @"
        UPDATE Notifications
        SET IsRead = 1, ReadAt = GETUTCDATE()
        WHERE NotificationId = @NotificationId AND UserId = @UserId AND IsRead = 0";

    public const string MarkAllAsRead = @"
        UPDATE Notifications
        SET IsRead = 1, ReadAt = GETUTCDATE()
        WHERE UserId = @UserId AND IsRead = 0";

    public const string CleanupOldNotifications = @"
        DELETE FROM Notifications
        WHERE IsRead = 1 AND ReadAt < DATEADD(day, -@RetentionDays, GETUTCDATE())";
}
