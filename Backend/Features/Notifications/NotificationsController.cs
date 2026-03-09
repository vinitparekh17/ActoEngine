using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ActoEngine.WebApi.Shared.Extensions;
using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Features.Notifications;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController(INotificationService notificationService) : ControllerBase
{
    private bool TryGetUserId(out int userId)
    {
        var resolved = HttpContext.GetUserId();
        userId = resolved ?? 0;
        return resolved.HasValue;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationDto>>> GetNotifications([FromQuery][Range(1, 100)] int limit = 50, [FromQuery][Range(0, int.MaxValue)] int offset = 0, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "User claim is missing or invalid" });
        }

        var notifications = await notificationService.GetUserNotificationsAsync(userId, limit, offset, cancellationToken);
        return Ok(notifications);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "User claim is missing or invalid" });
        }

        var count = await notificationService.GetUnreadCountAsync(userId, cancellationToken);
        return Ok(new UnreadCountResponse { UnreadCount = count });
    }

    [HttpPut("{notificationId:int}/read")]
    public async Task<IActionResult> MarkAsRead(int notificationId, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "User claim is missing or invalid" });
        }

        var success = await notificationService.MarkAsReadAsync(userId, notificationId, cancellationToken);
        if (!success)
        {
            return NotFound(new { message = "Notification not found or already read" });
        }
        return NoContent();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "User claim is missing or invalid" });
        }

        var updated = await notificationService.MarkAllAsReadAsync(userId, cancellationToken);
        return Ok(new { updatedCount = updated });
    }
}
