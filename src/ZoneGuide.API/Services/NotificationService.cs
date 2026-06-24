using ZoneGuide.API.Data;
using ZoneGuide.API.Hubs;
using ZoneGuide.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ZoneGuide.API.Services;

public interface INotificationService
{
    Task<List<NotificationDto>> GetNotificationsAsync(bool? isRead = null, bool isDeleted = false);
    Task<NotificationDto?> GetNotificationByIdAsync(int id);
    Task<NotificationDto> CreateNotificationAsync(string title, string message, string type, int? referenceId = null, string? referenceType = null, int? senderId = null);
    Task<bool> MarkAsReadAsync(int id);
    Task<int> MarkAllAsReadAsync();
    Task<bool> DeleteAsync(int id);
    Task<bool> RestoreAsync(int id);
    Task<int> GetUnreadCountAsync();
}

public class NotificationService : INotificationService
{
    private readonly AppDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(AppDbContext context, IHubContext<NotificationHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    public async Task<List<NotificationDto>> GetNotificationsAsync(bool? isRead = null, bool isDeleted = false)
    {
        var query = _context.Notifications
            .Include(n => n.Sender)
            .Where(n => n.IsDeleted == isDeleted)
            .AsQueryable();

        if (isRead.HasValue)
        {
            query = query.Where(n => n.IsRead == isRead.Value);
        }

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        return notifications.Select(MapToDto).ToList();
    }

    public async Task<NotificationDto?> GetNotificationByIdAsync(int id)
    {
        var notification = await _context.Notifications
            .Include(n => n.Sender)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (notification == null) return null;

        if (!notification.IsRead && !notification.IsDeleted)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return MapToDto(notification);
    }

    public async Task<NotificationDto> CreateNotificationAsync(string title, string message, string type, int? referenceId = null, string? referenceType = null, int? senderId = null)
    {
        var notification = new NotificationEntity
        {
            Title = title,
            Message = message,
            Type = type,
            ReferenceId = referenceId,
            ReferenceType = referenceType,
            SenderId = senderId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        var dto = MapToDto(notification);
        await _hubContext.Clients.Group("Notifications").SendAsync("NotificationCreated", dto);

        return dto;
    }

    public async Task<bool> MarkAsReadAsync(int id)
    {
        var notification = await _context.Notifications.FindAsync(id);
        if (notification == null) return false;

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> MarkAllAsReadAsync()
    {
        var unread = await _context.Notifications
            .Where(n => !n.IsRead && !n.IsDeleted)
            .ToListAsync();

        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return unread.Count;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var notification = await _context.Notifications.FindAsync(id);
        if (notification == null) return false;

        notification.IsDeleted = true;
        notification.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RestoreAsync(int id)
    {
        var notification = await _context.Notifications.FindAsync(id);
        if (notification == null || !notification.IsDeleted) return false;

        notification.IsDeleted = false;
        notification.DeletedAt = null;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetUnreadCountAsync()
    {
        return await _context.Notifications
            .CountAsync(n => !n.IsRead && !n.IsDeleted);
    }

    private NotificationDto MapToDto(NotificationEntity entity) => new()
    {
        Id = entity.Id,
        Title = entity.Title,
        Message = entity.Message,
        Type = entity.Type,
        ReferenceId = entity.ReferenceId,
        ReferenceType = entity.ReferenceType,
        SenderId = entity.SenderId,
        SenderName = entity.Sender?.DisplayName,
        SenderAvatarUrl = entity.Sender?.AvatarUrl,
        IsRead = entity.IsRead,
        CreatedAt = entity.CreatedAt,
        ReadAt = entity.ReadAt
    };
}
