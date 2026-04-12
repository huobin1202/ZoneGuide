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
        await _database.CreateTableAsync<POITranslation>();
        await _database.CreateTableAsync<Tour>();
        await EnsureTourColumnsAsync(_database);
        await _database.CreateTableAsync<TourTranslation>();
        await EnsureTourTranslationColumnsAsync(_database);
        await _database.CreateTableAsync<LocationHistory>();
        await _database.CreateTableAsync<NarrationHistory>();
        await _database.CreateTableAsync<POIStatistics>();

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
}
