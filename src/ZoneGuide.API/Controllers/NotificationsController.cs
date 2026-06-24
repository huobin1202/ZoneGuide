using ZoneGuide.API.Services;
using ZoneGuide.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ZoneGuide.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Contributor")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<NotificationDto>>> GetNotifications([FromQuery] bool? isRead = null, [FromQuery] bool isDeleted = false)
    {
        var notifications = await _notificationService.GetNotificationsAsync(isRead, isDeleted);
        return Ok(notifications);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<NotificationDto>> GetNotification(int id)
    {
        var notification = await _notificationService.GetNotificationByIdAsync(id);
        if (notification == null)
            return NotFound();
        return Ok(notification);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount()
    {
        var count = await _notificationService.GetUnreadCountAsync();
        return Ok(new UnreadCountDto { Count = count });
    }

    [HttpPost("{id}/read")]
    public async Task<ActionResult> MarkAsRead(int id)
    {
        var result = await _notificationService.MarkAsReadAsync(id);
        if (!result)
            return NotFound();
        return Ok();
    }

    [HttpPost("mark-all-read")]
    public async Task<ActionResult> MarkAllAsRead()
    {
        var count = await _notificationService.MarkAllAsReadAsync();
        return Ok(new { count });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteNotification(int id)
    {
        var result = await _notificationService.DeleteAsync(id);
        if (!result)
            return NotFound();
        return Ok();
    }

    [HttpPost("{id}/restore")]
    public async Task<ActionResult> RestoreNotification(int id)
    {
        var result = await _notificationService.RestoreAsync(id);
        if (!result)
            return NotFound();
        return Ok();
    }
}
