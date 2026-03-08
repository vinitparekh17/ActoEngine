using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActoEngine.WebApi.Features.Notifications;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController(INotificationService notificationService, ILogger<NotificationsController> logger) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationDto>>> GetNotifications([FromQuery] int limit = 50, [FromQuery] int offset = 0, CancellationToken cancellationToken = default)
    {
        var notifications = await notificationService.GetUserNotificationsAsync(UserId, limit, offset, cancellationToken);
        return Ok(notifications);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        var count = await notificationService.GetUnreadCountAsync(UserId, cancellationToken);
        return Ok(new UnreadCountResponse { UnreadCount = count });
    }

    [HttpPut("{notificationId:int}/read")]
    public async Task<IActionResult> MarkAsRead(int notificationId, CancellationToken cancellationToken = default)
    {
        var success = await notificationService.MarkAsReadAsync(UserId, notificationId, cancellationToken);
        if (!success)
        {
            return NotFound(new { message = "Notification not found or already read" });
        }
        return NoContent();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        var updated = await notificationService.MarkAllAsReadAsync(UserId, cancellationToken);
        return Ok(new { updatedCount = updated });
    }
}
