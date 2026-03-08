namespace ActoEngine.WebApi.Features.Notifications;

public class NotificationDto
{
    public int NotificationId { get; set; }
    public int UserId { get; set; }
    public int? ProjectId { get; set; }
    public required string Type { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

public class CreateNotificationRequest
{
    public int? ProjectId { get; set; }
    public required string Type { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
}

public class UnreadCountResponse
{
    public int UnreadCount { get; set; }
}
