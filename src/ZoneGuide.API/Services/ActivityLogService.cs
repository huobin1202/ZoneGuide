using ZoneGuide.API.Data;
using ZoneGuide.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ZoneGuide.API.Services;

public interface IActivityLogService
{
    Task LogAsync(string action, string entityType, string? entityId, string? entityName, string? details, int? userId, string userEmail, string userName, string? ipAddress = null);
    Task<(List<ActivityLogDto> Items, int TotalCount)> GetLogsAsync(int page = 1, int pageSize = 20, string? entityType = null, string? action = null, DateTime? from = null, DateTime? to = null);
}

public class ActivityLogService : IActivityLogService
{
    private readonly AppDbContext _context;

    public ActivityLogService(AppDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(string action, string entityType, string? entityId, string? entityName, string? details, int? userId, string userEmail, string userName, string? ipAddress = null)
    {
        var log = new ActivityLogEntity
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EntityName = entityName,
            Details = details,
            UserId = userId,
            UserEmail = userEmail,
            UserName = userName,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        };

        _context.ActivityLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task<(List<ActivityLogDto> Items, int TotalCount)> GetLogsAsync(int page = 1, int pageSize = 20, string? entityType = null, string? action = null, DateTime? from = null, DateTime? to = null)
    {
        var query = _context.ActivityLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(l => l.EntityType == entityType);

        if (!string.IsNullOrEmpty(action))
            query = query.Where(l => l.Action == action);

        if (from.HasValue)
            query = query.Where(l => l.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(l => l.CreatedAt <= to.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new ActivityLogDto
            {
                Id = l.Id,
                Action = l.Action,
                EntityType = l.EntityType,
                EntityId = l.EntityId,
                EntityName = l.EntityName,
                Details = l.Details,
                UserEmail = l.UserEmail,
                UserName = l.UserName,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync();

        return (items, totalCount);
    }
}
