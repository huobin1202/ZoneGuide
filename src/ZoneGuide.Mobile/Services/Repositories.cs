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
/// Repository cho POI Translation - lưu trữ SQLite
/// </summary>
public class POITranslationRepository : IPOITranslationRepository
{
    private readonly DatabaseService _database;

    public POITranslationRepository(DatabaseService database)
    {
        _database = database;
    }

    public async Task<List<POITranslation>> GetByPOIIdAsync(int poiId)
    {
        var db = await _database.GetConnectionAsync();
        return await db.Table<POITranslation>()
            .Where(t => t.POIId == poiId)
            .ToListAsync();
    }

    public async Task<POITranslation?> GetByPOIIdAndLanguageAsync(int poiId, string languageCode)
    {
        var db = await _database.GetConnectionAsync();
        var normalized = NormalizeLanguage(languageCode);
        var primary = GetPrimaryLanguage(normalized);

        var all = await db.Table<POITranslation>()
            .Where(t => t.POIId == poiId)
            .ToListAsync();

        var exact = all.FirstOrDefault(t =>
            string.Equals(NormalizeLanguage(t.LanguageCode), normalized, StringComparison.OrdinalIgnoreCase));

        if (exact != null)
            return exact;

        return all.FirstOrDefault(t =>
            string.Equals(GetPrimaryLanguage(NormalizeLanguage(t.LanguageCode)), primary, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<int> InsertAsync(POITranslation translation)
    {
        var db = await _database.GetConnectionAsync();
        translation.LanguageCode = NormalizeLanguage(translation.LanguageCode);
        translation.CreatedAt = DateTime.UtcNow;
        translation.UpdatedAt = DateTime.UtcNow;
        return await db.InsertAsync(translation);
    }

    public async Task<int> UpdateAsync(POITranslation translation)
    {
        var db = await _database.GetConnectionAsync();
        translation.LanguageCode = NormalizeLanguage(translation.LanguageCode);
        translation.UpdatedAt = DateTime.UtcNow;
        return await db.UpdateAsync(translation);
    }

    public async Task<int> DeleteAsync(int id)
    {
        var db = await _database.GetConnectionAsync();
        return await db.DeleteAsync<POITranslation>(id);
    }

    public async Task<int> InsertOrUpdateAsync(POITranslation translation)
    {
        var db = await _database.GetConnectionAsync();
        translation.LanguageCode = NormalizeLanguage(translation.LanguageCode);

        var candidates = await db.Table<POITranslation>()
            .Where(t => t.POIId == translation.POIId)
            .ToListAsync();

        var existing = candidates.FirstOrDefault(t =>
            string.Equals(NormalizeLanguage(t.LanguageCode), translation.LanguageCode, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            translation.Id = existing.Id;
            translation.CreatedAt = existing.CreatedAt;
            translation.UpdatedAt = DateTime.UtcNow;
            return await db.UpdateAsync(translation);
        }

        translation.CreatedAt = DateTime.UtcNow;
        translation.UpdatedAt = DateTime.UtcNow;
        return await db.InsertAsync(translation);
    }

    private static string NormalizeLanguage(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "vi-VN";

        var value = code.Trim().Replace('_', '-');
        return value.ToLowerInvariant() switch
        {
            var c when c.StartsWith("vi") => "vi-VN",
            var c when c.StartsWith("en") => "en-US",
            var c when c.StartsWith("zh") => "zh-CN",
            var c when c.StartsWith("ja") => "ja-JP",
            var c when c.StartsWith("ko") => "ko-KR",
            var c when c.StartsWith("fr") => "fr-FR",
            _ => value
        };
    }

    private static string GetPrimaryLanguage(string code)
    {
        var normalized = NormalizeLanguage(code);
        var idx = normalized.IndexOf('-');
        return idx > 0 ? normalized[..idx] : normalized;
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

    public async Task<int> DeleteNarrationAsync(int narrationId)
    {
        var db = await _database.GetConnectionAsync();
        return await db.DeleteAsync<NarrationHistory>(narrationId);
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
