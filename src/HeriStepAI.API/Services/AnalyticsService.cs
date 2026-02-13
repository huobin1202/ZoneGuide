using HeriStepAI.API.Data;
using HeriStepAI.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace HeriStepAI.API.Services;

public interface IAnalyticsService
{
    Task UploadAnalyticsAsync(AnalyticsUploadDto data);
    Task<DashboardAnalyticsDto> GetDashboardAsync(DateTime? from, DateTime? to);
    Task<List<TopPOIDto>> GetTopPOIsAsync(DateTime? from, DateTime? to, int count = 10);
    Task<List<HeatmapPointDto>> GetHeatmapDataAsync(DateTime? from, DateTime? to);
}

public class AnalyticsService : IAnalyticsService
{
    private readonly AppDbContext _context;

    public AnalyticsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task UploadAnalyticsAsync(AnalyticsUploadDto data)
    {
        // Save location histories
        var locationEntities = data.Locations.Select(l => new LocationHistoryEntity
        {
            AnonymousDeviceId = data.AnonymousDeviceId,
            SessionId = l.SessionId,
            Latitude = l.Latitude,
            Longitude = l.Longitude,
            Accuracy = l.Accuracy,
            Speed = l.Speed,
            Heading = l.Heading,
            Altitude = l.Altitude,
            Timestamp = l.Timestamp
        }).ToList();

        if (locationEntities.Any())
        {
            _context.LocationHistories.AddRange(locationEntities);
        }

        // Save narration histories
        var narrationEntities = data.Narrations.Select(n => new NarrationHistoryEntity
        {
            AnonymousDeviceId = data.AnonymousDeviceId,
            SessionId = n.SessionId,
            POIId = n.POIId,
            POIName = n.POIName,
            Language = n.Language,
            StartTime = n.StartTime,
            EndTime = n.EndTime,
            DurationSeconds = n.DurationSeconds,
            TotalDurationSeconds = n.TotalDurationSeconds,
            Completed = n.Completed,
            TriggerType = n.TriggerType,
            TriggerDistance = n.TriggerDistance,
            TriggerLatitude = n.TriggerLatitude,
            TriggerLongitude = n.TriggerLongitude
        }).ToList();

        if (narrationEntities.Any())
        {
            _context.NarrationHistories.AddRange(narrationEntities);

            // Update POI statistics
            foreach (var narration in narrationEntities)
            {
                var date = narration.StartTime.Date;
                var stat = await _context.POIStatistics
                    .FirstOrDefaultAsync(s => s.POIId == narration.POIId && s.Date == date);

                if (stat == null)
                {
                    stat = new POIStatisticsEntity
                    {
                        POIId = narration.POIId,
                        Date = date
                    };
                    _context.POIStatistics.Add(stat);
                }

                stat.ListenCount++;
                if (narration.Completed) stat.CompletedCount++;
                stat.TotalListenDurationSeconds += narration.DurationSeconds;
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<DashboardAnalyticsDto> GetDashboardAsync(DateTime? from, DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var totalPOIs = await _context.POIs.CountAsync(p => p.IsActive);
        var totalTours = await _context.Tours.CountAsync(t => t.IsActive);

        var narrations = await _context.NarrationHistories
            .Where(n => n.StartTime >= fromDate && n.StartTime <= toDate)
            .ToListAsync();

        var totalListens = narrations.Count;
        var uniqueUsers = narrations.Select(n => n.AnonymousDeviceId).Distinct().Count();
        var avgDuration = narrations.Any() ? narrations.Average(n => n.DurationSeconds) : 0;
        var completionRate = totalListens > 0 ? (double)narrations.Count(n => n.Completed) / totalListens : 0;

        return new DashboardAnalyticsDto
        {
            TotalPOIs = totalPOIs,
            TotalTours = totalTours,
            TotalListens = totalListens,
            UniqueUsers = uniqueUsers,
            AverageListenDurationSeconds = avgDuration,
            CompletionRate = completionRate,
            TopPOIs = await GetTopPOIsAsync(fromDate, toDate),
            HeatmapData = await GetHeatmapDataAsync(fromDate, toDate),
            DailyStats = await GetDailyStatsAsync(fromDate, toDate)
        };
    }

    public async Task<List<TopPOIDto>> GetTopPOIsAsync(DateTime? from, DateTime? to, int count = 10)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var stats = await _context.NarrationHistories
            .Where(n => n.StartTime >= fromDate && n.StartTime <= toDate)
            .GroupBy(n => new { n.POIId, n.POIName })
            .Select(g => new TopPOIDto
            {
                POIId = g.Key.POIId.ToString(),
                Name = g.Key.POIName,
                ListenCount = g.Count(),
                AvgDurationSeconds = g.Average(n => n.DurationSeconds),
                CompletionRate = g.Count() > 0 ? (double)g.Count(n => n.Completed) / g.Count() : 0
            })
            .OrderByDescending(s => s.ListenCount)
            .Take(count)
            .ToListAsync();

        return stats;
    }

    public async Task<List<HeatmapPointDto>> GetHeatmapDataAsync(DateTime? from, DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        // Group locations into grid cells
        var locations = await _context.LocationHistories
            .Where(l => l.Timestamp >= fromDate && l.Timestamp <= toDate)
            .ToListAsync();

        var gridSize = 0.001; // ~100m
        var heatmap = locations
            .GroupBy(l => new
            {
                Lat = Math.Round(l.Latitude / gridSize) * gridSize,
                Lon = Math.Round(l.Longitude / gridSize) * gridSize
            })
            .Select(g => new HeatmapPointDto
            {
                Latitude = g.Key.Lat,
                Longitude = g.Key.Lon,
                Weight = g.Count()
            })
            .ToList();

        return heatmap;
    }

    private async Task<List<DailyStatsDto>> GetDailyStatsAsync(DateTime from, DateTime to)
    {
        var stats = await _context.NarrationHistories
            .Where(n => n.StartTime >= from && n.StartTime <= to)
            .GroupBy(n => n.StartTime.Date)
            .Select(g => new DailyStatsDto
            {
                Date = g.Key,
                ListenCount = g.Count(),
                UniqueUsers = g.Select(n => n.AnonymousDeviceId).Distinct().Count(),
                AvgDurationSeconds = g.Average(n => n.DurationSeconds)
            })
            .OrderBy(s => s.Date)
            .ToListAsync();

        return stats;
    }
}
