using ZoneGuide.Shared.Models;
using SQLite;

namespace ZoneGuide.Mobile.Services;

/// <summary>
/// Service quản lý SQLite Database
/// </summary>
public class DatabaseService
{
    private SQLiteAsyncConnection? _database;
    private readonly string _dbPath;

    public DatabaseService()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "ZoneGuide.db");
    }

    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_database != null)
            return _database;

        _database = new SQLiteAsyncConnection(_dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

        // Tạo các bảng
        await _database.CreateTableAsync<POI>();
        await EnsurePoiColumnsAsync(_database);
        await _database.CreateTableAsync<POITranslation>();
        await EnsurePoiTranslationColumnsAsync(_database);
        await _database.CreateTableAsync<Tour>();
        await EnsureTourColumnsAsync(_database);
        await _database.CreateTableAsync<TourTranslation>();
        await EnsureTourTranslationColumnsAsync(_database);
        await _database.CreateTableAsync<LocationHistory>();
        await _database.CreateTableAsync<NarrationHistory>();
        await _database.CreateTableAsync<POIStatistics>();

        // Tạo indexes để tối ưu hiệu năng truy vấn
        await CreateIndexesAsync(_database);

        return _database;
    }

    public async Task<int> ClearAllDataAsync()
    {
        var db = await GetConnectionAsync();
        var count = 0;
        count += await db.DeleteAllAsync<POI>();
        count += await db.DeleteAllAsync<POITranslation>();
        count += await db.DeleteAllAsync<Tour>();
        count += await db.DeleteAllAsync<TourTranslation>();
        return count;
    }

    public async Task<int> ClearAnalyticsDataAsync()
    {
        var db = await GetConnectionAsync();
        var count = 0;
        count += await db.DeleteAllAsync<LocationHistory>();
        count += await db.DeleteAllAsync<NarrationHistory>();
        count += await db.DeleteAllAsync<POIStatistics>();
        return count;
    }

    private static async Task EnsureTourColumnsAsync(SQLiteAsyncConnection database)
    {
        var columns = await database.GetTableInfoAsync(nameof(Tour));

        if (!columns.Any(c => string.Equals(c.Name, nameof(Tour.AudioUrl), StringComparison.OrdinalIgnoreCase)))
        {
            await database.ExecuteAsync($"ALTER TABLE {nameof(Tour)} ADD COLUMN {nameof(Tour.AudioUrl)} TEXT");
        }

        if (!columns.Any(c => string.Equals(c.Name, nameof(Tour.AudioFilePath), StringComparison.OrdinalIgnoreCase)))
        {
            await database.ExecuteAsync($"ALTER TABLE {nameof(Tour)} ADD COLUMN {nameof(Tour.AudioFilePath)} TEXT");
        }
    }

    private static async Task EnsurePoiColumnsAsync(SQLiteAsyncConnection database)
    {
        var columns = await database.GetTableInfoAsync(nameof(POI));

        if (!columns.Any(c => string.Equals(c.Name, nameof(POI.AudioDurationSeconds), StringComparison.OrdinalIgnoreCase)))
        {
            await database.ExecuteAsync($"ALTER TABLE {nameof(POI)} ADD COLUMN {nameof(POI.AudioDurationSeconds)} INTEGER");
        }

        if (!columns.Any(c => string.Equals(c.Name, nameof(POI.AudioFileSizeBytes), StringComparison.OrdinalIgnoreCase)))
        {
            await database.ExecuteAsync($"ALTER TABLE {nameof(POI)} ADD COLUMN {nameof(POI.AudioFileSizeBytes)} INTEGER");
        }
    }

    private static async Task EnsurePoiTranslationColumnsAsync(SQLiteAsyncConnection database)
    {
        var columns = await database.GetTableInfoAsync(nameof(POITranslation));

        if (!columns.Any(c => string.Equals(c.Name, nameof(POITranslation.AudioDurationSeconds), StringComparison.OrdinalIgnoreCase)))
        {
            await database.ExecuteAsync($"ALTER TABLE {nameof(POITranslation)} ADD COLUMN {nameof(POITranslation.AudioDurationSeconds)} INTEGER");
        }

        if (!columns.Any(c => string.Equals(c.Name, nameof(POITranslation.AudioFileSizeBytes), StringComparison.OrdinalIgnoreCase)))
        {
            await database.ExecuteAsync($"ALTER TABLE {nameof(POITranslation)} ADD COLUMN {nameof(POITranslation.AudioFileSizeBytes)} INTEGER");
        }
    }

    private static async Task EnsureTourTranslationColumnsAsync(SQLiteAsyncConnection database)
    {
        var columns = await database.GetTableInfoAsync(nameof(TourTranslation));

        if (!columns.Any(c => string.Equals(c.Name, nameof(TourTranslation.AudioUrl), StringComparison.OrdinalIgnoreCase)))
        {
            await database.ExecuteAsync($"ALTER TABLE {nameof(TourTranslation)} ADD COLUMN {nameof(TourTranslation.AudioUrl)} TEXT");
        }

        if (!columns.Any(c => string.Equals(c.Name, nameof(TourTranslation.IsAudioOutdated), StringComparison.OrdinalIgnoreCase)))
        {
            await database.ExecuteAsync($"ALTER TABLE {nameof(TourTranslation)} ADD COLUMN {nameof(TourTranslation.IsAudioOutdated)} INTEGER NOT NULL DEFAULT 0");
        }
    }

    /// <summary>
    /// Tạo các indexes để tối ưu hiệu năng truy vấn SQLite
    /// </summary>
    private static async Task CreateIndexesAsync(SQLiteAsyncConnection database)
    {
        var indexes = new[]
        {
            // POI indexes
            "CREATE INDEX IF NOT EXISTS idx_poi_isactive ON POI(IsActive)",
            "CREATE INDEX IF NOT EXISTS idx_poi_tourid ON POI(TourId)",
            "CREATE INDEX IF NOT EXISTS idx_poi_unique_code ON POI(UniqueCode)",
            "CREATE INDEX IF NOT EXISTS idx_poi_latitude_longitude ON POI(Latitude, Longitude)",
            "CREATE INDEX IF NOT EXISTS idx_poi_category ON POI(Category)",
            
            // POITranslation indexes
            "CREATE INDEX IF NOT EXISTS idx_poi_translation_poiid ON POITranslation(POIId)",
            "CREATE UNIQUE INDEX IF NOT EXISTS idx_poi_translation_unique ON POITranslation(POIId, LanguageCode)",
            
            // Tour indexes
            "CREATE INDEX IF NOT EXISTS idx_tour_isactive ON Tour(IsActive)",
            "CREATE INDEX IF NOT EXISTS idx_tour_unique_code ON Tour(UniqueCode)",
            
            // TourTranslation indexes
            "CREATE INDEX IF NOT EXISTS idx_tour_translation_tourid ON TourTranslation(TourId)",
            "CREATE UNIQUE INDEX IF NOT EXISTS idx_tour_translation_unique ON TourTranslation(TourId, LanguageCode)",
            
            // LocationHistory indexes
            "CREATE INDEX IF NOT EXISTS idx_location_session ON LocationHistory(SessionId)",
            "CREATE INDEX IF NOT EXISTS idx_location_timestamp ON LocationHistory(Timestamp)",
            
            // NarrationHistory indexes
            "CREATE INDEX IF NOT EXISTS idx_narration_poiid ON NarrationHistory(POIId)",
            "CREATE INDEX IF NOT EXISTS idx_narration_starttime ON NarrationHistory(StartTime)",
            "CREATE INDEX IF NOT EXISTS idx_narration_session ON NarrationHistory(SessionId)",
            
            // POIStatistics indexes
            "CREATE UNIQUE INDEX IF NOT EXISTS idx_poi_stats_unique ON POIStatistics(POIId, Date)",
            "CREATE INDEX IF NOT EXISTS idx_poi_stats_date ON POIStatistics(Date)"
        };

        foreach (var indexSql in indexes)
        {
            try
            {
                await database.ExecuteAsync(indexSql);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create index: {indexSql}, Error: {ex.Message}");
            }
        }
    }
}
