using ZoneGuide.API.Data;
using ZoneGuide.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ZoneGuide.API.Services;

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
    private const double HeatmapGridSize = 0.001d;

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

            // Batch-update POI daily statistics to avoid one query per narration row.
            var narrationStats = narrationEntities
                .GroupBy(n => new { n.POIId, Date = n.StartTime.Date })
                .Select(g => new
                {
                    g.Key.POIId,
                    g.Key.Date,
                    ListenCount = g.Count(),
                    CompletedCount = g.Count(n => n.Completed),
                    TotalListenDurationSeconds = g.Sum(n => (long)n.DurationSeconds)
                })
                .ToList();

            var statPoiIds = narrationStats.Select(s => s.POIId).Distinct().ToList();
            var statDates = narrationStats.Select(s => s.Date).Distinct().ToList();

            var existingStats = await _context.POIStatistics
                .Where(s => statPoiIds.Contains(s.POIId) && statDates.Contains(s.Date))
                .ToListAsync();

            var statsByKey = existingStats.ToDictionary(
                s => (s.POIId, s.Date),
                s => s);

            foreach (var aggregated in narrationStats)
            {
                var key = (aggregated.POIId, aggregated.Date);
                if (!statsByKey.TryGetValue(key, out var stat))
                {
                    stat = new POIStatisticsEntity
                    {
                        POIId = aggregated.POIId,
                        Date = aggregated.Date
                    };
                    _context.POIStatistics.Add(stat);
                    statsByKey[key] = stat;
                }

                stat.ListenCount += aggregated.ListenCount;
                stat.CompletedCount += aggregated.CompletedCount;
                stat.TotalListenDurationSeconds += aggregated.TotalListenDurationSeconds;
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
        var narrationsQuery = _context.NarrationHistories
            .AsNoTracking()
            .Where(n => n.StartTime >= fromDate && n.StartTime <= toDate)
            .AsQueryable();

        var totalListens = await narrationsQuery.CountAsync();
        var uniqueUsers = await narrationsQuery
            .Select(n => n.AnonymousDeviceId)
            .Distinct()
            .CountAsync();
        var avgDuration = totalListens > 0
            ? await narrationsQuery.AverageAsync(n => (double?)n.DurationSeconds) ?? 0
            : 0;
        var completedCount = totalListens > 0
            ? await narrationsQuery.CountAsync(n => n.Completed)
            : 0;
        var completionRate = totalListens > 0 ? (double)completedCount / totalListens : 0;

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
            .AsNoTracking()
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

        var heatmap = await _context.LocationHistories
            .AsNoTracking()
            .Where(l => l.Timestamp >= fromDate && l.Timestamp <= toDate)
            .GroupBy(l => new
            {
                Lat = Math.Round(l.Latitude / HeatmapGridSize) * HeatmapGridSize,
                Lon = Math.Round(l.Longitude / HeatmapGridSize) * HeatmapGridSize
            })
            .Select(g => new HeatmapPointDto
            {
                Latitude = g.Key.Lat,
                Longitude = g.Key.Lon,
                Weight = g.Count()
            })
            .OrderByDescending(p => p.Weight)
            .ToListAsync();

        return heatmap;
    }

    private async Task<List<DailyStatsDto>> GetDailyStatsAsync(DateTime from, DateTime to)
    {
        var stats = await _context.NarrationHistories
            .AsNoTracking()
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
