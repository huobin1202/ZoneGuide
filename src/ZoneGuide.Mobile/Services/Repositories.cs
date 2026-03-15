using ZoneGuide.Shared.Interfaces;
using ZoneGuide.Shared.Models;

namespace ZoneGuide.Mobile.Services;

/// <summary>
/// Repository cho POI - lưu trữ SQLite
/// </summary>
public class POIRepository : IPOIRepository
{
    private readonly DatabaseService _database;

    public POIRepository(DatabaseService database)
    {
        _database = database;
    }

    public async Task<List<POI>> GetAllAsync()
    {
        var db = await _database.GetConnectionAsync();
        return await db.Table<POI>().ToListAsync();
    }

    public async Task<POI?> GetByIdAsync(int id)
    {
        var db = await _database.GetConnectionAsync();
        return await db.Table<POI>().FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<POI?> GetByCodeAsync(string code)
    {
        var db = await _database.GetConnectionAsync();
        return await db.Table<POI>().FirstOrDefaultAsync(p => p.UniqueCode == code);
    }

    public async Task<List<POI>> GetByTourIdAsync(int tourId)
    {
        var db = await _database.GetConnectionAsync();
        return await db.Table<POI>()
            .Where(p => p.TourId == tourId)
            .OrderBy(p => p.OrderInTour)
            .ToListAsync();
    }

    public async Task<List<POI>> GetActiveAsync()
    {
        var db = await _database.GetConnectionAsync();
        return await db.Table<POI>().Where(p => p.IsActive).ToListAsync();
    }

    public async Task<List<POI>> SearchAsync(string keyword)
    {
        var db = await _database.GetConnectionAsync();
        var lowerKeyword = keyword.ToLower();
        var all = await db.Table<POI>().ToListAsync();
        return all.Where(p => 
            p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            p.ShortDescription.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<int> InsertAsync(POI poi)
    {
        var db = await _database.GetConnectionAsync();
        poi.CreatedAt = DateTime.UtcNow;
        poi.UpdatedAt = DateTime.UtcNow;
        return await db.InsertAsync(poi);
    }

    public async Task<int> UpdateAsync(POI poi)
    {
        var db = await _database.GetConnectionAsync();
        poi.UpdatedAt = DateTime.UtcNow;
        return await db.UpdateAsync(poi);
    }

    public async Task<int> DeleteAsync(int id)
    {
        var db = await _database.GetConnectionAsync();
        return await db.DeleteAsync<POI>(id);
    }

    public async Task<int> InsertOrUpdateAsync(POI poi)
    {
        var existing = await GetByIdAsync(poi.Id);
        if (existing != null)
        {
            return await UpdateAsync(poi);
        }
        return await InsertAsync(poi);
    }

    public async Task<List<POI>> GetByLocationAsync(double lat, double lon, double radiusMeters)
    {
        var all = await GetActiveAsync();
        return all.Where(p => p.CalculateDistance(lat, lon) <= radiusMeters)
            .OrderBy(p => p.CalculateDistance(lat, lon))
            .ToList();
    }
}

/// <summary>
/// Repository cho Tour
/// </summary>
public class TourRepository : ITourRepository
{
    private readonly DatabaseService _database;

    public TourRepository(DatabaseService database)
    {
        _database = database;
    }

    public async Task<List<Tour>> GetAllAsync()
    {
        var db = await _database.GetConnectionAsync();
        return await db.Table<Tour>().ToListAsync();
    }

    public async Task<Tour?> GetByIdAsync(int id)
    {
        var db = await _database.GetConnectionAsync();
        return await db.Table<Tour>().FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Tour?> GetByCodeAsync(string code)
    {
        var db = await _database.GetConnectionAsync();
        return await db.Table<Tour>().FirstOrDefaultAsync(t => t.UniqueCode == code);
    }

    public async Task<List<Tour>> GetActiveAsync()
    {
        var db = await _database.GetConnectionAsync();
        return await db.Table<Tour>().Where(t => t.IsActive).ToListAsync();
    }

    public async Task<List<Tour>> SearchAsync(string keyword)
    {
        var all = await GetAllAsync();
        return all.Where(t =>
            t.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            t.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<int> InsertAsync(Tour tour)
    {
        var db = await _database.GetConnectionAsync();
        tour.CreatedAt = DateTime.UtcNow;
        tour.UpdatedAt = DateTime.UtcNow;
        return await db.InsertAsync(tour);
    }

    public async Task<int> UpdateAsync(Tour tour)
    {
        var db = await _database.GetConnectionAsync();
        tour.UpdatedAt = DateTime.UtcNow;
        return await db.UpdateAsync(tour);
    }

    public async Task<int> DeleteAsync(int id)
    {
        var db = await _database.GetConnectionAsync();
        return await db.DeleteAsync<Tour>(id);
    }

    public async Task<int> InsertOrUpdateAsync(Tour tour)
    {
        var existing = await GetByIdAsync(tour.Id);
        if (existing != null)
        {
            return await UpdateAsync(tour);
        }
        return await InsertAsync(tour);
    }
}

/// <summary>
/// Repository cho Analytics
/// </summary>
public class AnalyticsRepository : IAnalyticsRepository
{
    private readonly DatabaseService _database;

    public AnalyticsRepository(DatabaseService database)
    {
        _database = database;
    }

    public async Task<int> InsertLocationAsync(LocationHistory location)
    {
        var db = await _database.GetConnectionAsync();
        return await db.InsertAsync(location);
    }

    public async Task<int> InsertLocationsAsync(IEnumerable<LocationHistory> locations)
    {
        var db = await _database.GetConnectionAsync();
        return await db.InsertAllAsync(locations);
    }

    public async Task<List<LocationHistory>> GetLocationsBySessionAsync(string sessionId)
    {
        var db = await _database.GetConnectionAsync();
        return await db.Table<LocationHistory>()
            .Where(l => l.SessionId == sessionId)
            .OrderBy(l => l.Timestamp)
            .ToListAsync();
    }

    public async Task<List<LocationHistory>> GetLocationsByDateRangeAsync(DateTime start, DateTime end)
    {
        var db = await _database.GetConnectionAsync();
        return await db.Table<LocationHistory>()
            .Where(l => l.Timestamp >= start && l.Timestamp <= end)
            .ToListAsync();
    }

    public async Task<int> DeleteOldLocationsAsync(DateTime olderThan)
    {
        var db = await _database.GetConnectionAsync();
        var old = await db.Table<LocationHistory>()
            .Where(l => l.Timestamp < olderThan)
            .ToListAsync();
        
        var count = 0;
        foreach (var item in old)
        {
            count += await db.DeleteAsync(item);
        }
        return count;
    }

    public async Task<int> InsertNarrationAsync(NarrationHistory narration)
    {
        var db = await _database.GetConnectionAsync();
        return await db.InsertAsync(narration);
    }

    public async Task<int> UpdateNarrationAsync(NarrationHistory narration)
    {
        var db = await _database.GetConnectionAsync();
        return await db.UpdateAsync(narration);
    }

    public async Task<List<NarrationHistory>> GetNarrationsBySessionAsync(string sessionId)
    {
        var db = await _database.GetConnectionAsync();
        return await db.Table<NarrationHistory>()
            .Where(n => n.SessionId == sessionId)
            .OrderBy(n => n.StartTime)
            .ToListAsync();
    }

    public async Task<List<NarrationHistory>> GetNarrationsByPOIAsync(int poiId)
    {
        var db = await _database.GetConnectionAsync();
        return await db.Table<NarrationHistory>()
            .Where(n => n.POIId == poiId)
            .ToListAsync();
    }

    public async Task<List<NarrationHistory>> GetNarrationsByDateRangeAsync(DateTime start, DateTime end)
    {
        var db = await _database.GetConnectionAsync();
        return await db.Table<NarrationHistory>()
            .Where(n => n.StartTime >= start && n.StartTime <= end)
            .ToListAsync();
    }

    public async Task<POIStatistics?> GetStatisticsAsync(int poiId, DateTime date)
    {
        var db = await _database.GetConnectionAsync();
        var dateOnly = date.Date;
        return await db.Table<POIStatistics>()
            .FirstOrDefaultAsync(s => s.POIId == poiId && s.Date.Date == dateOnly);
    }

    public async Task<int> InsertOrUpdateStatisticsAsync(POIStatistics stats)
    {
        var db = await _database.GetConnectionAsync();
        var existing = await GetStatisticsAsync(stats.POIId, stats.Date);
        if (existing != null)
        {
            stats.Id = existing.Id;
            return await db.UpdateAsync(stats);
        }
        return await db.InsertAsync(stats);
    }

    public async Task<List<POIStatistics>> GetStatisticsByPOIAsync(int poiId, DateTime start, DateTime end)
    {
        var db = await _database.GetConnectionAsync();
        return await db.Table<POIStatistics>()
            .Where(s => s.POIId == poiId && s.Date >= start && s.Date <= end)
            .ToListAsync();
    }

    public async Task<List<POIStatistics>> GetTopPOIsAsync(DateTime start, DateTime end, int count = 10)
    {
        var db = await _database.GetConnectionAsync();
        var all = await db.Table<POIStatistics>()
            .Where(s => s.Date >= start && s.Date <= end)
            .ToListAsync();

        return all.GroupBy(s => s.POIId)
            .Select(g => new POIStatistics
            {
                POIId = g.Key,
                ListenCount = g.Sum(s => s.ListenCount),
                CompletedCount = g.Sum(s => s.CompletedCount),
                TotalListenDurationSeconds = g.Sum(s => s.TotalListenDurationSeconds),
                UniqueUsers = g.Sum(s => s.UniqueUsers)
            })
            .OrderByDescending(s => s.ListenCount)
            .Take(count)
            .ToList();
    }
}
